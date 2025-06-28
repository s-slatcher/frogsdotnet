using Godot;
using Microsoft.VisualBasic;
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
    
    public Dictionary<Rect2, MeshInstance3D> MeshInstanceMap = new();
    public QuadMeshDistortionApplier quadMeshDistortionApplier;

    public TerrainMap terrain;
    GeometryUtils gUtils = new();
    private Vector3 nextMeshPosition = new Vector3(0, 0, 0);

    private Vector2 nextTunnelStart = new Vector2(30, 0);
    private float accumTunnelRotation = 0;

    private List<IQuadMeshDistorter> distortionQueue = new();
    private IQuadMeshDistorter activeDistort;
    public Task task;

    public override void _Ready()
    {
        terrain = new(100);
        terrain.MaxHeight = 80;
        terrain.MinHeight = 15;

        GenerateMap_v2();

        // GetNode<Godot.Timer>("ExplodeTimer").Timeout += () => DrawTunnel();
        // GetNode<Godot.Timer>("ExplodeTimer").Timeout += () => DrawTunnel();
        GetTree().CreateTimer(1).Timeout += () => DisplayQuadMeshPolygons(quadMeshDistortionApplier.GetQuadMesh());

    }

    private void DrawTunnel()
    {
        var tunnelStart = nextTunnelStart;
        var tunnelEnd = new Vector2(1, 0.5f).Rotated(accumTunnelRotation) + tunnelStart;
        nextTunnelStart = tunnelEnd;
        accumTunnelRotation += 0.1f;

        distortionQueue.Add(new TunnelDistorter(tunnelStart, tunnelEnd, 2));
    }


    public override void _PhysicsProcess(double delta)
    {
        if (task != null && task.IsCompleted)
        {

            var affectedAreas = MeshInstanceMap.Keys.Where(rect => quadMeshDistortionApplier.DistortersActiveOnQuad(rect).Contains(activeDistort));
            foreach (var rect in affectedAreas)
            {
                var meshInstance = MeshInstanceMap[rect];
                var mesh = quadMeshDistortionApplier.GetQuadMesh().GenerateMeshes(rect)[rect];
                meshInstance.Mesh = mesh;
            }
            task = null;
            activeDistort = null;
        }

        if (distortionQueue.Count > 0 && activeDistort == null)
        {
            activeDistort = distortionQueue[0];
            distortionQueue.RemoveAt(0);

            task = new Task(() => quadMeshDistortionApplier.AddMeshDistorter(activeDistort));
            task.Start();
        }
    }

    private void GenerateMap_v2()
    {
        var width = 40f;
        List<Polygon2D> MapPolygonInstances = terrain.GenerateNext(width);
        var mapPoly = MapPolygonInstances[0];
        var tex = GetEdgeTexture(mapPoly.Polygon);
        var quadMesh = new PolygonQuadMesh(mapPoly.Polygon);
        quadMeshDistortionApplier = new(quadMesh);

        quadMeshDistortionApplier.AddMeshDistorter(new EdgeWrapDistorter(1, 2));
        quadMeshDistortionApplier.AddMeshDistorter(new BaseTerrainDistorter(4));
        

        var distortedQuadMesh = quadMeshDistortionApplier.QuadMeshHistory[^1];

        var meshMap = distortedQuadMesh.GenerateMeshes();

        foreach (Rect2 rect in meshMap.Keys)
        {
            var mesh = meshMap[rect];
            var meshInstance = GetNode<MeshInstance3D>("container").Duplicate() as MeshInstance3D;
            var material = meshInstance.MaterialOverride.Duplicate() as ShaderMaterial;
            material.SetShaderParameter("texture_edge", tex);
            meshInstance.MaterialOverride = material;

            meshInstance.Mesh = mesh;

            MeshInstanceMap[rect] = meshInstance;

            AddChild(meshInstance);
        }

        distortionQueue.Add(new ExplosionDistorter(new Vector2(19, 20), 10));
        distortionQueue.Add(new ExplosionDistorter(new Vector2(29, 30), 7));

    }

    public void DisplayQuadMeshPolygons(PolygonQuadMesh quadMesh)
    {
        var polygons = quadMesh.GetPolygons();
        GD.Print(polygons.Count);
        var polyContainer = new Node2D();
        polyContainer.RotationDegrees = 180;
        polyContainer.Scale = new Vector2(-10, 10);
        polyContainer.Position = new Vector2(100, 500);
        AddChild(polyContainer);

        foreach (Vector2[] poly in polygons)
        {
            var poly2d = new Polygon2D() { Polygon = poly };
            poly2d.SelfModulate = new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1);
            polyContainer.AddChild(poly2d);
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

