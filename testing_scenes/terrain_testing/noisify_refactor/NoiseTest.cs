using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class NoiseTest : Node2D
{

    HeightMap DistortMap;
    Line2D line;
    Polygon2D poly;
    Vector2 noiseLinePosition = new Vector2(0, -10);

    public override void _Ready()
    {
        line = GetNode<Line2D>("Line2D");
        poly = GetNode<Polygon2D>("Polygon2D");

        SetDistortMap();
        // height, base, top
        var polyList = GenerateNoiseEdgePoly(8, 10, 0);
        // line.Points = polyList.ToArray();
        poly.Polygon = polyList.ToArray();

        Polygon2D node = poly.GetChild(0) as Polygon2D;
        node.Polygon = poly.Polygon;




    }


    public void SetDistortMap()
    {

        var distortFreq = 0.08f;
        var distortLayers = 3;
        var layerFrequencyMult = 2f;
        var layerStrengthMult = 0.55f;
        DistortMap = new((int)GD.Randi(), distortFreq, distortLayers, layerFrequencyMult, layerStrengthMult);
        DistortMap.MaxHeight = 3f;
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

        line.Points = compressedLeftEdge.ToArray();
        GetNode<Line2D>("Line2D2").Points = compressedRightEdge.ToArray();

        compressedRightEdge.Reverse();


        return compressedLeftEdge.Concat(compressedRightEdge).ToList();

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

    public void GenerateConnectingTopper(List<Vector2> lineLeft, List<Vector2> lineRight)
    {

    }

}
