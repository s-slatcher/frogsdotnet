using Godot;
using System;
using System.Collections.Generic;

public partial class NormalPolyTesting : Node2D
{
    [Export] Polygon2D polyNode;

    public override void _Ready()
    {
        var points = polyNode.Polygon;

        var normPoly = new NormalPoly(points, 30);
        
        Rect2I boundingRect = normPoly.Rect;

        var polyNodeNew = new Polygon2D();

        var pointList = new List<Vector2>();

        foreach (LinkedPoint point in normPoly.GetIterator())
        {
            pointList.Add(point.prev);
        }

        polyNodeNew.Polygon = pointList.ToArray();

        

        var colRect = new ColorRect(){Size = boundingRect.Size};
        colRect.Color = Godot.Colors.Red;
        AddChild(colRect);
        AddChild(polyNodeNew);
    }


}
