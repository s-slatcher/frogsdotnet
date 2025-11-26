using Godot;
using Godot.NativeInterop;
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

    List<(Rect2, Godot.Color)> DebugRectDrawList = new();
    List<Vector2[]> DebugPolyList = new();

    const float targetTotalIslandWidth = 100;
    float maxIslandWidth = 30;
    float minIslandWidth = 5;    

    public PhysicsDirectSpaceState2D Space { get; private set; }


    public override void _Ready()
    {
        Space = PhysicsServer2D.SpaceGetDirectState(GetWorld2D().Space);

        GenerateTerrainMap();
        rng.Seed = (uint)TerrainPolySettings.HeightMapNoise.Seed;

        QueueRedraw();
    }
    public override void _Draw()
    {
        foreach (var drawRect in DebugRectDrawList)
        {
            DrawRect(drawRect.Item1, drawRect.Item2, false, 1);
        }

        foreach (var poly in DebugPolyList)
        {
            // var colorArray = new Godot.Color[poly.Length];
            // Array.Fill(colorArray, Godot.Colors.Black);
            DrawPolyline(poly, Colors.Red);

        }

    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton eventMouseButton)
        {
            
            var camNode = GetNode<DebugCamera2d>("DebugCamera2d");
            var pos = ToLocal(camNode.ToGlobal(camNode.GetLocalMousePosition()));
            var query = new PhysicsPointQueryParameters2D()
            {
                Position = pos,
                CollideWithAreas = true,
            };
            var drawRect = gu.RectFromCenterPoint(pos, new Vector2(4,4));
            if (Space.IntersectPoint(query).Count > 0)
            {
                AddRectToDrawList(drawRect, Colors.Red);
            }
            else AddRectToDrawList(drawRect, Colors.Green);
    
            QueueRedraw();

        }
    }

    public void AddRectToDrawList(Rect2 rect, Godot.Color? color = null)
    {
        if (color == null) color = Colors.Black;
        DebugRectDrawList.Add((rect, (Godot.Color)color));
    } 
    
    private void AddPolyToDrawList(Vector2[] polygon)
    {
        DebugPolyList.Add(polygon);
    }

    

    // notes on new spawn islands function
    // 1. create dedicated "spawn zone" function that takes a rect, grabs its top line, and creates another rect around it 
    //
    // 2. then pass to a funtion to sample randomly inside that rect
    //
    // 3. pass along spawn zones, (for now) just pick a random spawn zone above a specific width
    // 4. track total landmass area and stop after target
    // 5. 

    public Rect2 GetSpawnZoneFromTerrainRect(Rect2 terrain)
    {
        var spawnRect = new Rect2(terrain.Position + new Vector2(0, terrain.Size.Y), new Vector2(terrain.Size.X, 0));
        spawnRect = spawnRect.GrowIndividual(15, 15, 15, 15);
       // eventually add clamping to a min and max range to top and side spawn distaces
        return spawnRect;

    }

    public Vector2 GetRandomPointInSpawnRect(Rect2 SpawnRect)
    {
        var size = SpawnRect.Size;
        var pos = SpawnRect.Position;
        var x = rng.Randf() * size.X + pos.X;
        var y = rng.Randf() * size.Y + pos.Y;
        return new Vector2(x,y);
    }
   
    public void GenerateTerrainMap()
    {
        SetHeightMap();

        // var edgeNoise = new NoiseEdgePolygon();
        var terrainPolygon = new TerrainPolygonMap();
        terrainPolygon.Settings = TerrainPolySettings;
        // edgeNoise.Settings = TerrainPolySettings; 

        // get platforms and separate them at major division points (will be realigned later using rects)
        
        var mapRange = new Vector2(0, MapSettings.Width);
        var rects = heightMap.GetRects(mapRange);
        var landGroups = GroupHeightMapRects(rects);        
        var landPolygons = landGroups.Select(l => terrainPolygon.GetSimpleTerrainPoly(l).ToArray()).ToList();
        var landPolyRects = new List<Rect2>();
        float time = Time.GetTicksMsec();
        
        // translate polygons and create water gaps
        var lastEndPos = Vector2.Zero;
        for (int i = 0; i < landPolygons.Count; i++)
        {
            var land = landPolygons[i];
            if (!Geometry2D.IsPolygonClockwise(land)) GD.Print("not clockwise");
            var rect = gu.RectFromPolygon(land);
            var translation = lastEndPos - rect.Position;
            landPolygons[i] = gu.TranslatePolygon(land, translation);
            rect.Position = lastEndPos;
            landPolyRects.Add(rect);
            

            // add polygon to collision
            var convexPolys = Geometry2D.DecomposePolygonInConvex(landPolygons[i].ToArray());
            foreach (var poly in convexPolys)
            {
                var colShape = PhysicsShapeFromConvex(poly); 
                AddAreaToSpace(colShape, Vector2.Zero);
            }
            // var colShape = PhysicsShapeFromPolygon(reversed);
            AddPolyToDrawList(landPolygons[i]);

           

            var waterGap = 30f;
            lastEndPos = rect.Position + new Vector2(rect.Size.X + waterGap, 0);

            // AddRectToDrawList(rect);
        }
        var spawnRects = landPolyRects.Select(GetSpawnZoneFromTerrainRect).ToList();

        

        SpawnIslands(landPolygons, spawnRects, 0);
        GD.Print(Time.GetTicksMsec() - time, "ms for island rect gen");

    }

   


    private void SpawnIslands(List<Vector2[]> lands, List<Rect2> spawningRects, float accumLandWidth, float targetLandWidth = targetTotalIslandWidth)
    {
        // pick random spawn rect
        var spawnRect = spawningRects[rng.RandiRange(0, spawningRects.Count-1)];

        // get valid point:
        // after a non-intersecting point is chosen, binary search looks for max size rect to fit in area
        // if area too tight, loop repeats and new point chosen
        // if this outer loop fails X num of times (shoud not often fail) no island is chosen and recursion ends
        Rect2 islandRect = new();
        Vector2 spawnPoint;
        int rectLoops = 0;
        do
        {
            spawnPoint = GetRandomPointInSpawnRect(spawnRect);
            if (IsPointColliding(spawnPoint)) 
            {
                continue;
            }  // initial check for point collision before building rect;
            
            var aspectRatio = rng.RandfRange(0.5f,1); //randomly range from square to double wide
            var minSize = new Vector2(minIslandWidth, minIslandWidth * aspectRatio);
            var maxSize = new Vector2(maxIslandWidth, maxIslandWidth * aspectRatio);
            
            Rid rectShape;
            var steps = 0; var stepLimit = 6; 
            while(steps++ < stepLimit)
            {
                var midSize = minSize + (maxSize - minSize) / 2;
            
                rectShape = PhysicsShapeFromRectangle(midSize);
                bool isCollide = IsShapeColliding(rectShape, spawnPoint);

                if (isCollide) maxSize = midSize;
                else minSize = midSize;

            }

            // if minsize is too close to its original value after step limit reached, assume no valid rect found
            // else treat min size as near maximum possible rect size
            if (minSize.X - minIslandWidth < 2) continue; // start over with new spawn point
            else
            {
                islandRect = gu.RectFromCenterPoint(spawnPoint, minSize);
                break;
            }


        } while (rectLoops++ < 100);
        
        // no valid rect found, end recursion
        if  (islandRect.Size == Vector2.Zero) return;

        // convert island rect into an island poly
        // add the land and the spawning rect to lists, and add to accumulated width
        // recurse with updated list if not enough width accumlated
        var islandPoly = gu.PolygonFromRect(islandRect);
        lands.Add(islandPoly);
        
        // dont allow overly small islands to be sources of new island spawns
        if (islandRect.Size.X > 0.5*maxIslandWidth)
        {
           spawningRects.Add(GetSpawnZoneFromTerrainRect(islandRect)); 
        }
        
        
        
        AddRectToDrawList(islandRect);

        

        var shape = PhysicsShapeFromRectangle(islandRect.Size);
        AddAreaToSpace(shape, spawnPoint);

        accumLandWidth += islandRect.Size.X;
        if (accumLandWidth > targetLandWidth) return;

        SpawnIslands(lands, spawningRects, accumLandWidth, targetLandWidth);
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
    // public Rid PhysicsShapeFromPolygon(Vector2[] polygon)
    // {
    //     var shape = PhysicsServer2D.ConcavePolygonShapeCreate();
        
    //     // convert polygon into format for ShapeSetData (in sets of 2 forming segments)
    //     List<Vector2> pointSegmentList = new();
    //     var segments = gu.LineSegmentsFromPolygon(polygon);
    //     foreach (var seg in segments)
    //     {
    //         pointSegmentList.AddRange([seg.Start, seg.End]);
    //     }
        
    //     PhysicsServer2D.ShapeSetData(shape, pointSegmentList.ToArray());
    //     return shape;
    // }
    private Rid PhysicsShapeFromConvex(Vector2[] poly)
    {
        var shape = PhysicsServer2D.ConvexPolygonShapeCreate();
        PhysicsServer2D.ShapeSetData(shape, poly);
        return shape;
    }
    public Rid PhysicsShapeFromRectangle(Vector2 size)
    {
        var shape = PhysicsServer2D.RectangleShapeCreate();
        PhysicsServer2D.ShapeSetData(shape, size/2);
        return shape;
    }
    public void AddAreaToSpace(Rid shape, Vector2 origin)
    {
        var area = PhysicsServer2D.AreaCreate();
        var transform = Transform2D.Identity;
        transform.Origin = origin;
        PhysicsServer2D.AreaAddShape(area, shape);
        
        PhysicsServer2D.AreaSetTransform(area, transform);

		PhysicsServer2D.AreaSetSpace(area, GetWorld2D().Space);
        
    }
    public void RemoveAreaFromSpace(Rid area)
    {
        PhysicsServer2D.AreaSetSpace(area, new Rid());
    }
    public bool IsShapeColliding(Rid shape, Vector2 shapeOrigin)
    {
        var trans = Transform2D.Identity;
        trans.Origin = shapeOrigin;
        var query = new PhysicsShapeQueryParameters2D()
        {
            ShapeRid = shape,
            Transform = trans,
            CollideWithAreas = true,

        };
        
        return Space.GetRestInfo(query).Count != 0;

    }
    public bool IsPointColliding(Vector2 point)
    {
        // var rayEnd = new Vector2(-50, point.Y);
        // var rayQuery = PhysicsRayQueryParameters2D.Create(point, rayEnd );  // make sure ray end is beyond map bounds 
        // rayQuery.CollideWithAreas = true;
        // rayQuery.HitFromInside = true;

        // var result = Space.IntersectRay(rayQuery);
        // if (result.Count > 0)
        // {
        //     var norm = (Vector2)result["normal"];
            
        //     if (norm == Vector2.Zero)
        //     {
        //         GD.Print("from inside");
        //         return true;
        //     }
        //     else return false;
        // }

        var query = new PhysicsPointQueryParameters2D()
        {
            Position = point,
            CollideWithAreas = true
        };
        return Space.IntersectPoint(query, 1).Count != 0;
    }

  

}
