using Godot;
using System;

public partial class CustomPolygonTesting : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
    {
		var polyNode = GetNode<Polygon2D>("Polygon2D");
		Polygon polygon = new();
		polygon.SetPoints(polyNode.Polygon, 15);

		var polyNodeOutput = GetNode<Polygon2D>("Polygon2D2");
		polyNodeOutput.Polygon = polygon.GetSimplifiedPolygon().ToArray();

		var gu = new GeometryUtils();
		foreach(var node in polygon.leafNodeCache)
        {
            var poly2d = new Polygon2D();
            poly2d.Polygon = gu.PolygonFromRect(node.BoundingRect);
            var sizeRatio = node.BoundingRect.Size.X / polygon.BoundingRect.Size.X;
            sizeRatio = float.Sqrt(sizeRatio);
            poly2d.SelfModulate = new Color(sizeRatio, sizeRatio, sizeRatio, 1);
			AddChild(poly2d);	
        }
    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
    {
        
    }
}
