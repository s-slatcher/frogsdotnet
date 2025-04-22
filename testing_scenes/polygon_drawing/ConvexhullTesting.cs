using Godot;
using System;

public partial class ConvexhullTesting : Node2D
{
    public override void _Ready()
    {
        var poly = GetNode<Polygon2D>("Polygon2D").Polygon;
        var convexPolys = Geometry2D.DecomposePolygonInConvex(poly);
        
        foreach (var cPoly in convexPolys)
        {
            var rectPoly = new GeometryUtils().PolygonFromRect(new GeometryUtils().RectFromPolygon(cPoly));
            AddChild(new Polygon2D(){Polygon = rectPoly,Modulate = new Color(GD.Randf(),GD.Randf(),GD.Randf(),1)});

        }
    }

}
