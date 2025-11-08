using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using Vector2 = Godot.Vector2;

public partial class NewNoiseEdgePolyTesting : Node2D
{
    Polygon2D polyInput;
    Polygon2D polyResult;
    Line2D outline;

    HeightmapRects heightMap = new();
    GeometryUtils gu = new();

    [Export(PropertyHint.Range, "0, 1, 0.1")] float UpdateSpeed = 0;

    [ExportGroup("Mountain Shape")]
    [Export] bool CycleHeightMapSeed = false;

    [Export] int HeightMapSeed = 0;
    [Export] float Width = 100;
    [Export] float MaxHeight = 60;
    [Export] float Jaggedness = 1;
    [Export(PropertyHint.Range, "0.001, 0.05, 0.001")] float SmoothingEpsilon = 0.02f;
    [Export] float MeanCliffGrade = float.Pi / 15;

    [ExportGroup("Edge Noise")]
    [Export] bool CycleEdgeNoiseSeed = false;
    [Export] float HeightCutoff = 0.5f;
    [Export] float MaxYComponent = 0.75f;
    [Export] float NoiseClampingFactor = 0.8f; // ~0.8 limits clamping but seems to prevent most noise line overlaps
    [Export] float NoiseFrequency = 0.08f;
    [Export] int EdgeSeed = 0;
    [Export] float TargetCurveBuffer = 1;  // set as low as possible
    [Export] float TargetNoiseHeight = 3f;

    [ExportGroup("Caves")]
    [Export] float MinCaveHeight = 8;
    [Export] float MaxCaveHeight = 15;
    [Export] float MinHeightAllowance = 40;
    [Export] float BaseCaveChance = 1f;
    [Export] float MinCaveDepth = 5;
    [Export] float MaxCaveDepth = 10;
    
    

    

    [Export] bool DrawLines = false;

    RandomNumberGenerator rng = new();

    double timeSinceUpdate = 0;
    float NoiseStart = 0;

    FastNoiseLite EdgeNoise = GD.Load<FastNoiseLite>("uid://dw8jx8pbm8eps");

    public override void _PhysicsProcess(double delta)
    {
        timeSinceUpdate += delta;
        var deltaTarget = (1.05 - UpdateSpeed);

        if (timeSinceUpdate > deltaTarget && UpdateSpeed != 0)
        {
            timeSinceUpdate = 0;

            Update();
        }

    }
    
    private void Update()
    {
        UpdateNoise();

        var poly = GenerateLandmassPoly();
        var noisePoly = GenerateSmoothNoisePoly(poly.ToArray());


        polyResult.Polygon = noisePoly.ToArray();

        // checks if polygon has self intersection without visualizing
        
        // var tris = Geometry2D.TriangulatePolygon(polyResult.Polygon);
    
        outline.ClearPoints();
        

        // draws outlines
        if (!DrawLines) return;
        foreach (var p in noisePoly)
        {
            outline.AddPoint(p);
        }
    }

    public override void _Ready()
    {
        polyInput = GetNode<Polygon2D>("P");
        polyResult = GetNode<Polygon2D>("P2");
        outline = GetNode<Line2D>("Outline");

    }

    private void UpdateNoise()
    {

        if (CycleEdgeNoiseSeed) EdgeSeed += 1;
        rng.Seed = (uint)EdgeSeed;
        if (CycleHeightMapSeed) HeightMapSeed += 1;
        // noise for setting terrain shape
        heightMap.Height = MaxHeight;
        heightMap.NoiseSeed = HeightMapSeed;
        heightMap.Range = new Vector2(0, Width);
        heightMap.Jaggedness = Jaggedness;
        heightMap.Epsilon = SmoothingEpsilon;    
        
        // noise for distorted edges of terrain
        EdgeNoise.Frequency = NoiseFrequency;
        EdgeNoise.Seed = EdgeSeed;
        NoiseStart = 0;
    }

    private LineSegment TranslatePlatform(LineSegment platform, LineSegment lastPlatform)
    {
        var heightDelta = platform.Start.Y - lastPlatform.End.Y;
        var cliffLength = heightDelta / Math.Cos(MeanCliffGrade);
        var baseWidth = (float)Math.Sqrt(cliffLength * cliffLength - heightDelta * heightDelta);
        var newPlatStart = lastPlatform.End + new Vector2(baseWidth, heightDelta);

        return new LineSegment(newPlatStart, newPlatStart + platform.DirectionVector());
    }

