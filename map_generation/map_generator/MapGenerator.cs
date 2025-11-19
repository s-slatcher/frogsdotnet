using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public partial class MapGenerator : Node2D
{

    // will need to again split up these settings files, since height map rects and edge noising will be separate now
    [Export] public MapSettings MapSettings = new();
    [Export] public TerrainPolygonMapSettings TerrainPolySettings = new();
    HeightmapRects heightMap = new();
    GeometryUtils gu = new();
    RandomNumberGenerator rng = new();

    int maxIterations = 10;

    public override void _Ready()
    {
        GenerateTerrainMap();
        
        
        rng.Seed = (uint)TerrainPolySettings.HeightMapNoise.Seed;
    }

    public void SpawnIslands(List<LineSegment> spawnSourceLines, List<List<Vector2>> totalLand, int iterations = 0)
    {
        // steps of function ():
        // b4 callings: turn the basic set of landmasses into spawn lines and passes that list and the landmasses
        // add all lengths of spawnSourceLines together

       
        // loop once to sum length (pass it in instead?) and find a random percentage of that length
        var spawnLinesSum = 0f;
        foreach (var line in spawnSourceLines) spawnLinesSum += line.End.X - line.Start.X;
        var targetWidth = rng.Randf() * spawnLinesSum;
        
        // second loop to find which line, and at which point on that line, the randomized progress percentage sits
        var targetPos = new Vector2();
        var targetLine = new LineSegment(new Vector2(), new Vector2());
        var currentProg = 0f;
        foreach(var line in spawnSourceLines)
        {
            var widthRemaining = targetWidth - currentProg;
            var lineStart = line.Start;
            if (line.Start.X + widthRemaining < line.End.X)
            {
                targetPos = lineStart + new Vector2(widthRemaining, 0);
                targetLine = line;
                break;
            }
            currentProg += line.Length();
            
        }

        // find a randomized point some distance from that point on the line
        // validate that it can spawn at least a min sized island
        var newSpawnCenter = new Vector2();

        bool pointIsValid = false;
        var loopLimit = 20;
        var loops = 0;
        var islandRect = new Rect2();
        while (!pointIsValid && loops < loopLimit)
        {
            loops++;
            var maxDist = 50f;
            var randDist = (float)Math.Pow(rng.Randf(),2) * maxDist;
            var randAngle = float.Pi * 2 * rng.Randf();
            var randPos = (Vector2.Right * randDist).Rotated(randAngle) + targetPos;

            var maxRectSize = MaxNonIntersectingRectSize(randPos, new Vector2(10,10), new Vector2(40, 30), totalLand);
            
            if (maxRectSize == Vector2.Zero) continue;
            else
            {
                pointIsValid = true;
                newSpawnCenter = randPos;
                islandRect = gu.RectFromCenterPoint(newSpawnCenter, maxRectSize);

            }
        }

        if (pointIsValid)
        {
            var newLandPoly = gu.PolygonFromRect(islandRect);
            var spawnLineEnd = islandRect.End;
            var spawnLineStart = new Vector2(islandRect.Position.X, islandRect.End.Y);
            spawnSourceLines.Add(new LineSegment(spawnLineEnd, spawnLineStart));
            totalLand.Add(newLandPoly.ToList());

        }

        // find max allowed rect size for that spot

    }

    public Vector2 MaxNonIntersectingRectSize(Vector2 centerPoint, Vector2 minSize, Vector2 maxSize, List<List<Vector2>> totalLandmasses )
    {

        var sizeSteps = 5;
        var maximizedSize = Vector2.Zero;
        for (int i = 0; i < sizeSteps; i++)
        {
            var rectSize = ( (i+1) / sizeSteps ) * (maxSize-minSize) + minSize;
            var rect = gu.RectFromCenterPoint(centerPoint, rectSize);
            bool intersects = RectIntersectsLandmasses(totalLandmasses, rect);
            if (intersects) break;
            else maximizedSize = rectSize;
        }
        return maximizedSize;
    }

    public bool RectIntersectsLandmasses(List<List<Vector2>> landmasses, Rect2 rect)
    {
        var rectPoints = gu.PolygonFromRect(rect);
        foreach(var land in landmasses)
        {
            foreach(var p in rectPoints)
            {   
                if (Geometry2D.IsPointInPolygon(p, land.ToArray()))
                {
                    return true;
                }

            }
        }

        return false;


    }

    public void GenerateTerrainMap()
    {
        SetHeightMap();

        // pre-lim steps:
        // 1. get a full set of height map rects for the entire width
        // 2. split into islands (minor breaks at low points and major breaks at division levels ) 
        // 3. convert each group into a simplified map shape (cliff edges and cave insets)
        // 4. use simply polygons to find placements for complex islands (random sampling )
        
        var edgeNoise = new NoiseEdgePolygon();
        var terrainPolygon = new TerrainPolygonMap();
        terrainPolygon.Settings = TerrainPolySettings;
        edgeNoise.Settings = TerrainPolySettings; 

        // get platforms and separate them at major division points (will be realigned later using rects)
        var mapRange = new Vector2(0, MapSettings.Width);
        var rects = heightMap.GetRects(mapRange);
        var landGroups = GroupHeightMapRects(rects);
        

        var polygonGroups = landGroups.Select(l => terrainPolygon.GetSimpleTerrainPoly(l).ToList()).ToList();
        // for

        
        List<LineSegment> initSpawnLines = new();
        foreach (var poly in polygonGroups)
        {
            var rect = gu.RectFromPolygon(poly.ToArray());
            var spawnLineEnd = rect.End;
            var spawnLineStart = new Vector2(rect.Position.X, rect.End.Y);
            initSpawnLines.Add(new LineSegment(spawnLineEnd, spawnLineStart));
        }
        
        GD.Print("pre island count: ", polygonGroups.Count);
        SpawnIslands(initSpawnLines, polygonGroups);
        GD.Print("post island count: ", polygonGroups.Count);


        // var simplePolyRects = polygonGroups.Select(gu.RectFromPolygon);
        // var landGroupsNoisy = polygonGroups.Select(edgeNoise.ApplyNoiseEdge);
        

        



        // var startX = 0f;
        // for (int i = 0; i < landGroupsNoisy.Count(); i++)
        // {
        //     var land = landGroupsNoisy.ElementAt(i);
        //     var rect = simplePolyRects.ElementAt(i);
        //     var poly2d = new Polygon2D(){Polygon = land};
        //     poly2d.Position = new Vector2(startX, 0);
        //     var line = gu.Line2DFromPolygon(land, 0.5f, Colors.Red);
        //     line.Position = poly2d.Position;            
        //     startX += rect.Size.X + 30;

        //     AddChild(line);
        //     AddChild(poly2d);
        // }

        
        

        
    }

  
    
    private List<List<Rect2>> GroupHeightMapRects(List<Rect2> rects)
    {
        var landGroups = new List<List<Rect2>>() { new() { rects[0] } };
        var currGroupWidth = rects[0].Size.X;

        var groupTargetWidth = MapSettings.Width / MapSettings.MajorDivisions;
        // var groupTargetWidth = 10000;

        for (int i = 1; i < rects.Count; i++)
        {
            var rect = rects[i];
            var rectSize = rect.Size.X;
            if (currGroupWidth + (rectSize / 2) > groupTargetWidth)
            {
                landGroups.Add(new() { rect });
                currGroupWidth = rectSize;
            }
            else
            {
                landGroups[^1].Add(rect);
                currGroupWidth += rectSize;                
            }
        }

        return landGroups;
        
    } 

    private void SetHeightMap()
    {
        heightMap.Noise = TerrainPolySettings.HeightMapNoise;
        heightMap.Epsilon = TerrainPolySettings.SmoothingEpsilon;
        heightMap.Height = TerrainPolySettings.MaxHeight;
        heightMap.Jaggedness = TerrainPolySettings.Jaggedness;
    }

}
