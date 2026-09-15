using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Drawing;
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
    
    public float BevelDistance = 0.5f;

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
        BevelEdges();
        meshTool.SubdivideAll(SubdivideLevels);
        var mesh = meshTool.GetMesh();
        meshInst.Mesh = mesh;
        meshInst.MaterialOverride = material;
    }

    public void SetPolygon_TEST(NormalPoly poly)
    {
        var floorFace = new Face();

        var width = poly.Rect.Size.X;
        var depth = 5;
        var height = poly.Rect.Size.Y;  

        var points2d = new List<Vector2>()
        {
            new(0,0),
            new(width,0),
            new(width,depth),
            new(0,depth)
        };

        var rounded = GeometryUtils.RoundPolygon(points2d.ToArray(), 1, 3);
        var points3d_bottom = rounded.Select(p => new Vector3(p.X, 0, p.Y));
        var points3d_top = rounded.Select(p => new Vector3(p.X, height, p.Y));

        var botFaces = FacesFromPointList(points3d_bottom.ToList());
        var topFaces = FacesFromPointList(points3d_top.ToList());

        // first face in both lists should point upwards
        // GD.Print(PolygonMeshTool.GetNormal(topFaces[0].EdgeList[0]));
        // GD.Print(PolygonMeshTool.GetNormal(topFaces[1].EdgeList[0]));
        
        // combine edges from the tops-downward face, and the bottoms upward face
        var topEdges = topFaces[0].EdgeList.ToList();
        var botEdges = botFaces[1].EdgeList.ToList();
       
        // reverse and shift edge lists to connect faces starts-to-ends
        var firstTop = topEdges[0];
        topEdges.RemoveAt(0);
        topEdges.Add(firstTop);
        

        botEdges.Reverse();



        
        for (int i = 0; i < botEdges.Count; i++)
        {
            var botEdge = botEdges[i];

            var topEdge = topEdges[i];

            // GD.Print(botEdge.Face, topEdge.Face);
            
            meshTool.ConnectEdges(botEdge, topEdge, false);
            
        }

        // meshTool.unfacedEdges.AddRange(topEdges);
        meshTool.ResolveFaces();
        

        // should produce two faces -- the front and back of the mesh
        meshTool.SubdivideAll(SubdivideLevels);
        var mesh = meshTool.GetMesh();
        meshInst.Mesh = mesh;
        meshInst.MaterialOverride = material;

     
    }

    List<Face> FacesFromPointList(List<Vector3> points)
    {

        var initEdge = meshTool.GetStandaloneEdge(points[0], points[1]);
        var lastEdge = initEdge;
        
        for (int i = 2; i < points.Count; i++)
        {
            var nextP = points[i];
            lastEdge = meshTool.CreateNextEdge(lastEdge, nextP);
        }

        meshTool.ConnectEdges(lastEdge, initEdge);

        Debug.Assert(lastEdge.NextEdge == initEdge.PrevEdge);
        Debug.Assert(initEdge.TwinEdge.NextEdge == lastEdge.TwinEdge.PrevEdge);
        
        return [initEdge.Face, initEdge.TwinEdge.Face];
    }


    float DepthAtHeight(float h, float maxH, float maxD, float minD)
    {
        var rat = Math.Clamp(h / maxH, 0, 1);
        return (1-rat) * (maxD - minD) + minD;

    }

    public void BevelEdges()
    {
        // find max bevel dist (min extrude depth / 2)
        float bevelDist = BevelDistance;
        int bevelFaces = 3;

        // get set of viable edges for beveling
        HashSet<HalfEdge> bevelEdges = new();
        foreach (var face in meshTool.GetFaces())
        {   
            foreach (var hEdge in face.EdgeList)
            {
                var edgeVec = (hEdge.EndVert.Position - hEdge.StartVert.Position).Normalized();
                if (edgeVec.Y > 0.5 && hEdge.HasTwin())
                {
                    bevelEdges.Add(hEdge);
                }

            }
        }

        GD.Print(bevelEdges.Count," edges found to bevel");
        meshTool.BevelEdge_new(bevelEdges.ToList()[0], bevelDist, bevelFaces);
        // foreach(var hEdge in bevelEdges) meshTool.BevelEdge_new(hEdge, bevelDist, bevelFaces);
    }

    
    public void SetShaderParameters()
    {
        
    }

    public void SetMeshDepth()
    {
        meshTool.Extrude(3);

        return; 

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
