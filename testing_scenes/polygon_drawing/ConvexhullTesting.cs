using Godot;
using System;
using System.Numerics;

public partial class ConvexhullTesting : Node2D
{
    public override void _Ready()
    {
        var poly = GetNode<Polygon2D>("Polygon2D").Polygon;
        var convexPolys = Geometry2D.DecomposePolygonInConvex(poly);
        var gUtils = new GeometryUtils();
        
        // foreach (Godot.Vector2[] cPoly in convexPolys)
        // {
        //     var rectPoly = gUtils.PolygonFromRect(gUtils.RectFromPolygon(cPoly));
        //     AddChild(new Polygon2D() { Polygon = rectPoly, Modulate = new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1) });

        // }
    }

}
