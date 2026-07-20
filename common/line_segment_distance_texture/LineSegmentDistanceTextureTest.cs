using Godot;
using System;

public partial class LineSegmentDistanceTextureTest : Node2D
{

    public override void _Ready()
    {
        var node = GetNode<Polygon2D>("Polygon2D");

        var poly = new NormalPoly(node.Polygon, 10);
        
        node.Polygon = poly.Polygon;
        var lineSegTexture = new LineSegmentDistanceTexture(poly);
        lineSegTexture.PixelPerUnit = 1;
        lineSegTexture.PerpDist = 15;

        var tex = lineSegTexture.GetTexture();
        
        AddChild(new Sprite2D(){Texture = tex, Centered = false, Modulate = new Godot.Color(1,1,1,0.5f) });
    }

}
