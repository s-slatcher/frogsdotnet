using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Dynamic;
using System.Linq;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class NoiseTest : Node2D
{

    HeightMap DistortMap;
    Line2D line;
    Polygon2D poly;
    Vector2 noiseLinePosition = new Vector2(0, -10);

    float nextXStart = 0;

    public override void _Ready()
    {
        line = GetNode<Line2D>("Line2D");
        poly = GetNode<Polygon2D>("Polygon2D");

        SetDistortMap();
        // height, base, top


        for (int i = 0; i < 50; i++)
        {
            var poly = GenerateNoiseEdgePoly(GD.RandRange(20, 50), GD.RandRange(1, 30), GD.RandRange(1, 30));
            var polyInstance = new Polygon2D();
            polyInstance.Polygon = poly.ToArray();
            AddChild(polyInstance);
            polyInstance.Translate(new Vector2(nextXStart, 0));
            nextXStart += 45;
        }

        // var polyList = GenerateNoiseEdgePoly(30, 0, 30);
        // // line.Points = polyList.ToArray();
        // poly.Polygon = polyList.ToArray();

        // Polygon2D node = poly.GetChild(0) as Polygon2D;
        // node.Polygon = poly.Polygon;




    }


    public void SetDistortMap()
    {

        var distortFreq = 0.08f;
        var distortLayers = 3;
        var layerFrequencyMult = 2f;
        var layerStrengthMult = 0.55f;
        DistortMap = new((int)GD.Randi(), distortFreq, distortLayers, layerFrequencyMult, layerStrengthMult);
        DistortMap.MaxHeight = 6f;
        DistortMap.MinHeight = 0;

    }

    public List<Vector2> GenerateNoiseEdgePoly(float height, float baseWidth, float topWidth)
    {


        var footPoint = new Vector2(baseWidth / 2, 0);

        var bottomLeftPoint = Vector2.Zero;
        var bottomRightPoint = new Vector2(baseWidth, 0);
        var topLeftPoint = footPoint + new Vector2(-topWidth / 2, height);
        var topRightPoint = footPoint + new Vector2(topWidth / 2, height);

        // left vector from bottom-left to top-left
        var leftVector = topLeftPoint;
        var sideLength = leftVector.Length();

        // grab noise line, both rotated the same to be mirrored
        var leftEdgeLine = GetNoiseLine(leftVector);
        var rightEdgeLine = GetNoiseLine(leftVector);
        // flip the right edge and translate by base width
        // rightEdgeLine = rightEdgeLine.Select(p => p * new Vector2(-1, 1) + new Vector2(baseWidth, 0)).ToList();

        // smooth out the middle of the lines, ignoring final point 
        var smoothLeftEdge = SmoothNoiseLine(leftEdgeLine, rightEdgeLine[0], rightEdgeLine[^1]);
        var smoothRightEdge = SmoothNoiseLine(rightEdgeLine, leftEdgeLine[0], leftEdgeLine[^1]);

        // force both ends to converge onto the average slope of the noise line (maintains the first and last point positions, which converging on zero relative slope would not)
        var compressedLeftEdge = CompressNoiseLine(smoothLeftEdge);
        var compressedRightEdge = CompressNoiseLine(smoothRightEdge);

        compressedRightEdge = compressedRightEdge.Select(p => p * new Vector2(-1, 1) + new Vector2(baseWidth, 0)).ToList();

         // finding top and bottom gap
        // note: first element is lowest X 
        // need to flip and translate right edge first
        // since this is done before reversing, align first with first and last with last to generate
        var p1 = compressedLeftEdge[0];
        var d1 = p1 - compressedLeftEdge[1]; 
       
        var p2 = compressedRightEdge[0];
        var d2 = p2 - compressedRightEdge[1];

        var topEdge = GetRoundedEdge(p1, d1, p2, d2);


        p1 = compressedLeftEdge[^1];
        d1 = p1 - compressedLeftEdge[^2];

        p2 = compressedRightEdge[^1];
        d2 = p2 - compressedRightEdge[^2];

        var bottomEdge = GetRoundedEdge(p1, d1, p2, d2);


        line.Points = topEdge.ToArray();
        GetNode<Line2D>("Line2D2").Points = bottomEdge.ToArray();

        compressedRightEdge.Reverse();
        // bottomEdge.Reverse();
        topEdge.Reverse();


        return compressedLeftEdge.Concat(bottomEdge).Concat(compressedRightEdge).Concat(topEdge).ToList();

    }

    public List<Vector2> GetRoundedEdge(Vector2 point1, Vector2 direction1, Vector2 point2, Vector2 direction2)
    {
        var points = new List<Vector2>();


        var gapVector_1 = point2 - point1;
        var gapVector_2 = point1 - point2;

        var points_1 = GetRoundedSide(point1, direction1, gapVector_1);
        var points_2 = GetRoundedSide(point2, direction2, gapVector_2);


        points_2.Reverse();

        points.Add(point1);
        points.AddRange(points_1);
        points.AddRange(points_2);
        points.Add(point2);



        return points;
    }

    public List<Vector2> GetRoundedSide(Vector2 point, Vector2 direction, Vector2 gapVector)
    {
        var points = new List<Vector2>();
        var halfGap = gapVector.Length() / 2;
        var leftAngle = direction.AngleTo(gapVector);
        var rotatePercentage = 0.55f;
        
        var maxPointDist = float.Abs( halfGap / ( float.Cos(leftAngle) + float.Cos(leftAngle * (1 - rotatePercentage)) ));

        maxPointDist *= 0.5f;
        maxPointDist = float.Min(maxPointDist, 2);

        // first point extends in continued direction of point1, second point attaches to end but rotates to make up half the angle difference to the gap
        var point_add = direction.Normalized() * maxPointDist + point;
        var point_add_2 = direction.Normalized().Rotated(leftAngle * rotatePercentage) * maxPointDist + point_add;

        // get handle-in for second point on curve
        var handleDir = (gapVector * -1).Normalized();
        var handleLength = maxPointDist * 0.95f;
        var handle = handleDir * handleLength;

        var curve = new Curve2D();
        curve.AddPoint(point_add);
        curve.AddPoint(point_add_2, handle);
        return curve.Tessellate().ToList();

        // points.AddRange([point_add, point_add_2]);
        // return points;
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
            // projWeight = 0;
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
        var curvePoints = smoothCurve.Tessellate();
        return curvePoints;



    }

    public void DisplayNoiseLines(List<Vector2> noiseLine)
    {
        var line2d = new Line2D() { Points = noiseLine.ToArray(), Width = 0.5f };
        line2d.Position = noiseLinePosition;
        noiseLinePosition += new Vector2(noiseLine[^1].X + 5, 0);
        AddChild(line2d);
    }

 

}
