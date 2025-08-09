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

    public float CliffGrade = float.Pi / 15;
    public float CliffSideNoiseWidth = 8;

    private float groupingTolerance = 14f; // maximum height difference allowed in one group of points
    private float MinSurfaceWidth = 8; // minimum width difference of the points contained in a group, priority over groupingTolerance


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


    public List<Polygon2D> GenerateFromPointList(List<Vector2> pointList)
    {
        List<Rect2> towerRects = GroupPoints(pointList);
        var towerPolygons = towerRects.Select(GetTowerPolygon).ToList();
        ArrangeTowers(towerPolygons);
        var mergedList = ReduceMergePolygons(towerPolygons);
        return mergedList;
    }

    public List<Polygon2D> GenerateNext(float width)
    {
        CachedTowerAnchorPoints = new();
        GD.Print("called gen next width");
        UpdateHeightMaps();

        var points = heightMap.GetNextHeights(width);
        GD.Print(width, " width height map --- ", points.Count, " total points");

        List<Rect2> towerRects = GroupPoints(points);
        var towerPolygons = towerRects.Select(GetTowerPolygon).ToList();
        ArrangeTowers(towerPolygons);
        var mergedList = ReduceMergePolygons(towerPolygons);

        foreach (var p in CachedTowerAnchorPoints) GD.Print(p);

        return mergedList;
    }

    public TerrainPolygon GenerateNextTerrainPolygon(float width)
    {
        var mergedList = GenerateNext(width);
        var poly = mergedList[0].Polygon; // fix later, assumes only 1 polygon produced always
        var rect = GeometryUtils.RectFromPolygon(poly);

        var pointList = CachedTowerAnchorPoints;
        Curve curve = new();
        curve.MaxDomain = rect.Size.X;
        curve.MinValue = 0;
        curve.MaxValue = MaxHeight;
        curve.MinValue = MinHeight;
        foreach (var p in pointList) curve.AddPoint(p);


        return new TerrainPolygon(poly, curve, rect); 
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
            bool leftUnitIsShorter = towerPoly[0].Y < nextTowerPoly[0].Y;

            Vector2 topEdge;
            Vector2 mergePoint;
            Vector2 alignTranslation;

            if (leftUnitIsShorter)
            {
                topEdge = towerPoly[^1];
                mergePoint = HeightMatchPointToUnitEdge(nextTowerPoly, topEdge, false);
                alignTranslation = new Vector2((topEdge - mergePoint).X, 0);

            }
            else
            {
                topEdge = nextTowerPoly[0];
                mergePoint = HeightMatchPointToUnitEdge(towerPoly, topEdge, true);
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

        GD.Print("terrain hill anchor points: ");

        // CachedTowerAnchorPoints = groupRects.Select( rect => rect.GetCenter() - new Vector2(0, rect.Size.Y / 2) ).ToList();

        return groupRects;
    }

    private Vector2[] GetTowerPolygon(Rect2 rect)
    {
        var sideLength = rect.Size.Y / float.Cos(CliffGrade);
        var baseWidth = Math.Sqrt(sideLength * sideLength - rect.Size.Y * rect.Size.Y) * 2 + rect.Size.X;

        var leftSide = landDistortionMap.GetNextHeights(sideLength).ToArray();
        var rightSide = landDistortionMap.GetNextHeights(sideLength).ToArray();

        // normalize positions
        leftSide = gu.TranslatePolygon(leftSide, new Vector2(-leftSide[0].X, 0));
        rightSide = gu.TranslatePolygon(rightSide, new Vector2(-rightSide[0].X, 0));

        // add ending points to transition into flat surface
        leftSide = CurlEndsOfNoiseLine(leftSide, 1, new Vector2(1, 0), -1);
        rightSide = CurlEndsOfNoiseLine(rightSide, 1, new Vector2(1, 0), -1);

        //rotate to match side angle
        leftSide = gu.RotatePolygon(leftSide, float.Pi / 2 - CliffGrade);
        rightSide = gu.RotatePolygon(rightSide, float.Pi / 2 - CliffGrade);

        rightSide = gu.ScalePolygon(rightSide, new Vector2(-1, 1));


        var translateX = baseWidth;
        var translation = new Vector2((float)translateX, 0);
        var translatedRightSide = gu.TranslatePolygon(rightSide.ToArray(), translation);
        var combinedUnitPoly = leftSide.Reverse().Concat(translatedRightSide).ToArray();

        // convert to curve and smooth, then tesselate back into polygon
        var smoothCurve = gu.PointsToCurve(combinedUnitPoly, 0.75f, false);
        var tesselatePoly = smoothCurve.Tessellate(5, 4);

        var polyRect = GeometryUtils.RectFromPolygon(combinedUnitPoly);

        CachedTowerAnchorPoints.Add(polyRect.GetCenter() + new Vector2(0, polyRect.Size.Y / 2));

        return tesselatePoly;
        // return gu.TranslatePolygon(tesselatePoly, rect.Position);        

    }

    private Vector2[] CurlEndsOfNoiseLine(Vector2[] noiseLine, float curlRadius, Vector2 noiseOrientation, int curlDirection)
    {
        if (curlDirection != -1 && curlDirection != 1) return noiseLine;
        List<Vector2> newNoiseList = [.. noiseLine];
        var edgeVector = noiseOrientation.Normalized() * curlRadius;
        var bottomEdgeVector = edgeVector * -1;

        var topEdgeCenter = edgeVector.Rotated(float.Pi / 2 * curlDirection) + noiseLine[^1];
        var bottomEdgeCenter = edgeVector.Rotated(float.Pi / 2 * curlDirection) + noiseLine[0];

        Vector2 topEdge1 = topEdgeCenter + edgeVector.Rotated(-curlDirection * float.Pi / 4);
        Vector2 topEdge2 = topEdgeCenter + edgeVector;

        Vector2 bottomEdge1 = bottomEdgeCenter + bottomEdgeVector;
        Vector2 bottomEdge2 = bottomEdgeCenter + bottomEdgeVector.Rotated(curlDirection * float.Pi / 4);

        newNoiseList.AddRange([topEdge1, topEdge2]);
        newNoiseList.InsertRange(0, [bottomEdge1, bottomEdge2]);

        return newNoiseList.ToArray();
    }

    private Vector2 HeightMatchPointToUnitEdge(Vector2[] towerPolygon, Vector2 point, bool rightEdge)
    {

        Vector2 indexRange;
        if (rightEdge) indexRange = new Vector2(towerPolygon.Length / 2 - 1, towerPolygon.Length);
        else indexRange = new Vector2(0, towerPolygon.Length / 2 + 1);

        var matchingPoint = Vector2.Zero;
        var lowestDelta = float.MaxValue;

        for (int i = (int)indexRange.X; i < indexRange.Y; i++)
        {
            var unitP = towerPolygon[i];
            var delta = Math.Abs(point.Y - unitP.Y);
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
    public readonly Vector2[] Polygon = polygon;
    public readonly Rect2 BoundingRect = rect;
    public readonly Curve SimplifiedHeightCurve = heightCurve;
}
