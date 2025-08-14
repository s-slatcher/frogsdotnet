using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = Godot.Vector2;

public partial class NoiseEdgePoly : GodotObject
{

  
    

    float BaseWidth;
    float TopWidth;
    float Height;
    public float DistortWidth = 3;
    private float DistortShiftFactor = 0.33f; // degree noise pushes terrain in vs out, lower num for wider terrain

    GeometryUtils gu = new();

    HeightMap DistortMap;


    public Vector2[] Polygon;

    public NoiseEdgePoly(float baseWidth = 0.5f, float topWidth = 10, float height = 5)
    {
        BaseWidth = baseWidth;
        TopWidth = topWidth;
        Height = height;

        SetDistortMap();

        var cliffGrade =  - float.Atan2(Height, (TopWidth - BaseWidth) / 2);

        var towerRect = new Rect2(Vector2.Zero, new Vector2(baseWidth, Height));

        Polygon = GetTowerPolygon( towerRect, cliffGrade );

    }

    public NoiseEdgePoly(Rect2 rect, float grade)
    {
        SetDistortMap();
        Polygon = GetTowerPolygon(rect, grade);
    }

    public NoiseEdgePoly(double height, float topWidth, float baseWidth)
    {
        SetDistortMap();
        Polygon = GetTowerPolygon_new((float)height, topWidth, baseWidth);
    }

    public void SetDistortMap()
    {


        var distortFreq = 0.04f;
        var distortLayers = 3;
        var layerFrequencyMult = 2.25f;
        var layerStrengthMult = 0.55f;
        DistortMap = new(0, distortFreq, distortLayers, layerFrequencyMult, layerStrengthMult);
    }

    private Vector2[] GetTowerPolygon_new(float height, float topWidth, float baseWidth)
    {
        
        // force only positive heights
        DistortMap.MaxHeight = DistortWidth;
        DistortMap.MinHeight = 0;

        // find top corners ( with bottom left corner of base as origin )
        // foot point is middle point of base platform
        var footPoint = new Vector2(baseWidth / 2, 0);

        var bottomLeftPoint = Vector2.Zero;
        var bottomRightPoint = new Vector2(baseWidth, 0);
        var topLeftPoint = footPoint + new Vector2(-topWidth / 2, height);
        var topRightPoint = footPoint + new Vector2(topWidth / 2, height);

        // left vector from bottom-left to top-left
        var leftVector = topLeftPoint;
        // right vector from top-right point to bottom-right  (to maintain orientation)
        var rightVector = new Vector2(baseWidth, 0) - topRightPoint;
        var sideLength = leftVector.Length();
       

        var leftNoiseEdge = DistortMap.GetNextHeights(sideLength, true);
        var rightNoiseEdge = DistortMap.GetNextHeights(sideLength, true);


        // THIS IS WRONG:
        // EACH POINT IS BEING ROTATED BY THE ANGLE OF ITS VECTOR TO THE 0,0, THEY ALL NEED TO ROTATE THE SAME ANGLE
        leftNoiseEdge = leftNoiseEdge.Select(p => p.Rotated(p.AngleTo(leftVector)) + bottomLeftPoint).ToList();
        rightNoiseEdge = leftNoiseEdge.Select(p => p.Rotated(p.AngleTo(rightVector)) + topRightPoint).ToList();

        var polygon = leftNoiseEdge.Concat(rightNoiseEdge).ToArray();

        return polygon;

        
    }

    private Vector2[] GetTowerPolygon(Rect2 rect, float grade)
    {

        DistortMap.MaxHeight = DistortWidth * (1 - DistortShiftFactor);
        DistortMap.MinHeight = DistortMap.MaxHeight - DistortWidth;

        var sideLength = rect.Size.Y / float.Cos(grade);
        var baseWidth = Math.Sqrt(sideLength * sideLength - rect.Size.Y * rect.Size.Y) * 2 + rect.Size.X;

        var leftSide = DistortMap.GetNextHeights(sideLength).ToArray();
        var rightSide = DistortMap.GetNextHeights(sideLength).ToArray();

        var leftLineRect = GeometryUtils.RectFromPolygon(leftSide);
        var rightLineRect = GeometryUtils.RectFromPolygon(rightSide);
        // normalize positions
        leftSide = gu.TranslatePolygon(leftSide, -leftLineRect.Position);
        rightSide = gu.TranslatePolygon(rightSide, -rightLineRect.Position);

        // add ending points to transition into flat surface
        leftSide = CurlEndsOfNoiseLine(leftSide, 1, new Vector2(1, 0), -1);
        rightSide = CurlEndsOfNoiseLine(rightSide, 1, new Vector2(1, 0), -1);

        //rotate to match side angle
        leftSide = gu.RotatePolygon(leftSide, float.Pi / 2 - grade);
        rightSide = gu.RotatePolygon(rightSide, float.Pi / 2 - grade);

        rightSide = gu.ScalePolygon(rightSide, new Vector2(-1, 1));




        var translateX = baseWidth;
        var translation = new Vector2((float)translateX, 0);
        var translatedRightSide = gu.TranslatePolygon(rightSide.ToArray(), translation);
        var combinedUnitPoly = leftSide.Reverse().Concat(translatedRightSide).ToList();


        //insert base points to help curve smoothing
        var first = combinedUnitPoly[0];
        var last = combinedUnitPoly[^1];
        GD.Print("first / last height: ", first.Y, " ", last.Y, "  base width ", baseWidth);
        GD.Print(combinedUnitPoly[combinedUnitPoly.Count / 2]);

        // var halfLess = (last - first) * 0.45f + first;
        // var halfMore = (first - last) * 0.45f + last;
        // combinedUnitPoly.Insert(0, halfLess);
        // combinedUnitPoly.Add(halfMore);




        // convert to curve and smooth, then tesselate back into polygon
        var smoothCurve = gu.PointsToCurve(combinedUnitPoly.ToArray(), 1f, true);
        var tesselatePoly = smoothCurve.Tessellate(5, 4);



        return tesselatePoly;
        // return gu.TranslatePolygon(tesselatePoly, rect.Position);        

    }

    private Vector2[]   CurlEndsOfNoiseLine(Vector2[] noiseLine, float curlRadius, Vector2 noiseOrientation, int curlDirection)
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
}
