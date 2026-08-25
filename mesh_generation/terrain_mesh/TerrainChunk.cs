using Godot;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
[Tool]
public partial class TerrainChunk : Node3D
{
    PolygonMeshTool meshTool = new();
    NormalPoly normPoly;
    [Export] ShaderMaterial material;
    [Export] MeshInstance3D meshInst;

    // rules for defining stretch
    // init width and change over height defined by polygon coming in 
    // init depth and change over height defined by start height and min/max depths 
    public float MaxHeight = 60;
    public bool DoStretchMesh = true;
    public int SubdivideLevels = 1;
    public float depthAtMaxHeight = 1;
    public float depthAtBase = 3;
   

    public int terrainDepth = 5;
    
    // set 
    public override void _Ready()
    {
        base._Ready();
    }


    public void SetPolygon(NormalPoly poly)
    {
        normPoly = poly;
        // default to 1 extrude, override later
        meshTool.SetPolygon(normPoly);

        if (DoStretchMesh) SetMeshDepth();

        meshTool.SubdivideAll(SubdivideLevels);
        var mesh = meshTool.GetMesh();
        meshInst.Mesh = mesh;
        meshInst.MaterialOverride = material;
    }


    float DepthAtHeight(float h, float maxH, float maxD, float minD)
    {
        var rat = Math.Clamp(h / maxH, 0, 1);
        return (1-rat) * (maxD - minD) + minD;

    }
    public void SetShaderParameters()
    {
        
    }

    public void SetMeshDepth()
    {
        meshTool.ExtrudeMesh(1);

        var aabb = meshTool.GetAabb();
        var cen = aabb.GetCenter();
    
        var meshMaxHeight = aabb.Size.Y;
        var ratio = meshMaxHeight / MaxHeight;
        var adjRatio = float.Pow(ratio, 2f);
        var adjHeight = MaxHeight * adjRatio;
        var meshMinDepth = DepthAtHeight(adjHeight, MaxHeight, depthAtBase, depthAtMaxHeight);
        
        // multiply depth at point by factor based on

        var vertSet = meshTool.GetFaces().SelectMany(f => f.GetVerts()).ToHashSet();
        foreach (var vert in vertSet)
        {
            var pos = vert.Position;
            var h = pos.Y;
            var depth = DepthAtHeight(h, meshMaxHeight, depthAtBase, meshMinDepth);
            var normPos = pos - cen;
            normPos.Z = (depth / 2) * Math.Sign(normPos.Z); 
            vert.Position = normPos + cen;

        }
    }

}
