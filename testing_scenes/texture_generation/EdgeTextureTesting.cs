using Godot;
using System;

public partial class EdgeTextureTesting : Node2D
{

    public override void _Ready()
    {
        var time = Time.GetTicksMsec();

        var edgeTextureGenerator = new EdgeTextureGenerator();
        edgeTextureGenerator.Polygon = GetNode<Polygon2D>("Polygon2D").Polygon;
        var image = edgeTextureGenerator.Generate();
        
        
        var imageTex = ImageTexture.CreateFromImage(image);
        
        
        // for (int i = 0; i < 50; i++)
        // {
        //     imageTex.Update(image);
        // }

        GetNode<Sprite2D>("EdgeTextureContainer").Texture = imageTex;
        GD.Print(Time.GetTicksMsec() - time + " ms for tex gen");

    }

}
