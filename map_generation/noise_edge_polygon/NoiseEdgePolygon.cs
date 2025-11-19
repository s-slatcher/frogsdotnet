using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using Vector2 = Godot.Vector2;



public partial class NoiseEdgePolygon : GodotObject
{

    public TerrainPolygonMapSettings Settings = new();
    public float NoiseStart = 0;

    public Vector2[] ApplyNoiseEdge(Vector2[] inputPolygon)
    {
        var gu = new GeometryUtils();
        var lineSegs = gu.LineSegmentsFromPolygon(inputPolygon.ToArray());
        var lineCount = lineSegs.Count;
        var shortenedLines = new List<LineSegment>();
        var lineCurveLength = new List<float>();

        // get polygon as line segments
        // shorten each line and track the amount it was shortened by
        // ---
        for (int i = 0; i < lineCount; i++)
        {
            var seg = lineSegs[i];

            var curveBuffer = Settings.TargetCurveBuffer;
            if (Settings.TargetCurveBuffer * 2 > seg.Length())
            {
                curveBuffer = seg.Length() * 0.95f * 0.5f; // leaves 10% of line length minimum
            }

            var dirVec = seg.DirectionVector().Normalized();
            var segStart = dirVec * curveBuffer + seg.Start;
            var segEnd = segStart + dirVec * (seg.Length() - (2 * curveBuffer));
            var shortSeg = new LineSegment(segStart, segEnd);

            shortenedLines.Add(shortSeg);
            lineCurveLength.Add(curveBuffer);
        }


        // loop each shortened line applying noise
        // ---
        var noiseLines = new List<List<Vector2>>();
        foreach (var line in shortenedLines)
        {
            var noiseLine = GenerateNoiseLine(line);
            noiseLines.Add(noiseLine);

        }

        // loop noise lines and apply curves connecting them
        // ---
        var resultPolygon = new List<Vector2>();

        for (int i = 0; i < noiseLines.Count; i++)
        {
            var line = noiseLines[i];
            var nextLineIdx = i + 1 < lineCount ? i + 1 : 0;
            var nextLine = noiseLines[nextLineIdx];

            var bufferDist = lineCurveLength[i];
            var nextBufferDist = lineCurveLength[nextLineIdx];


            var lineDir = line[^1] - line[0];
            var nextLineDir = nextLine[0] - nextLine[^1];



            // may need to check if handles cross over and prevent that
            var handleLenFactor = 0.75f; // handles extend this percentage of way to original lines length 
            var handleLength = Math.Min(bufferDist, nextBufferDist) * handleLenFactor;

            var handle = lineDir.Normalized() * handleLength;

            var nextHandle = nextLineDir.Normalized() * handleLength;

            var curve = new Curve2D();
            curve.AddPoint(line[^1], null, handle);
            curve.AddPoint(nextLine[0], nextHandle, null);

            var curvePoly = curve.Tessellate(4, 8).ToList();
            curvePoly.RemoveAt(0);
            curvePoly.RemoveAt(curvePoly.Count - 1);

            resultPolygon.AddRange(line);
            resultPolygon.AddRange(curvePoly);
        }

        return resultPolygon.ToArray();



    }

    List<Vector2> GenerateNoiseLine(LineSegment line)
    {
        float NoiseDistortHeight = Settings.TargetNoiseHeight;
        NoiseDistortHeight = float.Clamp(NoiseDistortHeight, 0, line.Length() / 6);

        if (line.GetNormal().Y > Settings.MaxYComponentForNoise
        || line.Start.Y < Settings.NoiseHeightCutoff
        || line.End.Y < Settings.NoiseHeightCutoff)
        {
            NoiseDistortHeight = 0;
        }

        var sampleDist = 0.5f;
        var noisePoints = new List<Vector2>();
        var lineDir = line.DirectionVector().Normalized();
        var noiseDir = line.GetNormal();
        var sampleTotal = line.Length() / sampleDist;

        // if line is too short, noise application breaks
        if (sampleTotal < 2)
        {
            noisePoints = new List<Vector2>() { line.Start, line.End };
        }
        else
        {

            for (int i = 0; i < sampleTotal; i++)
            {
                var x = i * sampleDist;
                var y = Settings.EdgeNoise.GetNoise1D(x + NoiseStart);
                var line_pos = lineDir * x + line.Start;

                var edgeCloseness = 1 - Math.Min(i, (sampleTotal - 1) - i) / sampleTotal;
                // shrink noise from full (1) to 0.5, ranging from 1 closeness to 0.9;
                var shrinkFactor = GeometryUtils.Remap(edgeCloseness, 0.75f, 1, 1, Settings.NoiseClampingFactor, true);

                var shrunkNoise = NoiseDistortHeight * shrinkFactor;

                line_pos += noiseDir * y * shrunkNoise;
                noisePoints.Add(line_pos);

            }
        }
        NoiseStart += line.Length() * 2;

        var gu = new GeometryUtils();
        var simpleLine = gu.SimplifyPolygon(noisePoints.ToArray(), 0.05f).ToList();
        // return simpleLine;
        return CompressNoiseLine(simpleLine);


    }

    public List<Vector2> CompressNoiseLine(List<Vector2> noiseLine)
    {
        var newLine = new List<Vector2>();
        var avgVec = noiseLine[^1] - noiseLine[0];

        for (int i = 0; i < noiseLine.Count; i++)
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
}
