using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;

public partial class TerrainMesh : Node3D
{

    [Export] TerrainTexture terrainTexture;
    [Export] PolygonMesh polyMesh;


    public enum TerrainType
    {
        Landmass,
        FloatingIsland,
    }

    public float MaxDepth = 5;
    public float MinDepth = 5;
    public float QuadDensity = 0.5f;
    public float GrassLength = 2f;

    private Vector2[] terrainPoly = [];
    public Vector2[] TerrainPolygon
    {
        get { return terrainPoly; }
        set
        {
            terrainPoly = value;
            if (IsNodeReady()) SetMesh();
            else GD.PrintErr("Add terrain mesh to scene BEFORE assigning a polygon");
        }
    }

    public override void _Ready()
    {
        if (terrainPoly.Length != 0) SetMesh();  
    }


    private void SetMesh()
    {

        // if (!IsNodeReady()) GD.PrintErr("called generate before in scene tree");



        // SetDomainDepthCurve(terrainPoly);
        // SetHeightDepthCurve(terrainPoly);
        // GD.Print(terrainTexture);

        
        terrainTexture.SetPolygon(terrainPoly, GrassLength);
        var grass_texture = terrainTexture.GetTexture();
        var shader = (ShaderMaterial)polyMesh.MaterialOverride;
        shader.SetShaderParameter("grass_texture", grass_texture);


        polyMesh.QuadDensity = QuadDensity;
        polyMesh.MinDepth = MinDepth;
        polyMesh.GenerateMesh(terrainPoly);

        BuildGrass();
    }

    private void BuildGrass()
    {
        var time = Time.GetTicksMsec();
        MultiMeshInstance3D multiMeshInst = GetNode<MultiMeshInstance3D>("MultiMeshInstance3D");
        // var multiMesh = new MultiMesh();
        // multiMesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
        // var grassMeshScene = (PackedScene)GD.Load("uid://bqxbvm5ahkldx");

        // var meshInst = grassMeshScene.Instantiate<MeshInstance3D>();
        // var mat = meshInst.MaterialOverride;

        var multiMesh = (MultiMesh)multiMeshInst.Multimesh.Duplicate();
        multiMeshInst.Multimesh = multiMesh;     
           
        
        // multiMeshInst.Multimesh = multiMesh;
        // multiMeshInst.MaterialOverride = mat;

        List<Transform3D> grass_positions = new();

        var vertices = polyMesh.Mesh.GetFaces();
        GD.Print("face count from loop: ", vertices.Length / 3);
        var qualified_face_count = 0;

        for (int i = 0; i < vertices.Length; i += 3)
        {
            var vert1 = (Vector3)vertices[i];
            var vert2 = (Vector3)vertices[i + 1];
            var vert3 = (Vector3)vertices[i + 2];


            var face_norm = (vert1-vert2).Cross(vert2-vert3).Normalized() * -1;

            if (!(face_norm.Y > 0.6)) continue;
            if (GD.Randf() < 0.75) continue;
            qualified_face_count += 1;

            Vector3 centroid = (vert1 + vert2 + vert3) / 3;
            Vector3 randOffet = new Vector3(GD.Randf(), 0, GD.Randf()) / 5;
            Vector3 grass_pos = centroid + randOffet + new Vector3(0,-0.1f,0);

            var transform = new Transform3D(Basis.Identity, grass_pos);
            grass_positions.Add(transform);


        }
        GD.Print("grass faces: ", qualified_face_count, " loop time: ", Time.GetTicksMsec() - time);

        multiMeshInst.Multimesh.InstanceCount = qualified_face_count;
        for (int i = 0; i < qualified_face_count; i++)
        {
            multiMeshInst.Multimesh.SetInstanceTransform(i, grass_positions[i]);

        }

        // AddChild(multiMeshInst);
    }
        


    // public void SetHeightDepthCurve(TerrainPolygon terrainPoly)
    // {

    //     polyMesh.HeightDepthCurve = GD.Load<Curve>("uid://dnobssupgeuds");

    //     var c = new Curve();
    //     var maxHeight = terrainPoly.BoundingRect.End.Y;

    //     // c.MinDomain = 0;
    //     // c.MaxDomain = 1;
    //     // c.MinValue = MinDepth;
    //     // c.MaxValue = MaxDepth;

    //     // highest depth value aligned with lowest height
    //     var startPoint = new Vector2(0, 1);
    //     var endPoint = new Vector2(1, 0);

    //     c.AddPoint(startPoint);
    //     c.AddPoint(endPoint);
    //     c.BakeResolution = 500;
    //     polyMesh.HeightDepthCurve = c;
    // }

    // public void SetDomainDepthCurve(TerrainPolygon terrainPoly)
    // {
    //     if (terrainPoly.SimplifiedHeightCurve == null || terrainPoly.SimplifiedHeightCurve.PointCount == 0)
    //     {
    //         polyMesh.DomainDepthCurve = null;
    //         return;
    //     }

    //     var baseCurve = terrainPoly.SimplifiedHeightCurve;
    //     var width = terrainPoly.BoundingRect.Size.X;
    //     var c = new Curve();

    //     // c.MinDomain = 0;
    //     // c.MaxDomain = baseCurve.MaxDomain;
    //     // c.MinValue = 0;
    //     // c.MaxValue = 1;


    //     var baseCurvePoints = new List<Vector2>();
    //     for (int i = 0; i < baseCurve.PointCount; i++) baseCurvePoints.Add(baseCurve.GetPointPosition(i));

    //     var newPoints = baseCurvePoints
    //         .Select(p => new Vector2(p.X / width, float.Sqrt(p.Y / baseCurve.MaxValue))).ToList();

    //     var pF = newPoints[0];
    //     var pL = newPoints[^1];

    //     var midValue = float.Sqrt(0.35f);

    //     // conditional tapering on edges of curve 
    //     var startValue = pF.Y > midValue ? float.Max(midValue, pF.Y * 0.5f) : pF.Y;
    //     var endValue = pL.Y > midValue ? float.Max(midValue, pL.Y * 0.5f) : pL.Y;

    //     newPoints.Insert(0, new Vector2(0, startValue));
    //     newPoints.Add(new Vector2(c.MaxDomain, endValue));

    //     foreach (var p in newPoints) c.AddPoint(p);
    //     c.BakeResolution = 500;
    //     polyMesh.DomainDepthCurve = c;
    // }


    public void ExplodeTerrain(Vector3 position, float radius)
    {
        polyMesh.ExplodeTerrain(position, radius);
    }
    



}
