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

    private Vector2 nextTunnelStart = new Vector2(0, 30);
    private float accumTunnelRotation = 0;

    private List<IQuadMeshDistorter> distortionQueue = new();
    private IQuadMeshDistorter activeDistort;
    public Task task;
    public Task MeshTask;
    public float taskTime = 0;

    public override void _Ready()
    {
        terrain = new(100);
        terrain.MaxHeight = 80;
        terrain.MinHeight = 15;
        GetNode<LineDrawing>("LineDrawing").lineDrawn += OnLineDrawn;
        GenerateMap_v2();

        // GetNode<Godot.Timer>("ExplodeTimer").Timeout += DrawTunnel;
        // GetTree().CreateTimer(1).Timeout += () => DisplayQuadMeshPolygons(quadMeshDistortionApplier.GetQuadMesh());
        // GetTree().CreateTimer(3).Timeout += () => distortionQueue.Add(new BaseTerrainDistorter(0.25f, new Rect2(new Vector2(50,30), new Vector2(20,20))));
        GetNode<PlaneMouseCapture>("PlaneMouseCapture").PlaneClicked += OnPlaneClicked;
    }

    private void OnLineDrawn(Vector3[] line)
    {
        distortionQueue.Add(new TunnelDistorter(new Vector2(line[0].X, line[0].Y), new Vector2(line[1].X, line[1].Y), 3));
    }


    private void OnPlaneClicked(Vector3 vector)
    {
        var vec2 = new Vector2(vector.X, vector.Y);
        distortionQueue.Add(new TunnelDistorter(vec2, vec2, (float)GD.RandRange(8f, 8)));
    }


    private void DrawTunnel()
    {
        var tunnelStart = nextTunnelStart;
        var tunnelEnd = new Vector2(1.5f, 0f).Rotated(accumTunnelRotation) + tunnelStart;
        nextTunnelStart = tunnelEnd;
        accumTunnelRotation += 0.005f;

        distortionQueue.Add(new TunnelDistorter(tunnelStart, tunnelEnd, 4));
    }


    public override void _PhysicsProcess(double delta)
    {
        if (task != null && task.IsCompleted)
        {
            var quadMesh = quadMeshDistortionApplier.GetQuadMesh();
            var totalTaskTime = Time.GetTicksMsec() - taskTime;
             
            var meshTime = Time.GetTicksMsec();
            var affectedAreas = MeshInstanceMap.Keys.Where(rect => quadMeshDistortionApplier.DistortersActiveOnQuad(rect).Contains(activeDistort));
            var affectLength = affectedAreas.ToList().Count;
            foreach (var rect in affectedAreas)
            {
                var meshInstance = MeshInstanceMap[rect];
                var mesh = quadMesh.GenerateMeshes(rect)[rect];
                meshInstance.Mesh = mesh;
            }
            
            task = null;
            activeDistort = null;
            GD.Print("total triangulation time: ",  quadMesh.triangulationTimeCount);
            quadMesh.triangulationTimeCount = 0;
            GD.Print("affected areas: ", affectLength, "time for task: ", totalTaskTime, "  time for mesh gen: ", Time.GetTicksMsec() - meshTime );
        }

        if (distortionQueue.Count > 0 && activeDistort == null)
        {
            activeDistort = distortionQueue[0];
            distortionQueue.RemoveAt(0);

            task = new Task(() => quadMeshDistortionApplier.AddMeshDistorter(activeDistort));
            task.Start();
            taskTime = Time.GetTicksMsec();

        }

        //update quad meshes parameters
        var mouse_pos = GetNode<PlaneMouseCapture>("PlaneMouseCapture").LastMousePos;
        foreach (var meshInstance in MeshInstanceMap.Values)
        {
            var mat = (ShaderMaterial)meshInstance.MaterialOverride;
            mat.SetShaderParameter("circle_center", mouse_pos);


        }
    }

    private void GenerateMap_v2()
    {
        var width = 80f;
        List<Polygon2D> MapPolygonInstances = terrain.GenerateNext(width);
        var mapPoly = MapPolygonInstances[0];
        var tex = GetEdgeTexture(mapPoly.Polygon);
        var quadMesh = new PolygonQuadMesh(mapPoly.Polygon);
        quadMeshDistortionApplier = new(quadMesh);

        quadMeshDistortionApplier.AddMeshDistorter(new EdgeWrapDistorter(1, 6));
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

        distortionQueue.Add(new TunnelDistorter(new Vector2(30, 20),new Vector2(10, 20), 7));
        // distortionQueue.Add(new ExplosionDistorter(new Vector2(29, 30), 4));

        // distortionQueue.Add(new TunnelDistorter(new Vector2(20, 15),new Vector2(10, 15), 4));
        // distortionQueue.Add(new TunnelDistorter(new Vector2(20, 15), new Vector2(30, 15), 4, true));

    }

    public void DisplayQuadMeshPolygons(PolygonQuadMesh quadMesh)
    {
        var polygons = quadMesh.GetPolygons();
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

