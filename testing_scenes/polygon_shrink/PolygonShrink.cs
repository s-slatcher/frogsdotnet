using Godot;
using System;

public partial class PolygonShrink : Node2D
{
    Polygon2D polyNode;

    public override void _Ready()
    {
        polyNode = GetNode<Polygon2D>("Polygon2D");
        var poly = polyNode.Polygon;
        
        var decomposedPolys = Geometry2D.DecomposePolygonInConvex(poly);
        foreach (var convex_poly in decomposedPolys)
        {
            var shrunkPoly = Geometry2D.OffsetPolygon(convex_poly, -20)[0];
            AddChild(new Polygon2D() { Polygon = shrunkPoly, SelfModulate = new Color(GD.RandRange(0,1), GD.RandRange(0,1), GD.RandRange(0,1), 1) });
        }
        
        
    }

}
