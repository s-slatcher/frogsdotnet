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
    [Export] float LandWidth = 20;

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
    }

    private void GenerateTerrain()
    {
        var landmassGen = new LandmassTerrainPolyGenerator((int)rng.Seed);
        landmassGen.Height = 120;


        // simplify poly (takes 250 points down to <100) without much visual change
        var poly = landmassGen.GenerateTerrainPoly(0, LandWidth);
        var gUtils = new GeometryUtils();
        var simplePoly = gUtils.SimplifyPolygon(poly, 0.05f);
        GD.Print("landmass poly point count: ", poly.Length);
        GD.Print("count after simplifying: ", simplePoly.Length);
        poly = simplePoly;

        var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
        terrainMesh.QuadDensity = 0.50f; //USUALLY 0.5f
        terrainMesh.MinDepth = 3f;
        AddChild(terrainMesh);
        terrainMesh.TerrainPolygon = poly;

        TerrainRegionMap[GeometryUtils.RectFromPolygon(poly)] = terrainMesh;
        
    }
 

    private void OnPlaneClicked(Vector3 vector)
    {

        var randRadius = (float)GD.RandRange(radiusRange.X, radiusRange.Y);
        var center2d = new Vector2(vector.X, vector.Y);
        GD.Print("clicked at: ", center2d);

        var explodeRect = new Rect2(Vector2.Zero, new Godot.Vector2(randRadius * 2, randRadius * 2));
        explodeRect.Position = center2d - new Vector2(randRadius, randRadius);

        foreach (var rect in TerrainRegionMap.Keys)
        {
            var mesh = TerrainRegionMap[rect];
            var newRect = new Rect2(rect.Position + new Vector2(mesh.Position.X, mesh.Position.Y), rect.Size);

            if (explodeRect.Intersects(newRect))
            {
                mesh.ExplodeTerrain(vector, randRadius);
            }

        }



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
