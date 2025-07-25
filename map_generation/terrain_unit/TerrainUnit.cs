using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

public partial class TerrainUnit : Node3D
{

    public float EdgeRadius = 1;
    public float EdgeExtension = 4;

    [Export] public PackedScene terrainTextureScene;
    TerrainTexture terrainTexture;
    List<MeshInstance3D> meshInstanceList = new();

    Vector2[] _polygon;
    DistortableCompositePolygonMeshUpdater meshUpdater;
    ShaderMaterial material = GD.Load<ShaderMaterial>("uid://b8kwrx0bwxsks").Duplicate() as ShaderMaterial;


    public override void _Ready()
    {
    }

    public void SetPolygon(Vector2[] polygon)
    {
        if (!IsNodeReady())
        {
            GD.PrintErr("node not ready");
            return;
        }


        if (meshUpdater != null) RemoveChild(meshUpdater);

        _polygon = polygon;
        meshUpdater = (DistortableCompositePolygonMeshUpdater)GD.Load<PackedScene>("uid://3yd4st0lfqrw").Instantiate();
        var quadMesh = new PolygonQuadMesh(polygon);

        meshUpdater.SetQuadMesh(quadMesh);
        AddChild(meshUpdater);

        var edgeWrapDistort = new EdgeWrapDistorter(EdgeRadius, EdgeExtension);
        meshUpdater.DistortAndUpdate(edgeWrapDistort);

        // set material params REPLACE WITH SCENE THAT HANDLES EDGE TEXTURES IN A VIEWPORT

        // var edgeTexture = GetEdgeTexture(polygon);
        terrainTexture = terrainTextureScene.Instantiate() as TerrainTexture;
        AddChild(terrainTexture);

        terrainTexture.SetPolygon(polygon);
        CallDeferred("SetMaterials"); 

        // material.SetShaderParameter("texture_edge", texture);

        // set all mesh instances to share material
        foreach (var inst in meshUpdater.GetMeshInstances())
        {
            meshInstanceList.Add(inst);
            inst.MaterialOverride = material;
        }
        

    }

    void SetMaterials()
    {
        var texture = terrainTexture.GetTexture();
        material.SetShaderParameter("texture_edge", texture);
        material.SetShaderParameter("edge_depth", (EdgeExtension + EdgeRadius) * -1);
        GD.Print("terrain tex size:", terrainTexture.Size);
    }

    public Rect2 GetRect2()
    {
        var gUtil = new GeometryUtils();
        return gUtil.RectIFromPolygon(_polygon);


    }

    //-----------
    public void ExplodeTerrain(Vector2 center1, Vector2 center2, float radius)
    {
        var distort = new TunnelDistorter(center1, center2, radius);
        terrainTexture.AddExplosion(center1, center2, radius);
        meshUpdater.DistortAndUpdate(distort);
        
        
        // var img = terrainTexture.GetTexture().GetImage();
        // img.SavePng("res://TERRAIN_TEX.png");


    }
    public void ExplodeTerrain(Vector2 center, float radius)
    {
        ExplodeTerrain(center, center, radius);

    }


}