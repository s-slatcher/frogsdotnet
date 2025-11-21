using Godot;
using System;

public partial class CustomPolygonTesting : Node2D
{
	// Called when the node enters the scene tree for the first time.

	public Polygon poly;
	public override void _Ready()
	{
		float time = Time.GetTicksMsec();
		var polyNode = GetNode<Polygon2D>("Polygon2D");
		Polygon polygon = new();
		polygon.SetPoints(polyNode.Polygon, 0);

		var polyNodeOutput = GetNode<Polygon2D>("Polygon2D2");
		polyNodeOutput.Polygon = polygon.GetSimplifiedPolygon().ToArray();
		
		GD.Print("point size from/to:", polyNode.Polygon.Length,", ", polyNodeOutput.Polygon.Length);

		var gu = new GeometryUtils();
		// foreach (var node in polygon.allNodeCache)
		// {
		// 	var poly2d = new Polygon2D();
		// 	poly2d.Polygon = gu.PolygonFromRect(node.BoundingRect);
		// 	var sizeRatio = node.BoundingRect.Size.X / polygon.BoundingRect.Size.X;
		// 	sizeRatio = float.Sqrt(sizeRatio);
		// 	poly2d.SelfModulate = new Color(sizeRatio, sizeRatio, sizeRatio, 1);
		// 	AddChild(poly2d);
		// }
		poly = polygon;
		GD.Print(Time.GetTicksMsec() - time);

		// inPointInPolygon performance comparision
		// runs same point 20 times
		var mark = GetNode<Marker2D>("Marker2D");
		var pos = mark.Position;
		var treeTime = Time.GetTicksMsec();
		for (int i = 0; i < 20; i++)
		{
			GD.Print(poly.IsPointInPolygon(pos));
		}
		GD.Print("tree time: ", Time.GetTicksMsec() - treeTime);
		
		var standardTime = Time.GetTicksMsec();
		for (int i = 0; i < 20; i++)
		{
			GD.Print(Geometry2D.IsPointInPolygon(pos, polyNodeOutput.Polygon));
		}
		GD.Print("standard time: ", Time.GetTicksMsec() - standardTime);

		
		

    }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

	}

    public override void _PhysicsProcess(double delta)
	{
		// var line2d = GetNode<Line2D>("Line2D");
		// var p1 = line2d.Points[0];
		// var p2 = line2d.Points[1];
		// var collisions = poly.RaycastPolygon(p1, p2);

		// GD.Print(poly.IsPointInPolygon(mark.Position));
    }

}
