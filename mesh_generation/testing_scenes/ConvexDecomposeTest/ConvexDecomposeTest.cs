using Godot;
using System;

public partial class ConvexDecomposeTest : Node2D
{
    [Export] Polygon2D polyNode;


    public override void _Ready()
    {
        var decomposePolys = Geometry2D.DecomposePolygonInConvex(polyNode.Polygon);
        foreach (var poly in decomposePolys)
        {
            var shrunkPoly = Geometry2D.OffsetPolygon(poly, -1)[0];
            AddChild(new Polygon2D(){Polygon = shrunkPoly});
        }
    }

}
