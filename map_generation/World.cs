using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vector2 = Godot.Vector2;



public partial class World : Node3D
{

    [Export] ShaderMaterial meshMaterial;


    
    public int MeshLodIndex = 0;
    public List<MeshInstance3D> MeshInstances = new();
    public List<MeshInstance3D> WireMeshInstances = new();

    public List<ExtrudedMesh> MeshGenerators = new();

    public override void _Ready()
    {
        GenerateMap();
        GetNode<Timer>("SwapMesh").Timeout += SwapMesh;

    }

    private void SwapMesh()
    {
        for (int i = 0; i < MeshInstances.Count; i++)
        {
            if (MeshGenerators[i].Meshes.Count <= MeshLodIndex) continue;
            MeshInstances[i].Mesh = MeshGenerators[i].Meshes[MeshLodIndex];
            WireMeshInstances[i].Mesh = MeshGenerators[i].WireframeMeshes[MeshLodIndex];
        }
        MeshLodIndex += 1;
        if (MeshLodIndex > 10) MeshLodIndex = 0;
    }


    private void GenerateMap()
    {
        var terrainMap = new TerrainMap();
        terrainMap.Width = 225;
        terrainMap.MaxHeight = 80;
        
        terrainMap.MinHeight = 5;
        terrainMap.MajorDivisions = 3;
        terrainMap.Seed = (int)GD.Randi();

        List<Polygon2D> MapPolygonInstances = terrainMap.Generate();
        
        var wireframeMat = GD.Load("uid://ccgh4ycc7mj5e") as StandardMaterial3D;
        foreach(var poly2D in MapPolygonInstances)
        {



            AddChild(poly2D);
            
            var em = new ExtrudedMesh(poly2D.Polygon, 0.125f, 1.25f, 3);
            var meshInstance = GetNode<MeshInstance3D>("container").Duplicate() as MeshInstance3D;
            var meshPosition = new Godot.Vector3(poly2D.Position.X, poly2D.Position.Y, 0);
            meshInstance.Position = meshPosition;
            
            var material = meshInstance.MaterialOverride.Duplicate() as ShaderMaterial;
            meshInstance.MaterialOverride = material;
            
            var tex = GetEdgeTexture(poly2D.Polygon);
            material.SetShaderParameter("texture_edge", tex);

            MeshGenerators.Add(em);
            MeshInstances.Add(meshInstance);
            // meshInstance.Mesh = em.Meshes[index];
            // meshInstance.Mesh = em.Meshes[^1];
            

            var sprite2d = new Sprite2D(){Texture = tex, Position = poly2D.Position, Scale = new Vector2(1/8f, 1/8f), Centered = false};
            sprite2d.Modulate = new Color(1,1,1,0.5f);
            AddChild(sprite2d);

            var meshInstanceWireframe = new MeshInstance3D(){Position = meshPosition + new Godot.Vector3(0,0,0.5f), MaterialOverride = wireframeMat};
            AddChild(meshInstanceWireframe);         
            WireMeshInstances.Add(meshInstanceWireframe);   

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

