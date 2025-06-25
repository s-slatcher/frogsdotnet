using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;



public partial class World : Node3D
{

    [Export] ShaderMaterial meshMaterial;

    public Task task = Task.CompletedTask;
    private List<Rect2> TaskedExplosionRegions = new(); 

    public Dictionary<Rect2, MeshInstance3D> MeshInstanceMap = new();
    public QuadMeshDistortionApplier quadMeshDistortionApplier;

    public TerrainMap terrain;
    GeometryUtils gUtils = new();
    private Vector3 nextMeshPosition = new Vector3(0, 0, 0);


    public override void _Ready()
    {

        terrain = new(100);
        terrain.MaxHeight = 80;
        terrain.MinHeight = 15;

        // thread = new(GenerateMap);
        // thread.Start();

        GenerateMap_v2();
        GetNode<Godot.Timer>("ExplodeTimer").Timeout += () => RandomExplosion();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (task.IsCompleted)
        {
            
            var quadMesh = quadMeshDistortionApplier.GetQuadMesh();
            foreach (Rect2 region in TaskedExplosionRegions)
            {
                var meshMap = quadMesh.GenerateMeshes(region);
                MeshInstanceMap[region].Mesh = meshMap[region];
            }
            TaskedExplosionRegions = new();

        }
    }



    public void RandomExplosion()
    {
        ExplodeTerrain(new Vector2(GD.Randf() * 50, GD.Randf() * 60), GD.RandRange(3, 10));
    }

    private void ExplodeTerrain(Vector2 center, float radius)
    {
        if (task.Status == TaskStatus.Running)
        {
            GD.Print("task still active");
            return;
        }

        task = new Task(() => quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(center, radius)));
        task.Start();

        // quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(center, radius));
        var quadMesh = quadMeshDistortionApplier.GetQuadMesh();

        TaskedExplosionRegions = MeshInstanceMap.Keys.Where(rect => gUtils.CircleOverlapsRect(rect, center, radius)).ToList();


    }

    private void GenerateMap_v2()
    {

        var width = 40f;
        List<Polygon2D> MapPolygonInstances = terrain.GenerateNext(width);
        var mapPoly = MapPolygonInstances[0];
        var tex = GetEdgeTexture(mapPoly.Polygon);
        var quadMesh = new PolygonQuadMesh(mapPoly.Polygon);
        quadMeshDistortionApplier = new(quadMesh);

        var time = Time.GetTicksMsec();
        quadMeshDistortionApplier.AddMeshDistorter(new BaseTerrainDistorter(16));
        quadMeshDistortionApplier.AddMeshDistorter(new EdgeWrapDistorter(1, 2));

        GD.Print("edge wrap distort time: ", Time.GetTicksMsec() - time);
        time = Time.GetTicksMsec();

        // quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(25, 25), 5));
        // quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(33, 25), 8));
        // quadMeshDistortionApplier.AddMeshDistorter(new ExplosionDistorter(new Vector2(15, 35), 12));

        GD.Print(" explosions distort time: ", Time.GetTicksMsec() - time);


        var distortedQuadMesh = quadMeshDistortionApplier.QuadMeshHistory[^1];

        // var polygons = distortedQuadMesh.GetPolygons();
        // GD.Print(polygons.Count);
        // var polyContainer = new Node2D();
        // polyContainer.RotationDegrees = 180;
        // polyContainer.Scale = new Vector2(-10, 10);
        // polyContainer.Position = new Vector2(100, 500);
        // AddChild(polyContainer);
        // foreach (Vector2[] poly in polygons)
        // {
        //     var poly2d = new Polygon2D() { Polygon = poly };
        //     poly2d.SelfModulate = new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1);
        //     polyContainer.AddChild(poly2d);
        // }

        var meshMap = distortedQuadMesh.GenerateMeshes();



        // meshList = distorter.QuadMesh.GenerateMeshes();
        foreach (Rect2 rect in meshMap.Keys)
        {
            var mesh = meshMap[rect];
            var meshInstance = GetNode<MeshInstance3D>("wireframeContainer").Duplicate() as MeshInstance3D;

            // var meshInstance = GetNode<MeshInstance3D>("container").Duplicate() as MeshInstance3D;
            // var material = meshInstance.MaterialOverride.Duplicate() as ShaderMaterial;
            // material.SetShaderParameter("texture_edge", tex);
            // meshInstance.MaterialOverride = material;


            meshInstance.Mesh = mesh;

            MeshInstanceMap[rect] = meshInstance;

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