    private List<Vector2> GenerateLandmassPoly()
    {


        // get height rects, add dummy at end to complete 
        var heightRects = heightMap.GetRects();
        heightRects.Add(new(new Vector2(0, 0), new Vector2(1, 0)));
        
        
        // convert rects to line segments;
        var platformLines = heightRects.Select(rect => new LineSegment(rect.End - new Vector2(rect.Size.X, 0), rect.End)).ToList();

        var landPoly = new List<Vector2>();


        // set initial platform position, and final rect
        var initPlatform = new LineSegment(Vector2.Zero, Vector2.Zero);
        var lastPlatform = initPlatform;

        
        foreach (var node in GetChildren())
        {
            if (node is Line2D && node.Name != "Outline") node.QueueFree();
        }

        for (int i = 0; i < heightRects.Count; i++)
        {
            
            // step 1: get the platform and translate it to meet previous at an angle.
            var platform = platformLines[i];
            platform = TranslatePlatform(platform, lastPlatform);
            platformLines[i] = platform;
            landPoly.Add(lastPlatform.End);
            //step 1: get height difference and check conditions for cave
            
            Vector2 cliffVector = platform.Start - lastPlatform.End;
            float heightDelta = cliffVector.Y;
            float absHeight = float.Abs(heightDelta);

            bool cliffSupportsCave = absHeight > MinHeightAllowance;

            if (cliffSupportsCave && rng.Randf() < BaseCaveChance)
            {

                // step 2: find cave height and floor position on cliff
                float caveHeight = rng.RandfRange(MinCaveHeight, MaxCaveHeight);
                float maxFloorPosition = (absHeight - caveHeight) / absHeight;  // limit how close cave ceiling to can be to top platform
                float caveStartProgress = float.Clamp(rng.Randfn(0.5f, 0.1f), 0, maxFloorPosition); ;  // cave floor averages in middle, usually extends to upper half of cliff 

                //step 3: establish cliff slope direction 
                Vector2 lowPos; Vector2 highPos;
                if (heightDelta > 0) (lowPos, highPos) = (lastPlatform.End, platform.Start);
                else (lowPos, highPos) = (platform.Start, lastPlatform.End);

                // set 4: set cave floor width and how far tucked into the mountain it is
                Vector2 caveCliffPosition = (highPos - lowPos) * caveStartProgress + lowPos;
                var caveDepth = rng.RandfRange(MinCaveDepth, MaxCaveDepth) * Math.Sign(heightDelta);
                var caveInsetPercentage = rng.RandfRange(0.25f, 0.5f);  // 0.5 means the middle of cave floor would intersect with cliff face line;
                // var caveInsetPercentage = 0.90f;
                var caveTranslate = (1 - caveInsetPercentage) * caveDepth;
                Vector2 caveEnd = caveCliffPosition + new Vector2(caveTranslate, 0);


                List<Vector2> cavePoints = [caveEnd, new Vector2(caveDepth, 0) + caveEnd];
                if (heightDelta < 0) cavePoints.Reverse();
                landPoly.AddRange(cavePoints);

            }
            
            landPoly.Add(platform.Start);
            

            


            // // step 2: establish slope direction and the two connecting points that might form a cave
            // float heightChange = platform.Start.Y - lastPlatform.End.Y;
            // var caveDir = 1;
            // LineSegment highPlat = platform;
            // LineSegment lowPlat = lastPlatform;
            // Vector2 caveStart = lowPlat.End;
            // Vector2 caveEnd = highPlat.Start;
            
            

            // // step 3: check conditions for building caves
            // if (i == 0 || i == heightRects.Count - 1 || Math.Abs(heightChange) < MinHeightAllowance)  // first and last edges are floor not landmass
            // {
            //     landPoly.AddRange([lastPlatform.End, platform.Start]);
            //     lastPlatform = platform;
            //     continue;
            // }

            // if (heightChange < 0)
            // {
            //     caveDir = -1;
            //     (highPlat, lowPlat) = (lowPlat, highPlat);
            //     (caveStart, caveEnd) = (lowPlat.Start, highPlat.End);
            // }

            // // step 4: find how deep the low plat will be extended to form floor of cave
            // // depth cant go beyond end of higher platform, nor can the it be longer than half the height difference 
            // Vector2 highPlatMiddle = (highPlat.Start + highPlat.End) / 2;
            // float caveMaxLength = float.Abs(caveStart.X - highPlatMiddle.X);
            // caveMaxLength = new List<float>() { caveMaxLength, (caveEnd.Y - caveStart.Y) }.Min();

            // GD.Print(caveMaxLength);
            // float caveExtension = float.Clamp(rng.RandfRange(caveMaxLength / 3, caveMaxLength), 0, MaxCaveDepth);
            // // if (caveExtension < 3) continue;
            // Vector2 caveCorner = new Vector2(caveExtension * caveDir, 0) + caveStart;

            // // step 4: find the height of the cave wall based on height max cave length
            // var caveWallHeight = rng.RandfRange(caveMaxLength / 3, caveMaxLength * 0.75f);
            // var randAngle = rng.RandfRange(0, 0.25f) * caveDir;
            // var caveWallX = float.Tan(randAngle) * caveWallHeight;
            // var caveWallEnd = new Vector2(caveWallX, caveWallHeight) + caveCorner;

            // // step 5: get a ceiling angle and connect it to original cliff wall
            // var randCeilingAngle = rng.RandfRange(0.25f, 0.5f) * caveDir * -1;
            // var ceilingLine = (Vector2.Left * caveDir).Rotated(randCeilingAngle);
            // var connectPoint = (Vector2)Geometry2D.LineIntersectsLine(caveWallEnd, ceilingLine, caveStart, caveEnd - caveStart);

            // var line = new Line2D();
            // List<Vector2> linePoints;
            // if (caveDir == 1) linePoints = [caveStart, caveCorner, caveWallEnd, connectPoint, platform.Start];
            // else linePoints = [lastPlatform.End, connectPoint, caveWallEnd, caveCorner, caveStart];

            // // landPoly.Add(lastPlatform.End);

            // landPoly.AddRange(linePoints);

            // foreach (var p in linePoints) line.AddPoint(p);
            // line.Width = 0.25f;
            // line.Modulate = Colors.Red;
            // line.Gradient = new Gradient();
            // AddChild(line);
            
            
            lastPlatform = platform;
        }

        // remove final point to correct the placeholder platform
        // landPoly.RemoveAt(landPoly.Count - 1);

        var cavedPoly = new List<Vector2>();

        var maxCaveHeight = 4;

       

        // for (int i = 0; i < platformLines.Count - 1; i++) // ignore platform at end
        // {
        //     var plat1 = platformLines[i];
        //     var plat2 = platformLines[i + 1];

            

        //     if (plat2.Start.Y - plat1.Start.Y < 1.5 * maxCaveHeight) continue;

        //     // pick a new plat1 end point at least a third distance to the next platforms end point
        //     var maxPlatExtension = (plat2.End.X - plat1.End.X ) / 2;
        //     var newPlatEnd = plat1.End + new Vector2(rng.RandfRange(maxPlatExtension / 3, maxPlatExtension), 0);

        //     var caveHeight = rng.RandfRange(2, maxCaveHeight);

        //     var caveBackWallTop = newPlatEnd + new Vector2(0, caveHeight);

        //     var caveCeilingEnd = (Vector2)Geometry2D.LineIntersectsLine(caveBackWallTop, Vector2.Left, plat1.End, (plat2.Start - plat1.End));

        //     var line = new Line2D();
        //     Vector2[] linePoints = [plat1.End, newPlatEnd, caveBackWallTop, caveCeilingEnd];
        //     foreach (var p in linePoints) line.AddPoint(p);
        //     line.Width = 0.25f;
        //     line.Modulate = Colors.Red;
        //     AddChild(line);
        // }

        return landPoly;
    }
    

    


