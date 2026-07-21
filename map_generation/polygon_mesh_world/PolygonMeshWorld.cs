using Godot;
using Godot.NativeInterop;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Numerics;
using System.Reflection;
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

    [Export] PackedScene terrainMeshScene;
    [Export] MapGenerator mapGenerator;

    [Export] DebugExplosionGenerator debugExplosionScene;
    TerrainMesh terrainMesh;

    GeometryUtils gUtils = new();
    RandomNumberGenerator rng = new();

    Vector2 radiusRange = new Vector2(2,7);

    Vector2 lastClick = Vector2.Zero;

    List<TerrainMesh> TerrainRegionList = new();



    public override void _Ready()
    {

        rng.Seed = (uint)Seed;


        GenerateTerrain();
        
        
        GD.Print("name of scene: ", Name);
        
    }

    private void GenerateTerrain()
    {
        // var gUtils = new GeometryUtils();

        List<NormalPoly> polygons = mapGenerator.GetPolygons();
        
        foreach (NormalPoly poly in polygons)
        {

            var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            AddChild(terrainMesh);
            terrainMesh.MinDepth = 3;

            terrainMesh.TerrainPolygon = poly;

            debugExplosionScene.AreaExploded += terrainMesh.OnAreaExploded;
            

        }  
      
    }
    
    
}
