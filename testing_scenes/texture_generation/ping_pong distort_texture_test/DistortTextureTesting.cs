 using Godot;
using System;
using System.ComponentModel;
using System.Numerics;

public partial class DistortTextureTesting : Node3D
{

    [Export] MeshInstance3D mesh;
    ShaderMaterial meshMaterial;
    [Export] MeshDistortTexture meshDistortTexture;

    public override void _Ready()
    {

        Rect2 rect = new(){Size = new Godot.Vector2(100,100)};
        meshDistortTexture.SetRect(rect);
        
        meshMaterial = (ShaderMaterial)mesh.MaterialOverride;

        meshDistortTexture.TextureUpdated += OnTextureUpdated;


        // GetTree().CreateTimer(3).Timeout += OnTimeout;


    }

    private void OnTextureUpdated(Godot.ViewportTexture texture)
    {
        var meshMat = (ShaderMaterial)mesh.MaterialOverride;
        meshMat.SetShaderParameter("explosion_map", texture);
    }


    private void OnTimeout()
    {
        // meshDistortTexture.ApplyDistortion(new TerrainDistortion(GD.Randf() * 3, new Godot.Vector2(GD.Randf() * 100, GD.Randf() * 100)));

        // meshMaterial.SetShaderParameter("distort_texture", meshDistortTexture.GetTexture());
    }
}
