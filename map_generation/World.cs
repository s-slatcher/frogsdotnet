using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;



public partial class World : Node3D
{

    [Export] ShaderMaterial meshMaterial;
    

    private bool meshFinished = false;
    private double meshTime = 0;
    private Task task;
    private Thread thread;
    private Dictionary<Task<Mesh>, MeshInstance3D> meshGenerationTasks = new();

    public int MeshLodIndex = 0;
    public List<MeshInstance3D> MeshInstances = new();
    public List<MeshInstance3D> WireMeshInstances = new();
    public List<ExtrudedMesh> MeshGenerators = new();

    

    public TerrainMap terrain;

    private Vector3 nextMeshPosition = new Vector3(0, 0, 0);

    public override void _Ready()
    {

        terrain = new();
        terrain.MaxHeight = 80;
        terrain.MinHeight = 35;

        // thread = new(GenerateMap);
        // thread.Start();

        GenerateMap_v2();
    }

    public override void _Process(double delta)
    {
        return;
       
    }

    public override void _PhysicsProcess(double delta)
    {
        return;
    }

    private void GenerateMap_v2()
    {

        var width = 200f;
        List<Polygon2D> MapPolygonInstances = terrain.GenerateNext(width);
        var mapPoly = MapPolygonInstances[0];
        var tex = GetEdgeTexture(mapPoly.Polygon);


        var quadMesh = new PolygonQuadMesh(mapPoly.Polygon);
        QuadMeshDistortionApplier quadMeshDistortionApplier = new(quadMesh);

        var time = Time.GetTicksMsec();
        quadMeshDistortionApplier.AddMeshDistorter(new EdgeWrapDistorter(1, 2));

        
        // quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(10, 20), 15));
       
        GD.Print("edge wrap distort time: ", Time.GetTicksMsec() - time);
        time = Time.GetTicksMsec();

        quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(25, 25), 5));
        quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(33, 25), 8));
        quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(15, 35), 12));
        
        GD.Print(" explosions distort time: ", Time.GetTicksMsec() - time);
        

        var distortedQuadMesh = quadMeshDistortionApplier.QuadMeshHistory[^1];

        // var polygons = distortedQuadMesh.GetPolygons();
        // GD.Print(polygons.Count);
        // foreach (Vector2[] poly in polygons)
        // {
        //     var poly2d = new Polygon2D() { Polygon = poly };
        //     poly2d.SelfModulate = new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1);
        //     AddChild(poly2d);
        // }

        List<Mesh> meshList = distortedQuadMesh.GenerateMeshes();
        
        
        
        // meshList = distorter.QuadMesh.GenerateMeshes();
        foreach (Mesh mesh in meshList)
        {
            var meshInstance = GetNode<MeshInstance3D>("container").Duplicate() as MeshInstance3D;
            meshInstance.Mesh = mesh;
            var material = meshInstance.MaterialOverride.Duplicate() as ShaderMaterial;
            material.SetShaderParameter("texture_edge", tex);
            meshInstance.MaterialOverride = material;
            AddChild(meshInstance);

        }


    }


    ImageTexture GetEdgeTexture(Vector2[] polygon)
    {
        var edgeTextureGenerator = new EdgeTextureGenerator();
        edgeTextureGenerator.Polygon = polygon;
        edgeTextureGenerator.edgeDistanceLimit = 6;
        edgeTextureGenerator.edgeBuffer = 2;
        Image image = edgeTextureGenerator.Generate();
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

}

