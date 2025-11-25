using Godot;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Xml.XPath;

public partial class CollisionPolyPerfTest : Node2D
{

    PhysicsDirectSpaceState2D space;
    PhysicsShapeQueryParameters2D param;

    List<Vector2> intersectPoints = new();
    public override void _Ready()
    {
        var time = 0f;

        var polyNode = GetNode<Polygon2D>("Poly");
        var polygon = polyNode.Polygon;
        var collisionPoly = GetNode<StaticBody2D>("Static").GetChild<CollisionPolygon2D>(0);


        // first test time to convert poly into collision polygon

        time = Time.GetTicksMsec();
        collisionPoly.Polygon = polygon;
        time = Time.GetTicksMsec();
        
        var shapeSrc = GetNode<Area2D>("Area2D");

        var collider = PhysicsServer2D.BodyCreate();
        var colliderShape = PhysicsServer2D.AreaGetShape(shapeSrc.GetRid(), 0);
        // PhysicsServer2D.BodyAddShape(collider, colliderShape, Transform2D.Identity);


        GD.Print(collider);
        var collQueryParams = new PhysicsShapeQueryParameters2D()
        {
          ShapeRid = colliderShape
        };

        space = PhysicsServer2D.SpaceGetDirectState(GetWorld2D().Space);
        
        param = collQueryParams;
        
        CallDeferred("TestCollisionSpeed");

    }

    public override void _PhysicsProcess(double delta)
    {
        
        
    }

    public void TestCollisionSpeed()
    {
        var time = Time.GetTicksMsec();
        var startY = 0;
        var endY = 0;

        // y range 0 - 1000
        // x end = 1500

       
        List<(Vector2, Vector2)> rayList = new();
        

        // var transform = Transform2D.Identity;
        for (int i = 0; i < 200; i++)
        {
            var newArea = PhysicsServer2D.AreaCreate();
            var rect = PhysicsServer2D.RectangleShapeCreate();
            
            PhysicsServer2D.ShapeSetData(rect, new Vector2(100,100));
            var randTransform = Transform2D.Identity;
            randTransform.Origin = new Vector2(GD.RandRange(0, 1000),GD.RandRange(0, 1000));

            PhysicsServer2D.AreaAddShape(newArea, rect, randTransform);
            
            var query = new PhysicsShapeQueryParameters2D();
            query.Transform = randTransform;
            query.ShapeRid = rect;
            var result = space.IntersectShape(query);
            if (result.Count != 0)
            {
                GD.Print("----");
                foreach (var dict in result)
                {
                    GD.Print(dict["collider"]);
                }

            
            }

            // GD.Print(PhysicsServer2D.GetProcessInfo(PhysicsServer2D.ProcessInfo.CollisionPairs));
            
            // var rayStartY = GD.Randf() * 1000;
            // var rayEndY = GD.Randf() * 1000;
            // var rayStart = new Vector2(0, rayStartY);
            // var rayEnd = new Vector2(1500, rayEndY);

            // if (int.IsEvenInteger(i)) (rayStart, rayEnd) = (rayEnd, rayStart);
            // var ray = PhysicsRayQueryParameters2D.Create(rayStart, rayEnd);

            // var rayIntersect = space.IntersectRay(ray);
            // if (rayIntersect.Count > 0) intersectPoints.Add((Vector2)rayIntersect["position"]);

        }

        // QueueRedraw();
        GD.Print(Time.GetTicksMsec() - time);



    }

    // public override void _Draw()
    // {
    //     foreach (var p in intersectPoints)
    //     {
    //         DrawCircle(p, 7, Colors.BlanchedAlmond);
    //     }

    // }



}
