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
    [Export] PackedScene terrainMeshScene;
    [Export] MapGenerator mapGenerator;

    [Export] DebugExplosionGenerator debugExplosionScene;
    TerrainMesh terrainMesh;

    GeometryUtils gUtils = new();

    Vector2 radiusRange = new Vector2(2,7);

    Vector2 lastClick = Vector2.Zero;

    List<TerrainMesh> TerrainRegionList = new();



    public override void _Ready()
    {
        // GenerateTerrain();
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
