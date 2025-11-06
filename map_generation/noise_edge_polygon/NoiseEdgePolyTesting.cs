using Godot;
using System;

public partial class NoiseEdgePolyTesting : Node2D
{
    public override void _Ready()
    {
        var line = GetNode<Line2D>("Line2D");
        var noisePoly = new NoiseEdgePoly(50, 75, 20).Polygon;
        foreach(var p in noisePoly)
        {
            line.AddPoint(p);
        }        


    }

}
