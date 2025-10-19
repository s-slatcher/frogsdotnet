using Godot;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using Vector2 = Godot.Vector2;

public partial class TerrainMap : GodotObject
{
    

    public float MaxHeight = 80;
    public float MinHeight = 0;

    public float CliffGrade =  float.Pi / 15;
    public float CliffSideNoiseWidth = 8;

    private float groupingTolerance = 8f; // maximum height difference allowed in one group of points
    private float MinSurfaceWidth = 6; // minimum width difference of the points contained in a group, priority over groupingTolerance


    private float landDistortShiftFactor = 0.33f;
    private Random rand;
    private HeightMap heightMap;
    private HeightMap landDistortionMap;

    private GeometryUtils gu = new();

    public List<Vector2> CachedTowerAnchorPoints = new();

    public TerrainMap(int seed = 0)
    {
        rand = new(seed);
        heightMap = new(seed, 0.025f);
        heightMap.RemapToHeightRange = true;

        var distortFreq = 0.04f;
        var distortLayers = 3;
        var layerFrequencyMult = 2.25f;
        var layerStrengthMult = 0.55f;
        landDistortionMap = new(seed, distortFreq, distortLayers, layerFrequencyMult, layerStrengthMult);

    }

    void UpdateHeightMaps()
    {
        heightMap.MaxHeight = MaxHeight;
        heightMap.MinHeight = MinHeight;

        landDistortionMap.MaxHeight = CliffSideNoiseWidth * (1 - landDistortShiftFactor);
        landDistortionMap.MinHeight = landDistortionMap.MaxHeight - CliffSideNoiseWidth;
    }

    public List<Polygon2D> GenerateNext(float width)
    {
        CachedTowerAnchorPoints = new();
        UpdateHeightMaps();

        var points = heightMap.GetNextHeights(width);
        var curve = gu.PointsToCurve(points.ToArray());
        points = curve.Tessellate().ToList();

        GD.Print("for width: ", width, " height map points: ", points.Count);
        List<Rect2> towerRects = GroupPoints(points);
        var towerPolygons = new List<Vector2[]>();

        foreach (var rect in towerRects)
        {
            CachedTowerAnchorPoints.Add(rect.GetCenter() + new Vector2(0, rect.Size.Y / 2));
            var topWidth = rect.Size.X;
            var height = rect.Size.Y;
            var baseWidth = float.Tan(CliffGrade) * height * 2 + topWidth;
            towerPolygons.Add(new NoiseEdgePoly(height, baseWidth, topWidth, false).Polygon);    

        }

        GD.Print("generated terrain of width ", width, " merged tower count: ", towerPolygons.Count);
       
        ArrangeTowers(towerPolygons);
        var mergedList = ReduceMergePolygons(towerPolygons);


        return mergedList;
    }

    public Vector2[] GenerateNextTerrainPolygon(float width)
    {
        var mergedList = GenerateNext(width);
        var poly = mergedList[0].Polygon; // FIX EVENTUALLY, assumes only 1 polygon produced always
        return poly;
    }

    

    private List<Polygon2D> ReduceMergePolygons(List<Vector2[]> towerPolygons)
    {
        var mergedList = new List<Polygon2D>();
        var currentMerge = towerPolygons[0];

        foreach (var poly in towerPolygons)
        {
            var mergeResult = Geometry2D.MergePolygons(poly, currentMerge);
            if (mergeResult.Count > 1 && !Geometry2D.IsPolygonClockwise(mergeResult[1]))
            {
                GD.Print("failed merge");
                var poly2D = ConvertToPolygonInstance(currentMerge);
                mergedList.Add(poly2D);
                currentMerge = poly;
            }
            else
            {
                currentMerge = mergeResult[0];
            }

        }

        mergedList.Add(ConvertToPolygonInstance(currentMerge));
        return mergedList;
    }

    private Polygon2D ConvertToPolygonInstance(Vector2[] polygon)
    {
        var rect = GeometryUtils.RectFromPolygon(polygon);
        var normalizedPoly = gu.TranslatePolygon(polygon, -rect.Position);
        // var smallPoly = Geometry2D.OffsetPolygon(normalizedPoly, -5)[0];
        // normalizedPoly = Geometry2D.MergePolygons(normalizedPoly, smallPoly)[0];
        var poly2D = new Polygon2D() { Polygon = normalizedPoly };
        poly2D.Position = rect.Position;

        return poly2D;

    }

