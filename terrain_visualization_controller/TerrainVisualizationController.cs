using Godot;
using System;

public partial class TerrainVisualizationController : Node3D
{
    [Export] PackedScene terrainMeshScene;
    public void AddPolygon(NormalPoly terrainPolygon)
    {
       
        var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
        AddChild(terrainMesh);
        terrainMesh.MinDepth = 3;
        terrainMesh.TerrainPolygon = terrainPolygon;


    }

}
