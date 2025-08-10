using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class NoiseEdgePoly : GodotObject
{   

    

    float BaseWidth;
    float TopWidth;
    float Height;
    public float DistortWidth = 8;
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

        var cliffGrade =  -1 * float.Atan2(Height, (TopWidth - BaseWidth) / 2);

        var towerRect = new Rect2(Vector2.Zero, new Vector2(baseWidth, Height));

        Polygon = GetTowerPolygon( towerRect, cliffGrade );

    }

    public NoiseEdgePoly(Rect2 rect, float grade)
    {

        
        SetDistortMap();
        Polygon = GetTowerPolygon(rect, grade);
    }

    public void SetDistortMap()
    {   


        var distortFreq = 0.04f;
        var distortLayers = 3;
        var layerFrequencyMult = 2.25f;
        var layerStrengthMult = 0.55f;
        DistortMap = new(0, distortFreq, distortLayers, layerFrequencyMult, layerStrengthMult);
    }


    private Vector2[] GetTowerPolygon(Rect2 rect, float grade)
    {
        
        DistortMap.MaxHeight = DistortWidth * (1 - DistortShiftFactor);
        DistortMap.MinHeight = DistortMap.MaxHeight - DistortWidth;

        var sideLength = rect.Size.Y / float.Cos(grade);
        var baseWidth = Math.Sqrt(sideLength * sideLength - rect.Size.Y * rect.Size.Y) * 2 + rect.Size.X;

        var leftSide = DistortMap.GetNextHeights(sideLength).ToArray();
        var rightSide = DistortMap.GetNextHeights(sideLength).ToArray();

        // normalize positions
        leftSide = gu.TranslatePolygon(leftSide, new Vector2(-leftSide[0].X, 0));
        rightSide = gu.TranslatePolygon(rightSide, new Vector2(-rightSide[0].X, 0));

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
        var combinedUnitPoly = leftSide.Reverse().Concat(translatedRightSide).ToArray();

        // convert to curve and smooth, then tesselate back into polygon
        var smoothCurve = gu.PointsToCurve(combinedUnitPoly, 0.75f, false);
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
