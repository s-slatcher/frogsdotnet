using Godot;
using System;
using System.Collections.Generic;
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
        // BevelEdges();
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

    public void BevelEdges()
    {
        // find max bevel dist (min extrude depth / 2)
        float bevelDist = 0.5f;
        int bevelFaces = 3;

        // get set of viable edges for beveling
        HashSet<HalfEdge> bevelEdges = new();
        foreach (var face in meshTool.GetFaces())
        {   
            foreach (var hEdge in face.EdgeList)
            {
                var edgeVec = (hEdge.EndVert.Position - hEdge.StartVert.Position).Normalized();
                if (edgeVec.Y > 0.5 && hEdge.TwinEdge != null)
                {
                    bevelEdges.Add(hEdge);
                }

            }
        }

        GD.Print(bevelEdges.Count," edges found to bevel");
        foreach(var hEdge in bevelEdges) meshTool.BevelEdge(hEdge, bevelDist, bevelFaces);
    }

    
    public void SetShaderParameters()
    {
        
    }

    public void SetMeshDepth()
    {
        meshTool.Extrude(1);

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