    private void ArrangeTowers(List<Vector2[]> towers)
    {


        for (int i = 0; i < towers.Count - 1; i++)
        {

            var towerPoly = towers[i];
            var nextTowerPoly = towers[i + 1];
            var rect = GeometryUtils.RectFromPolygon(towerPoly);
            var rectNext = GeometryUtils.RectFromPolygon(nextTowerPoly);
            bool leftUnitIsShorter = rect.Size.Y < rectNext.Size.Y;

            Vector2 topEdge;
            Vector2 mergePoint;
            Vector2 alignTranslation;

            if (leftUnitIsShorter)
            {
                topEdge = HeightMatchPointToUnitEdge(towerPoly, rect, rect.End.Y, true);
                mergePoint = HeightMatchPointToUnitEdge(nextTowerPoly, rectNext, topEdge.Y, false);
                alignTranslation = new Vector2((topEdge - mergePoint).X, 0);

            }
            else
            {
                topEdge = HeightMatchPointToUnitEdge(nextTowerPoly, rectNext, rectNext.End.Y, false);
                mergePoint = HeightMatchPointToUnitEdge(towerPoly, rect, topEdge.Y, true);
                alignTranslation = new Vector2((mergePoint - topEdge).X, 0);
            }

            towers[i + 1] = gu.TranslatePolygon(towers[i + 1], alignTranslation);
            CachedTowerAnchorPoints[i + 1] += alignTranslation;

        }
    }

 
 
    private List<Rect2> GroupPoints(List<Vector2> points)
    {
        var groupRects = new List<Rect2>();

        var groups = new List<List<Vector2>>();
        var currentGroup = new List<Vector2>();
        var groupMin = float.MaxValue;
        var groupMax = float.MinValue;
        for (int i = 1; i < points.Count; i++)
        {
            var pointHeight = points[i].Y;
            groupMin = Math.Min(pointHeight, groupMin);
            groupMax = Math.Max(pointHeight, groupMax);
            currentGroup.Add(points[i]);
            if (groupMax - groupMin > groupingTolerance)
            {
                var groupWidth = currentGroup[0].X - currentGroup[^1].X;
                if (Math.Abs(groupWidth) > MinSurfaceWidth)
                {
                    groups.Add(currentGroup);
                    currentGroup = new() { points[i] };
                    groupMin = groupMax = points[i].Y;
                }
            }
        }

        for (int i = 0; i < groups.Count; i++)
        {
            var averageY = groups[i].Aggregate(0f, (acc, vec) => acc += vec.Y);
            averageY /= groups[i].Count;
            var p1 = groups[i][0];
            var p2 = groups[i][^1];
            p1.Y = p2.Y = averageY;

            var rect = new Rect2() { Position = new Vector2(p1.X, 0), End = p2 };
            groupRects.Add(rect);
        }


        return groupRects;
    }

   
    private Vector2 HeightMatchPointToUnitEdge(Vector2[] towerPolygon, Rect2 towerRect, float matchHeight, bool rightEdge)
    {

        var matchingPoint = Vector2.Zero;
        var lowestDelta = float.MaxValue;

        for (int i = 0; i < towerPolygon.Length; i++)
        {
            var unitP = towerPolygon[i];
            bool isRightEdge = unitP.X > towerRect.Size.X / 2 + towerRect.Position.X;
            if (isRightEdge != rightEdge) continue; 

            var delta = Math.Abs(matchHeight - unitP.Y);
            if (delta < lowestDelta)
            {
                matchingPoint = towerPolygon[i];
                lowestDelta = delta;
            }
        }
        return matchingPoint;

    }

}

public struct TerrainPolygon(Vector2[] polygon, Curve heightCurve, Rect2 rect)
{
    public Vector2[] Polygon = polygon;
    public Rect2 BoundingRect = rect;
    public Curve SimplifiedHeightCurve = heightCurve;
}
