using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = Godot.Vector2;

public partial class NoiseEdgePoly : GodotObject
{


    float MaxDistortWidth = 6f;
    float MaxDistortLayerFrequency = 0.15f;
    float MinDistortLayerFrequency = 0.04f; 

    float BaseWidth;
    float TopWidth;
    float Height;
    private float DistortShiftFactor = 0.33f; // degree noise pushes terrain in vs out, lower num for wider terrain

    GeometryUtils gu = new();

    HeightMap DistortMap;


    public Vector2[] Polygon;

    public NoiseEdgePoly(float height, float baseWidth, float topWidth, bool isIsland)
    {
        BaseWidth = baseWidth;
        TopWidth = topWidth;
        Height = height;

        if (isIsland) SetDistortMap(); 
        else SetDistortMap();

    

        var towerRect = new Rect2(Vector2.Zero, new Vector2(baseWidth, Height));

        Polygon = GenerateNoiseEdgePoly(height, baseWidth, topWidth).ToArray();

    }

    
    public void SetDistortMap()
    {
        // automatically adjusting noise settings based on terrain dimensions
        var minWidth = float.Min(BaseWidth, TopWidth);
        var distortWidth = 8f; 


        var distortFreq = 0.06f;
        var distortLayers = 3;
        var layerFrequencyMult = 2f;
        var layerStrengthMult = 0.45f;
        DistortMap = new(1, distortFreq, distortLayers, layerFrequencyMult, layerStrengthMult);


        DistortMap.MaxHeight = distortWidth;
        DistortMap.MinHeight = 0;

    }


      public List<Vector2> GenerateNoiseEdgePoly(float height, float baseWidth, float topWidth)
    {


        var footPoint = new Vector2(baseWidth / 2, 0);

        var bottomLeftPoint = Vector2.Zero;
        var bottomRightPoint = new Vector2(baseWidth, 0);
        var topLeftPoint = footPoint + new Vector2(-topWidth / 2, height);
        var topRightPoint = footPoint + new Vector2(topWidth / 2, height);

        var leftVector = topLeftPoint;
        var sideLength = leftVector.Length();

        // grab noise line, both rotated the same to be mirrored
        var leftEdgeLine = GetNoiseLine(leftVector);
        var rightEdgeLine = GetNoiseLine(leftVector);
       
        // rightEdgeLine = rightEdgeLine.Select(p => p * new Vector2(-1, 1) + new Vector2(baseWidth, 0)).ToList();

        // smooth out the middle of the lines, ignoring final point 
        var smoothLeftEdge = SmoothNoiseLine(leftEdgeLine, rightEdgeLine[0], rightEdgeLine[^1]);
        var smoothRightEdge = SmoothNoiseLine(rightEdgeLine, leftEdgeLine[0], leftEdgeLine[^1]);

        // force slope at both ends to converge onto the average slope of the noise line (maintains the first and last point positions)
        var compressedLeftEdge = CompressNoiseLine(smoothLeftEdge);
        var compressedRightEdge = CompressNoiseLine(smoothRightEdge);
       
        // flip the right edge and translate by base width
        compressedRightEdge = compressedRightEdge.Select(p => p * new Vector2(-1, 1) + new Vector2(baseWidth, 0)).ToList();

        // produce the rounded top and bottom caps connecting the noise lines
        var p1 = compressedLeftEdge[0];
        var d1 = p1 - compressedLeftEdge[1]; 
        var p2 = compressedRightEdge[0];
        var d2 = p2 - compressedRightEdge[1];
        var topEdge = GetRoundedEdge(p1, d1, p2, d2, 8);


        p1 = compressedLeftEdge[^1];
        d1 = p1 - compressedLeftEdge[^2];
        p2 = compressedRightEdge[^1];
        d2 = p2 - compressedRightEdge[^2];
        var bottomEdge = GetRoundedEdge(p1, d1, p2, d2, 3);

        // flip two segments to concat with proper winding order
        compressedRightEdge.Reverse();
        topEdge.Reverse();


        return compressedLeftEdge.Concat(bottomEdge).Concat(compressedRightEdge).Concat(topEdge).ToList();

    }

