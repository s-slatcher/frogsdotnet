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

    RandomNumberGenerator rng = new();

    Vector2 radiusRange = new Vector2(2, 8);

    Vector2 lastClick = Vector2.Zero;

    Dictionary<Rect2, TerrainMesh> TerrainRegionMap = new();



    public override void _Ready()
    {

        rng.Seed = (uint)Seed;

        PlaneMouseCapture planeCap = GetNode<PlaneMouseCapture>("PlaneMouseCapture");
        planeCap.PlaneClicked += OnPlaneClicked;

        GenerateTerrain();
        var compareMeshInst = GetNode<MeshInstance3D>("HeightCompareMesh");
        var compareMesh = (QuadMesh)compareMeshInst.Mesh;
        compareMesh.Size = new Vector2( (LandWidth + AverageLandGap) * TotalLandmasses , LandHeight);
        var halfSize = compareMesh.Size / 2;
        compareMeshInst.Position = new Vector3(halfSize.X, halfSize.Y, -5);

        
    }

    private void GenerateTerrain()
    {
        var gUtils = new GeometryUtils();

        var landmassGen = new LandmassTerrainPolyGenerator((int)rng.Seed);


        var terrainMap = new TerrainPolygonMap();
        terrainMap.Settings.MaxHeight = LandHeight;
        terrainMap.Settings.EdgeNoise.Seed = Seed;
        terrainMap.Settings.HeightMapNoise.Seed = Seed;

        landmassGen.Height = LandHeight;
        landmassGen.Jaggedness = 2;


        // simplify poly (takes 250 points down to <100) without much visual change

        Vector2 nextRange = new Vector2(0, LandWidth);


        for (int i = 0; i < TotalLandmasses; i++)
        {
            // var poly = landmassGen.GenerateTerrainPoly(nextRange.X, nextRange.Y);
            var poly = terrainMap.GetTerrainPolygon(nextRange);

            GD.Print("unsimplified poly point count: ", poly.Length);
            var simplePoly = gUtils.SimplifyPolygon(poly, PolygonDetail);
            poly = simplePoly;
            GD.Print("simplfied count: ", poly.Length);


            var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            terrainMesh.Position = new Vector3(nextRange.X, 0, 0);
            terrainMesh.QuadDensity = 0.5f; //NOT A MAP SETTING, PROBABLY GOES UNCHANGED?
            terrainMesh.MinDepth = 3f; // SAME?
            
            AddChild(terrainMesh);

            terrainMesh.TerrainPolygon = poly;
            TerrainRegionMap[GeometryUtils.RectFromPolygon(poly)] = terrainMesh;

            var waterGap = (float)rng.Randfn(AverageLandGap, 4);
            nextRange.X = nextRange.Y + waterGap;
            nextRange.Y = nextRange.X + LandWidth;
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

            if (explodeRect.Intersects(newRect))
            {
                print_string += vector.ToString() + ",";
                mesh.ExplodeTerrain(vector, randRadius);
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
