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
        
        thread = new(GenerateMap);
        thread.Start();
    }

    public override void _Process(double delta)
    {  

        meshTime += delta;
        
        if (task == null || !task.IsCompleted) return;
        
        meshGenerationTasks[ MeshGenerators[^1].GetMesh() ] = MeshInstances[^1];
        
        
        // GD.Print(meshTime + " sec to make mesh");
        meshTime = 0;
        task = null;
        GetTree().CreateTimer(4).Timeout += () => {
            thread = new(GenerateMap);
            thread.Start(); 
        };
        
        

        meshFinished = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        // check every task for completion
        var tasksToRemove = new List<Task<Mesh>>();
        foreach (var task in meshGenerationTasks.Keys)
        {
            if (task.IsCompleted)
            {
                
                var meshInstance = meshGenerationTasks[task];
                meshInstance.Mesh = task.Result;
                tasksToRemove.Add(task);
                


            }
        }
        foreach (var task in tasksToRemove) meshGenerationTasks.Remove(task);
    }


    private void GenerateMap()
    {
     
        var width = 300f;

        
        List<Polygon2D> MapPolygonInstances = terrain.GenerateNext(width);
        
        var wireframeMat = GD.Load("uid://ccgh4ycc7mj5e") as StandardMaterial3D;
        
        var poly2D = MapPolygonInstances[0];
        // AddChild(poly2D);
        
        var em = new ExtrudedMesh(poly2D.Polygon, 0.25f, 1f, 3);
        em.EdgeCutOffHeight = 7f;
        MeshGenerators.Add(em);
        task = Task.Factory.StartNew(() => GenerateMesh(em));
        // GD.Print("init task time: " + (Time.GetTicksMsec() - time));        
        var meshInstance = GetNode<MeshInstance3D>("container").Duplicate() as MeshInstance3D;
        meshInstance.Position = nextMeshPosition;
        nextMeshPosition += new Vector3(width + 10, 0 ,0);
        MeshInstances.Add(meshInstance);

        // var meshPosition = new Godot.Vector3(poly2D.Position.X, poly2D.Position.Y, 0);
        // meshInstance.Position = meshPosition;
        var material = meshInstance.MaterialOverride.Duplicate() as ShaderMaterial;
        meshInstance.MaterialOverride = material;
        
        var tex = GetEdgeTexture(poly2D.Polygon);
        material.SetShaderParameter("texture_edge", tex);
        CallDeferred("add_child", meshInstance);
            
            
            // meshInstance.Mesh = em.Meshes[index];
            // meshInstance.Mesh = em.GetMesh();
            

            // var sprite2d = new Sprite2D(){Texture = tex, Position = poly2D.Position, Scale = new Vector2(1/8f, 1/8f), Centered = false};
            // sprite2d.Modulate = new Color(1,1,1,0.5f);
            // AddChild(sprite2d);

            // var meshInstanceWireframe = new MeshInstance3D(){Position = meshPosition + new Godot.Vector3(0,0,0.5f), MaterialOverride = wireframeMat};
            // AddChild(meshInstanceWireframe);         
            // WireMeshInstances.Add(meshInstanceWireframe);   

        
    }

    void GenerateMesh(ExtrudedMesh extrudedMesh)
    {
        extrudedMesh.SetupPolygonQuad();
    }

    void AddMeshToScene()
    {
        
              
        MeshInstances[^1].Mesh = MeshGenerators[^1].CachedMesh;
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