    public List<Vector2> GetRoundedEdge(Vector2 point1, Vector2 direction1, Vector2 point2, Vector2 direction2, float maxRoundingHeight)
    {
        var points = new List<Vector2>();


        var gapVector_1 = point2 - point1;
        var gapVector_2 = gapVector_1 * -1;

        var points_1 = GetRoundedSide(point1, direction1, gapVector_1, maxRoundingHeight);
        var points_2 = GetRoundedSide(point2, direction2, gapVector_2, maxRoundingHeight);

        // a points handle direction is the vector *to* the point *from* the opposite edge point
        var curveHandleDir_1 = points_1[^1] - points_2[^1];
        var curveHandleDir_2 = curveHandleDir_1 * -1;

        // handle position is where the points incoming direction vector meets the handle direction vector
        var curvePos1 = Geometry2D.LineIntersectsLine(points_1[^1], curveHandleDir_1, point1, direction1);
        var curvePos2 = Geometry2D.LineIntersectsLine(points_2[^1], curveHandleDir_2, point2, direction2);


        var curve = new Curve2D();
        curve.AddPoint(points_1[0]);
        curve.AddPoint(points_1[1], (Vector2)curvePos1 - points_1[1]);
        curve.AddPoint(points_2[1], null, (Vector2)curvePos2 - points_2[1]);
        curve.AddPoint(points_2[0]);

        return curve.Tessellate(4, 8).ToList();
        
    }

    public List<Vector2> GetRoundedSide(Vector2 point, Vector2 direction, Vector2 gapVector, float maxRoundingHeight)
    {
        var points = new List<Vector2>();
        var halfGap = gapVector.Length() / 2;
        var leftAngle = direction.AngleTo(gapVector);
        var rotatePercentage = 0.5f;

        var maxPointDist = float.Abs(halfGap / (float.Cos(leftAngle) + float.Cos(leftAngle * (1 - rotatePercentage))));

        maxPointDist *= 0.5f;
        maxPointDist = float.Min(maxPointDist, maxRoundingHeight);

        // first point extends in continued direction of point1,
        // second point attaches to end of first, but rotates to make up half the angle difference to the gap vector
        var point_add = direction.Normalized() * maxPointDist + point;
        var point_add_2 = direction.Normalized().Rotated(leftAngle * rotatePercentage) * maxPointDist + point_add;

      
        points.AddRange([point_add, point_add_2]);
        return points;
    }


    public List<Vector2> CompressNoiseLine(Vector2[] noiseLine)
    {
        var newLine = new List<Vector2>();
        var avgVec = noiseLine[^1] - noiseLine[0];
        for (int i = 0; i < noiseLine.Length; i++)
        {
            var p = noiseLine[i];
            var proj = (p - noiseLine[0]).Project(avgVec);
            var progress = proj.Length() / avgVec.Length();
            var progressToCenter = float.Min(progress, 1 - progress) * 2;  // 0 at either end, 1 in the center
            var projWeight = float.Pow(1 - progressToCenter, 3);
            if (projWeight > 0.95) projWeight = 1;
            newLine.Add(p.Lerp((proj + noiseLine[0]), projWeight));
        }

        return newLine;

    }

    public List<Vector2> GetNoiseLine(Vector2 line)
    {
        var edgeLine = DistortMap.GetNextHeights(line.Length(), true);
        // edgeLine.RemoveAt(1);
        // edgeLine.RemoveAt(edgeLine.Count - 2);

        var rotateAngle = Vector2.Right.AngleTo(line);
        edgeLine = edgeLine.Select(p => p.Rotated(rotateAngle)).ToList();

        return edgeLine;
    }



    public Vector2[] SmoothNoiseLine(List<Vector2> noiseLine, Vector2 bottomTarget, Vector2 topTarget)
    {
        var smoothCurve = new Curve2D();
        var smoothingFactor = 1.25f;
        var points = noiseLine;
        smoothCurve.AddPoint(points[0]);


        for (int i = 1; i < points.Count - 1; i++)
        {

            Vector2 p = points[i];
            Vector2 last = i == 0 ? bottomTarget : points[i - 1];
            Vector2 next = i == points.Count - 1 ? topTarget : points[i + 1];

            var vecLast = p - last;
            var vecNext = next - p;

            var shortLength = Math.Min(vecNext.Length(), vecLast.Length());


            var handleDir = (vecLast + vecNext).Normalized();
            // if (i == 0) handleDir = vecLast.Normalized();
            // if (i == points.Count - 1) handleDir = vecNext.Normalized();

            // var vecAvg = ((vecLast ) + (vecNext ) ) / 2;

            var handleNextLength = vecNext.Length() * 0.5f * smoothingFactor;
            var handleLastLength = vecLast.Length() * 0.5f * smoothingFactor;



            var handleOut = handleDir * (float)handleNextLength;
            var handleIn = handleDir * -1 * (float)handleLastLength;
            smoothCurve.AddPoint(p, handleIn, handleOut);
        }

        smoothCurve.AddPoint(points[^1]);
        var curvePoints = smoothCurve.Tessellate(4, 8);
        return curvePoints;



    }
}
