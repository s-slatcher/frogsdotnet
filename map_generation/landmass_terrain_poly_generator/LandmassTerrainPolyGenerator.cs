using Godot;
using System;
using System.Collections.Generic;

public partial class LandmassTerrainPolyGenerator : Resource
{


    HeightmapRects heightMap = new();

    public RandomNumberGenerator rng = new();

    public float Height = 80;
    public float CliffGrade =  float.Pi / 10;

    float PlatformFilteringWidth = 1;
    float BaseFilteringOdds = 0.25f;
    float CutoffWidth = 1;

    GeometryUtils gu = new();


    public LandmassTerrainPolyGenerator(int seed = 1)
    {
        rng.Seed = (ulong)seed;
        heightMap.NoiseSeed = seed;

    }
    
    

    public Vector2[] GenerateTerrainPoly(float startX, float endX)
    {
        var poly = new List<Vector2>();

        heightMap.Range = new Vector2(startX, endX);

        var platRects = heightMap.GetRects();
        // var filteredRects = FilterSmallPlatforms(platRects);
        var filteredRects = platRects;

        var towerPolygons = new List<Vector2[]>();

        foreach (var rect in filteredRects)
        {
            var topWidth = rect.Size.X;
            var height = rect.Size.Y;
            var baseWidth = float.Tan(CliffGrade) * height * 2 + topWidth;
            towerPolygons.Add(new NoiseEdgePoly(height, baseWidth, topWidth, false).Polygon);

        }

        ArrangeTowers(towerPolygons);
        var mergedList = ReduceMergePolygons(towerPolygons);

        var finalPoly = mergedList[0].Polygon;    // CORRECT THIS FUNNY BUSINESS



        return mergedList[0].Polygon;

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

        }
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

    List<Rect2> FilterSmallPlatforms(List<Rect2> rectList)
    {
        var filteredList = new List<Rect2>();
        foreach (var rect in rectList)
        {
            var width = rect.Size.X;
            if (width < CutoffWidth) continue;
            if (width <= PlatformFilteringWidth)
            {
                var filterThreshold = BaseFilteringOdds * float.Pow(width / PlatformFilteringWidth, 0.5f);
                // if (rng.Randf() > filterThreshold) continue;
            }
            filteredList.Add(rect);
        }

        GD.Print("filtered rects by width: ", rectList.Count - filteredList.Count);

        return filteredList;

    }

}
