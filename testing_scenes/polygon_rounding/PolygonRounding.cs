using Godot;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class PolygonRounding : Node2D
{

    [Export(PropertyHint.Range, "0,1,")] public float EdgeRounding = 0;

    List<Vector2> vertices = new();
    List<Vector2> normals = new();

    public struct Circle(Vector2 center, float radius)
    {
        public Vector2 center = center;
        public float radius = radius;
    }
    public override void _Ready()
    {
        GenerateBoxPoints();
                

                RoundEdges();
                AddChild(new Polygon2D(){Polygon = vertices.ToArray()});
                GenerateArrows();


    }

    public override void _PhysicsProcess(double delta)
    {
        // reset 
       
        
    }


    private void RoundEdges()
    {
        // find the inscribed circle
        var gu = new GeometryUtils();
        var normPoly = new NormalPoly(vertices.ToArray(),0);
        var polyRect = normPoly.Rect;

        var center = polyRect.GetCenter();
        
        var innerCircle = new Circle(center , normPoly.Rect.Size.X / 2);
        var outerCircle = new Circle(center, polyRect.Size.X/2 * float.Sqrt(2));

        var circleRadius = (outerCircle.radius - innerCircle.radius) * (EdgeRounding) + innerCircle.radius;

            
            // var distToCenter = vert.DistanceTo(center);


            // var vec = vert - center;
            // vec /= (distToCenter / circleRadius);
            // var new_vert_pos = vec + center;

            // var angle = normals[i].AngleTo(vec);
            // var weight =  (1 + (float.Cos(4*angle))) / 2;
            // GD.Print(weight);

            // vertices[i] = (new_vert_pos * (1-weight)) + (vert * (weight));
            // normals[i] = vec.Normalized();
        

    }


    private void GenerateArrows()
    {
        var node = GetNode<Polygon2D>("ArrowPoly");
        for (int i = 0; i < vertices.Count; i++)
        {
            
            Polygon2D arrow = (Polygon2D)node.Duplicate();
            AddChild(arrow);
            arrow.Rotate((float)Math.Atan2(normals[i].Y, normals[i].X));

            arrow.Position = vertices[i];   
        }
        node.Visible = false;
    }


    private void GenerateBoxPoints()
    {

        int sides = 4;
        int pointsPerSide = 15  ;
        float sideLength = 200;
        var pointDist = sideLength/pointsPerSide;


        Vector2 startPos = new();
        Vector2 currentPos = startPos;
        List<Vector2> directions = new()
        {
          new Vector2(1,0),
          new Vector2(0,1),
          new Vector2(-1,0),
          new Vector2(0,-1)  
        };

        for (int i = 0; i < sides; i++)
        {
            var dir = directions[i];
            for (int j = 0; j < pointsPerSide; j++)
            {
                vertices.Add(currentPos + (dir * pointDist));
                currentPos = vertices[^1];

                normals.Add(dir.Rotated(-float.Pi/2));
            }


        }
    }
}
