using Godot;
using System;

public partial class TerrainChunk : Node3D
{
    PolygonMeshTool meshTool = new();
    NormalPoly normPoly;
    [Export] ShaderMaterial material;

    public int terrainDepth = 5;
    
    // set 
    public override void _Ready()
    {
        base._Ready();
    }


    public void SetPolygon(NormalPoly poly)
    {
        normPoly = poly;
        meshTool.SetPolygon(normPoly, 5);
    }

    public void SetShaderParameters()
    {
        
    }

}
