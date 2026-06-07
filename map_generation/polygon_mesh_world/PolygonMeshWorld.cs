using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Numerics;
using Vector2 = Godot.Vector2; 
using Vector3 = Godot.Vector3;

public partial class PolygonMeshWorld : Node3D
{
    [Export] int Seed = 1;
    [Export] float LandWidth = 120;
    [Export] float LandHeight = 80;
    [Export] int TotalLandmasses = 2;
    [Export] float PolygonDetail = 0.05f; // lower means higher detail (smaller differences allowed before collapsing)
    [Export] float AverageLandGap = 100;

    [Export] PackedScene explodeScene;
    [Export] PackedScene terrainMeshScene;
    TerrainMesh terrainMesh;

    GeometryUtils gUtils = new();
    RandomNumberGenerator rng = new();

    Vector2 radiusRange = new Vector2(2,7);

    Vector2 lastClick = Vector2.Zero;

    Dictionary<Rect2, TerrainMesh> TerrainRegionMap = new();



    public override void _Ready()
    {

        rng.Seed = (uint)Seed;

        PlaneMouseCapture planeCap = GetNode<PlaneMouseCapture>("PlaneMouseCapture");
        planeCap.PlaneClicked += OnPlaneClicked;

        GenerateTerrain();
        // var compareMeshInst = GetNode<MeshInstance3D>("HeightCompareMesh");
        // var compareMesh = (QuadMesh)compareMeshInst.Mesh;
        // compareMesh.Size = new Vector2( (LandWidth + AverageLandGap) * TotalLandmasses , LandHeight);
        // var halfSize = compareMesh.Size / 2;
        // compareMeshInst.Position = new Vector3(halfSize.X, halfSize.Y, -5);

        
        
    }

    private void GenerateTerrain()
    {
        // var gUtils = new GeometryUtils();


        var mapGenerator = GetNode<MapGenerator>("MapGenerator");
        List<NormalPoly> polygons = mapGenerator.GetPolygons();
        GD.Print(polygons.Count);


        // how old loop works:
        // - generate list of polygons with world-aligned point positions
        // - find the corner of that polygon, so the mesh node can be world aligned
        // - terrain mesh class passes polygon to polymesh where THERE it is normalized and given margin

            
            // foreach ( var poly in polygons)
            // {
            //     var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            //     terrainMesh.MinDepth = 3;
            //     var terrainRect = gUtils.RectFromPolygon(poly);

            //     terrainMesh.Position = new Vector3(terrainRect.Position.X  , terrainRect.Position.Y, 0);
            //     TerrainRegionMap.Add(terrainRect, terrainMesh);
            //     AddChild(terrainMesh);
            //     GD.Print(poly.Length);
            //     terrainMesh.TerrainPolygon = poly;
                
            
        // }

        // new system passes a normalized poly from MapGenerator, so all parties receive same poly with same bounding rect;
        // in future, can refactor so mapGenerator itself is generator normalPolys from step 1

        foreach (NormalPoly poly in polygons)
        {
            var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            AddChild(terrainMesh);
            terrainMesh.MinDepth = 3;

            TerrainRegionMap.Add(poly.Rect, terrainMesh);

            terrainMesh.TerrainPolygon = poly;


        }  
      


        

    }
    

 

    private void OnPlaneClicked(Vector3 vector)
    {

        var randRadius = (float)GD.RandRange(radiusRange.X, radiusRange.Y);
        var center2d = new Vector2(vector.X, vector.Y);
        
        var explodeRect = new Rect2(Vector2.Zero, new Godot.Vector2(randRadius * 2, randRadius * 2));
        explodeRect.Position = center2d - new Vector2(randRadius, randRadius);
        var print_string = "explosion intersect at: ";

        foreach (var rect in TerrainRegionMap.Keys)
        {
            var mesh = TerrainRegionMap[rect];
            var newRect = new Rect2(rect.Position + new Vector2(mesh.Position.X, mesh.Position.Y), rect.Size);
            GD.Print(rect.Position);
            if (explodeRect.Intersects(rect))
            {
                print_string += vector.ToString() + ",";
                mesh.ExplodeTerrain( vector, randRadius);
            }

        }
        GD.Print(print_string);


        var explosion = (Node3D)explodeScene.Instantiate();
        explosion.Position = vector + new Vector3(0, 0, 1);
        explosion.Scale = new Vector3(1, 1, 1) * randRadius;

        AddChild(explosion);

    }


    public override void _Process(double delta)
    {
        // float dt = (float)delta;
        // var randPos = new Vector3(GD.RandRange(0, 150), GD.RandRange(0, 100), 0);
        // var radiusAdd = (1.0 - randPos.Y / 100) * 5;
        // radiusAdd = float.Clamp((float)radiusAdd, 1, 10/3);
        // terrainMesh.ExplodeTerrain(
        //     randPos, (float)GD.RandRange(1 * radiusAdd, 3 * radiusAdd)
        // );
    }
    
   


}
