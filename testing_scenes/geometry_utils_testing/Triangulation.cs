using Godot;
using System;

public partial class Triangulation : Node2D
{
    public override void _Ready()
    {
        var poly = GetNode<Polygon2D>("Polygon2D").Polygon;
        var tri_idx = Geometry2D.TriangulatePolygon(poly);

        for (int i = 2; i < tri_idx.Length; i += 3)
        {
            GD.Print(i, " ", tri_idx.Length);
            Vector2[] newLinePoints =
            [
                poly[tri_idx[i-2] ],
                poly[tri_idx[i-1]],
                poly[tri_idx[i]]
            ];
            var line2d = new Polygon2D() { Polygon = newLinePoints };
            AddChild(line2d);
            line2d.Modulate = new Color(i/30,1,1,1); 
            
        }

    }

}