    public List<Vector2> GenerateSmoothNoisePoly(Vector2[] inputPolygon)
    {
        var lineSegs = gu.LineSegmentsFromPolygon(inputPolygon);
        var lineCount = lineSegs.Count;
        var shortenedLines = new List<LineSegment>();
        var lineCurveLength = new List<float>();

        // get polygon as line segments
        // shorten each line and track the amount it was shortened by
        // ---
        for (int i = 0; i < lineCount; i++)
        {
            var seg = lineSegs[i];

            var curveBuffer = TargetCurveBuffer;
            if (TargetCurveBuffer * 2 > seg.Length())
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
            var noiseLine = GenerateNoiseLine__(line);
            noiseLines.Add(noiseLine);
            if (noiseLine.Count < 2) GD.Print("!!!!"); 
            // var line2d = new Line2D();
            // line2d.Gradient = new Gradient();
            // line2d.Width = 3;
            // line2d.Modulate = Colors.Red;

            // foreach (var p in noiseLine)
            // {
            //     line2d.AddPoint(p);
            // }

            // AddChild(line2d);

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

        return resultPolygon;

      
        
    }

    List<Vector2> GenerateNoiseLine__(LineSegment line)
    {
        float NoiseDistortHeight = TargetNoiseHeight;
        NoiseDistortHeight = float.Clamp(NoiseDistortHeight, 0, line.Length() / 6);

        if (line.GetNormal().Y > MaxYComponent || line.Start.Y < HeightCutoff || line.End.Y < HeightCutoff  ) NoiseDistortHeight = 0;


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
                var y = EdgeNoise.GetNoise1D(x + NoiseStart);
                var line_pos = lineDir * x + line.Start;

                var edgeCloseness = 1 - Math.Min(i, (sampleTotal - 1) - i) / sampleTotal;
                // shrink noise from full (1) to 0.5, ranging from 1 closeness to 0.9;
                var shrinkFactor = GeometryUtils.Remap(edgeCloseness, 0.75f, 1, 1, NoiseClampingFactor, true);

                var shrunkNoise = NoiseDistortHeight * shrinkFactor;

                line_pos += noiseDir * y * shrunkNoise;
                noisePoints.Add(line_pos);

            }
        }
        NoiseStart += line.Length() * 2;

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
