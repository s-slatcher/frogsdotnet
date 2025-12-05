using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;

public partial class MapGenerator : Node2D
{

    // will need to again split up these settings files, since height map rects and edge noising will be separate now
    [Export] public MapSettings MapSettings = new();
    [Export] public TerrainPolygonMapSettings IslandPolySettings = new();
    [Export] public TerrainPolygonMapSettings TerrainPolySettings = new();
    
    [Export] public bool DrawDebugDrawings = false;

    HeightmapRects heightMap = new();


    TerrainPolygonMap terrainPolygonMap = new();
    TerrainPolygonMap islandPolygonMap = new();
    
    GeometryUtils gu = new();
    RandomNumberGenerator rng = new();

    List<(Rect2, Godot.Color)> DebugRectDrawList = new();
    List<Vector2[]> DebugPolyList = new();

    List<Vector2[]> SimpleLandmasses = new();
    List<Vector2[]> NoisyLandmasses = new();
    Dictionary<Vector2[], Rect2> LandmassRectMap = new();


    const float targetTotalIslandWidth = 120;
    float maxIslandWidth = 35;
    float minIslandWidth = 10;    

    public PhysicsDirectSpaceState2D Space { get; private set; }

    private void SetHeightMap(TerrainPolygonMapSettings settings)
    {
        heightMap.Noise = settings.HeightMapNoise;
        heightMap.Noise.Seed = settings.Seed;
        heightMap.Epsilon = settings.SmoothingEpsilon;
        heightMap.Height = settings.MaxHeight;
        heightMap.Jaggedness = settings.Jaggedness;
    }

    public override void _Ready()
    {
        Space = PhysicsServer2D.SpaceGetDirectState(GetWorld2D().Space);
        rng.Seed = (uint)TerrainPolySettings.Seed;
        
        terrainPolygonMap.Settings = TerrainPolySettings;
        islandPolygonMap.Settings = IslandPolySettings;
        GenerateTerrainMap();

        QueueRedraw();
    }

