using Godot;
using System;
using System.Drawing;

public partial class TerrainTextureTesting : Node2D
{

    public override void _Ready()
    {
        var gUtils = new GeometryUtils();

        var polygon = GetNode<Polygon2D>("Polygon2D").Polygon;
        var texScene = GetNode<TerrainTexture>("TerrainTexture");

        texScene.SetPolygon(polygon);

        // GD.Print(texScene.Size);


        var sprite = GetNode<Sprite2D>("Sprite2D");
        var viewport = GetNode<SubViewport>("TerrainTexture");

        sprite.Texture = viewport.GetTexture();
        
        

        // var meshMat = GetNode<MeshInstance2D>("MeshInstance2D").Material as ShaderMaterial;
        // meshMat.SetShaderParameter("viewport_tex", viewport.GetTexture());


        // meshInst.Mesh = new QuadMesh() { Size = texScene.Size };
        // meshInst.Position = texScene.Size / 2;

    }

    
}
