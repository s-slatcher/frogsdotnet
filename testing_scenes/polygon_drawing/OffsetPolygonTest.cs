using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class OffsetPolygonTest : Node2D
{
    public override void _Ready()
    {
        var path = GetNode<Path2D>("Path2D");
        var curve = path.Curve;
        // curve.BakeInterval = 5;
        var poly = curve.Tessellate();
        // var poly = curve.GetBakedPoints();

        var shrunkPoly = Geometry2D.OffsetPolygon(poly, -50);
        var unShrunkPoly = Geometry2D.OffsetPolygon(shrunkPoly[0], 50);
        AddPolygonListToScene([shrunkPoly[0]]);  
    
        
    }

    void insetTest()
    {
        var path = GetNode<Path2D>("Path2D");
        var curve = path.Curve;
        curve.BakeInterval = 1;
        var poly = curve.Tessellate();
        
        var time = Time.GetTicksMsec();
        var insetPolyList = new List<Vector2[]>();
        for (int i = 0; i < 100; i++)
        {
            insetPolyList.AddRange(Geometry2D.OffsetPolygon(poly, -i));
        }
        GD.Print("time: " + (Time.GetTicksMsec()- time));
        AddPolygonListToScene(insetPolyList);
    }
    
    void AddPolygonListToScene(List<Vector2[]> list)
    {
        foreach (var poly in list)
        {
            AddChild(new Polygon2D(){Polygon = poly, SelfModulate = randColor()});
        }
    }

    Color randColor()
    {
        return new Color(GD.Randf(), GD.Randf(), GD.Randf(), 1);
    }
}