    public List<Vector2[]> GetPolygons()
    {
        return new(NoisyLandmasses){};
    }
    
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton eventMouseButton)
        {
            if (eventMouseButton.ButtonIndex != MouseButton.Left) return;
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
    
    public override void _Draw()
    {
        if (!DrawDebugDrawings) return;
        foreach (var drawRect in DebugRectDrawList)
        {
            DrawRect(drawRect.Item1, drawRect.Item2, false, 1);
        }

        foreach (var poly in DebugPolyList)
        {
            // var colorArray = new Godot.Color[poly.Length];
            // Array.Fill(colorArray, Godot.Colors.Black);
            
            DrawPolyline(poly, Colors.Red);
            DrawPolyline([poly[^1], poly[0]], Colors.Red);

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
        
        SetHeightMap(TerrainPolySettings);
        
        var mapRange = new Vector2(0, MapSettings.Width);
        var rects = heightMap.GetRects(mapRange);
        var landGroups = GroupHeightMapRects(rects);        
        var landPolygons = landGroups.Select(l => terrainPolygonMap.GetSimpleTerrainPoly(l).ToArray()).ToList();
        var landPolyRects = new List<Rect2>();
        float time = Time.GetTicksMsec();
        
        // translate polygons and create water gaps
        var lastEndPos = Vector2.Zero;
        var accumLandWidth = 0f;
        for (int i = 0; i < landPolygons.Count; i++)
        {
            var land = landPolygons[i];
            if (!Geometry2D.IsPolygonClockwise(land)) GD.Print("not clockwise");
            var rect = gu.RectFromPolygon(land);
            var translation = lastEndPos - rect.Position;
            landPolygons[i] = gu.TranslatePolygon(land, translation);
            rect.Position = lastEndPos;

            SimpleLandmasses.Add(landPolygons[i]);            
            LandmassRectMap[landPolygons[i]] = rect;

            accumLandWidth += rect.Size.X;

            // add polygon to collision
            
            var colShapes = PhysicsShapesFromConcave(landPolygons[i]); 
            
            AddAreaToSpace(colShapes, new());
            // var colShape = PhysicsShapeFromPolygon(reversed);
            
           

            var waterGap = 30f;
            lastEndPos = rect.Position + new Vector2(rect.Size.X + waterGap, 0);

            // AddRectToDrawList(rect);
        }
        var spawnRects = landPolyRects.Select(GetSpawnZoneFromTerrainRect).ToList();

        

        SpawnIslands(accumLandWidth, targetTotalIslandWidth + accumLandWidth);
        GD.Print(Time.GetTicksMsec() - time, "ms for island rect gen");

        var noisePolyApplier = new NoiseEdgePolygon();
        noisePolyApplier.Settings = TerrainPolySettings;
        
        foreach (var landPoly in SimpleLandmasses)
        {
            
            var noisePoly = noisePolyApplier.ApplyNoiseEdge(landPoly);
            NoisyLandmasses.Add(noisePoly);
            AddPolyToDrawList(noisePoly);


        }

    }

   


    private void SpawnIslands(float accumLandWidth, float targetLandWidth)
    {
        // pick random spawn rect weighted by width
        var spawnWidthTarget = rng.Randf() * accumLandWidth;
        Rect2 spawnRect = new();
        var w = 0f;
        for (int i = 0; i < SimpleLandmasses.Count; i++)
        {
            var land = SimpleLandmasses[i];
            var rect = LandmassRectMap[land];
            w += rect.Size.X; 
            if (w > spawnWidthTarget)
            {
                spawnRect = GetSpawnZoneFromTerrainRect(rect);
                break;
            }
        }

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
            
            var aspectRatio = rng.RandfRange(0.6f,0.85f); //randomly range from square to double wide
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
        var islandPoly = IslandLandmassFromRect(islandRect);
        
        SimpleLandmasses.Add(islandPoly);
        LandmassRectMap[islandPoly] = islandRect;
        
        // dont allow overly small islands to be sources of new island spawns
        
        
        
        AddRectToDrawList(islandRect);

        

        var shape = PhysicsShapeFromRectangle(islandRect.Size);
        AddAreaToSpace(shape, spawnPoint);

        accumLandWidth += islandRect.Size.X;
        if (accumLandWidth > targetLandWidth) return;

        SpawnIslands(accumLandWidth, targetLandWidth);
    }

    private Vector2[] IslandLandmassFromRect(Rect2 rect)
    {

        // shrink rect to add buffer between landmasses
        rect = gu.RectFromCenterPoint(rect.Size/2 + rect.Position, rect.Size * 0.9f);

        // split height across max height and bottom slope extension
        var halfHeight = rect.Size.Y / 2;
        var width = rect.Size.X; // final poly width ends up wider than this value; shrunk down to this size
        
        IslandPolySettings.MaxHeight = halfHeight;
        IslandPolySettings.BottomPointHeight = halfHeight;
        IslandPolySettings.BottomPlatformWidth = 3;

        SetHeightMap(IslandPolySettings);
        var heightRects = heightMap.GetRects(new Vector2(rect.Position.X, rect.End.X));
        var simplePoly = islandPolygonMap.GetSimpleTerrainPoly(heightRects);
        var simpleRect = gu.RectFromPolygon(simplePoly);

        var scale = rect.Size / simpleRect.Size; 
        simplePoly = gu.TranslatePolygon(simplePoly, -simpleRect.Position);
        simplePoly = gu.ScalePolygon(simplePoly, scale);
        var translation = rect.Position;
        var translatedPoly = gu.TranslatePolygon(simplePoly, translation);

        return translatedPoly;
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

    
 
    private List<Rid> PhysicsShapesFromConcave(Vector2[] poly)
    {
        var convexPolys = Geometry2D.DecomposePolygonInConvex(poly.ToArray());
        var shapes = new List<Rid>();
        foreach(var conPol in convexPolys)
        { 
            var shape = PhysicsServer2D.ConvexPolygonShapeCreate();
            PhysicsServer2D.ShapeSetData(shape, conPol);
            shapes.Add(shape);

        }
       
        return shapes;
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
    public void AddAreaToSpace(List<Rid> shapes, List<Vector2> shapeOrigins)
    {
        for (int i = 0; i < shapes.Count; i++)
        {
            AddAreaToSpace(shapes[i], shapeOrigins.Count == shapes.Count ? shapeOrigins[0] : Vector2.Zero);
        }
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

        var query = new PhysicsPointQueryParameters2D()
        {
            Position = point,
            CollideWithAreas = true
        };
        return Space.IntersectPoint(query, 1).Count != 0;
    }

  

}
