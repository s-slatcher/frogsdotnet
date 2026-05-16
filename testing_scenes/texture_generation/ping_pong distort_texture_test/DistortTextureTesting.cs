using Godot;
using System;
using System.ComponentModel;
using System.Numerics;

public partial class DistortTextureTesting : Node2D
{

    [Export] MeshInstance2D mesh;
    ShaderMaterial meshMaterial;
    [Export] MeshDistortTexture meshDistortTexture;

    public override void _Ready()
    {

        Rect2 rect = new(){Size = new Godot.Vector2(100,100)};
        meshDistortTexture.SetRect(rect);
        meshMaterial = (ShaderMaterial)mesh.Material;


        GetTree().CreateTimer(3).Timeout += OnTimeout;


    }

    private void OnTimeout()
    {
        // meshDistortTexture.ApplyDistortion(new TerrainDistortion(GD.Randf() * 3, new Godot.Vector2(GD.Randf() * 100, GD.Randf() * 100)));

        // meshMaterial.SetShaderParameter("distort_texture", meshDistortTexture.GetTexture());
    }
}
