using Godot;
using System;

public partial class DebugPolygon : Polygon2D
{


    public override void _Ready()
    {

        var viewportSize = GetViewport().GetVisibleRect().Size;

        var hoverDistance = viewportSize.X / 50;

        foreach (var point in Polygon)
        {


        }
    }

}
