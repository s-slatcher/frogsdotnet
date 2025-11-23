using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PhysicsServerTest : Node2D
{
	Polygon2D initPolyNode;
	PhysicsDirectSpaceState2D Space;

	List<(Vector2, float)> ActiveCircles = new();
	List<(Vector2, float)> FailedCircles = new();
	

	int totalQueryCount = 0;
	public override void _Ready()
    {
        initPolyNode = GetNode<Polygon2D>("Polygon2D");
        GetTree().CreateTimer(1).Timeout += OnSpawnTimer;
        Space = PhysicsServer2D.SpaceGetDirectState(GetWorld2D().Space);



    }

    private void OnSpawnTimer()
    {
        GetTree().CreateTimer(1.5f).Timeout += OnSpawnTimer;
		
		var time = Time.GetTicksMsec();
		var circlesPerLoop = 25;
		var totalCreated = 0;
		for (int i = 0; i < circlesPerLoop; i++)
		{
			if(  SpawnNewCircle() ) totalCreated++;
		}
		GD.Print("genned: ", totalCreated, " from trying to gen: ", circlesPerLoop, " circles in time: ", Time.GetTicksMsec() - time);
		GD.Print("total circle overlap queries: ", totalQueryCount);
		QueueRedraw();

    }


    private bool SpawnNewCircle()
    {

        // get a rand position on frame
        var loopMax = 50;
		var loops = 0;
        Vector2 pos;
        while (true)
        {
            loops++;
            if (loops > loopMax)
            {
                GD.Print("cant find valid point");
                return false;
            }
            pos = new Vector2(GD.RandRange(0, 800), GD.RandRange(0, 800));
            var pointQuery = new PhysicsPointQueryParameters2D() { Position = pos };
            var result = Space.IntersectPoint(pointQuery);

            // no collisions, point is valid break loop
            if (result.Count == 0) break;

        }

        // GD.Print("cirlce added at ", pos);
        float radius = 60;
		
		while (radius > 5)
        {
            bool circleAdded = TryAddCollisionCircle(pos, radius);
			if (circleAdded)
            {
            ActiveCircles.Add((pos, radius));
			return true;
			}
                
			radius -= 5;
			
        }
		return false;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
	{
	}

	public bool TryAddCollisionCircle(Vector2 position, float radius)
    {
		// make circle shape 
        var transform = Transform2D.Identity;
		transform.Origin = position;
		var circle = PhysicsServer2D.CircleShapeCreate();
		PhysicsServer2D.ShapeSetData(circle, radius);
	
		var colQuery = new PhysicsShapeQueryParameters2D()
        {
            ShapeRid = circle,
			Transform = transform,
			CollideWithAreas = true
        };
		
		var results = Space.GetRestInfo(colQuery);
		totalQueryCount ++;
		
		if (results.ContainsKey("normal")) 
		{
			// GD.Print(results["normal"]);
			return false;
       
		}
		// make area and give it the transform
		var area = PhysicsServer2D.AreaCreate();
		PhysicsServer2D.AreaAddShape(area, circle);
		PhysicsServer2D.AreaSetTransform(area, transform);
		
		// add to world space (Rid not directSpace)
		PhysicsServer2D.AreaSetSpace(area, GetWorld2D().Space);


		// var shape2d = new CircleShape2D();
		// var shapeNode = new CollisionShape2D(){Shape = shape2d};
		// var staticB = new StaticBody2D();
		// shapeNode.Transform = transform;	
		// staticB.AddChild(shapeNode);
		// AddChild(staticB);
		
		return true;
    }

    public override void _Draw()
    {
		var circleColor = new Color(0,0,0,0.5f);
		var failedColor = new Color(1,0,0,0.15f);
        foreach (var circle in ActiveCircles)
        {
            DrawCircle(circle.Item1, circle.Item2, circleColor);
        }
		 foreach (var circle in FailedCircles)
        {
            DrawCircle(circle.Item1, circle.Item2, failedColor);
        }
    }



}
