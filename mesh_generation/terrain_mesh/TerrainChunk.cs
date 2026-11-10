using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

public partial class TerrainChunk : Node3D
{
    PolygonMeshTool meshTool = new();
    
    [Export] ShaderMaterial material;
    [Export] MeshInstance3D meshInst;
   
    public int SubdivideLevels = 1;
    
    public int terrainDepth = 5;

    public float RoundingDistance = 1.5f;
    public float RoundingToleranceDegrees = 8;
    public float TargetSubdivideArea {get; set;} = 1;
    
    // set 
    public override void _Ready()
    {
        base._Ready();
    }


    public void SetPolygon(NormalPoly poly)
    {
        var floorFace = new Face();

        var width = poly.Rect.Size.X;
        var height = poly.Rect.Size.Y;  

        var points2d = new List<Vector2>()
        {
            new(0,0),
            new(width,0),
            new(width,terrainDepth),
            new(0,terrainDepth)
        };

        var rounded = GeometryUtils.RoundPolygon(points2d.ToArray(), RoundingDistance, RoundingToleranceDegrees);
        var points3d_bottom = rounded.Select(p => new Vector3(p.X, 0, p.Y));
        var points3d_top = rounded.Select(p => new Vector3(p.X, height, p.Y));

        var botFaces = FacesFromPointList(points3d_bottom.ToList());
        var topFaces = FacesFromPointList(points3d_top.ToList());
        
        //first of either face list has normal pointing up, the second is twin facing down
        //run connection between opposite normal faces to make produce the shape
        var topEdges = topFaces[0].EdgeList.ToList();
        var botEdges = botFaces[1].EdgeList.ToList();
        
        var center = botFaces[0].WeightedCenter;
        foreach (var edge in topEdges)
        {
            var center2d = new Vector2(center.X, center.Z);
            var vert = edge.EndVert;
            var vertPos2d = new Vector2(vert.Position.X, vert.Position.Z);
            var vec2d = vertPos2d - center2d;
            var ang = float.Atan2(vec2d.Y, vec2d.X);

            vert.UV = new Vector2((ang + float.Pi) / (2*float.Pi), 1f - (vert.Position.Y / height ));

        }
        foreach (var edge in botEdges)
        {
            var center2d = new Vector2(center.X, center.Z);
            var vert = edge.EndVert;
            var vertPos2d = new Vector2(vert.Position.X, vert.Position.Z);
            var vec2d = vertPos2d - center2d;
            var ang = float.Atan2(vec2d.Y, vec2d.X);

            vert.UV = new Vector2((ang + float.Pi) / (2*float.Pi), 1f - (vert.Position.Y / height ));

            
        }

        

       
        // reverse and shift edge lists to connect edges starts-to-ends
        var firstTop = topEdges[0];
        topEdges.RemoveAt(0);
        topEdges.Add(firstTop);
        botEdges.Reverse();
        
        for (int i = 0; i < botEdges.Count; i++)
        {
            var botEdge = botEdges[i];
            var topEdge = topEdges[i];
            meshTool.ConnectEdges(botEdge, topEdge, false);
        }
        // resolve only after all connections made t
        meshTool.ResolveFaces();
        

        // should produce two faces -- the front and back of the mesh
        meshTool.SubdivideMesh(TargetSubdivideArea);
        var mesh = meshTool.GetMesh();
        meshInst.Mesh = mesh;
        meshInst.MaterialOverride = material;

        // TestAreaAndCenter();

     
    }
    
    public void TestAreaAndCenter()
    {
        var faces = meshTool.GetFaces();
        foreach (var face in faces )
        {
            var pos = face.WeightedCenter;
            var area = face.Area;
            var sphere = new SphereMesh();
            sphere.Radius = area / 100;
            sphere.Height = sphere.Radius * 2; 
            var inst = new MeshInstance3D
            {
                Mesh = sphere,
                Position = pos
            };
            AddChild(inst);

        }
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



}
