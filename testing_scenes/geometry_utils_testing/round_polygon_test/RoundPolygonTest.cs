using Godot;
using System;

public partial class RoundPolygonTest : Node2D
{
    public override void _Ready()
    {
        var polyNode = GetNode<Polygon2D>("Polygon2D");
        var rounded = GeometryUtils.RoundPolygon(polyNode.Polygon, 60, 5);
        var roundedLine = GeometryUtils.Line2DFromPolygon(rounded);
        
        AddChild(roundedLine);

    }

}
