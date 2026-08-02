using Godot;
using System;
using System.Diagnostics;

[Tool]
public partial class RectSubdivide : Node2D
{
    public override void _Ready()
    {
        var gu = new GeometryUtils();

        var rect = new Rect2(new(), new Vector2(100,100));

        var subRects = GeometryUtils.SubdivideRect(rect);

        foreach(var subRect in subRects)
        {
            var poly = gu.PolygonFromRect(subRect);
            AddChild(new Polygon2D()
            {
                Polygon = poly,
                SelfModulate = Godot.Colors.Black * GD.Randf()
            });
        }

    }

    

}
