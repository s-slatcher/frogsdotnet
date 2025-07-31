using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = Godot.Vector2;

public partial class PolygonInterpolation : Node2D
{

    static float Mod(float x, float m)
    {
        return (x%m + m)%m;
    }
    
    public float quadSize = 30f;

    public override void _Ready()
    {
        var polyNode = GetNode<Polygon2D>("Polygon2D");
        var poly = polyNode.Polygon;

        var interp_poly = InterpPolyEdge(poly);
        AddChild(new Polygon2D() { Polygon = interp_poly });
        // var line = GetNode<Line2D>("Line2D");
        // line.Points = interp_poly;
        // line.Width = 4f;
        // for (int i = 1; i < interp_poly.Length; i++)
        // {
        //     var line = new Line2D()
        //     {
        //         Points = [interp_poly[i], interp_poly[i - 1]],
        //         Width = 4f,
        //         Modulate = new Godot.Color(GD.Randf(), GD.Randf(), GD.Randf(), 1),
        //         Position = new Vector2(500,100),
        //     };
        //     AddChild(line);
        // }

    }


    Vector2[] InterpPolyEdge(Vector2[] polygon)
    {
        var newPoly = new List<Vector2>();
        var len = polygon.Length;

        for (int i = 0; i < len; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[i == len - 1 ? 0 : i + 1];

            var dirVec = p2 - p1;

            // convert line into a range of x-y values from lowest-highest both axis
            var rangeRect = new Rect2() { Position = p1, End = p2 };
            rangeRect = rangeRect.Abs();
            var rect_pos = rangeRect.Position;
            

            // define new start point for range aligned to grid, but clamped into range
            var startPoint = new Vector2
            (
                rect_pos.X - Mod(rect_pos.X, quadSize) + quadSize,
                rect_pos.Y - Mod(rect_pos.Y, quadSize) + quadSize
            );

            GD.Print("range start: ", rect_pos, " adjusted start: ", startPoint, "   full range: ", rangeRect.Size);
            
            rangeRect.Position = startPoint;
            rect_pos = rangeRect.Position;
            var range = rangeRect.Size;

            var intervals = new Vector2I((int)(range.X / quadSize), (int)(range.Y / quadSize));

            var newPoints = new List<Vector2>() { p1 };

            

            for (int k = 0; k < intervals.X; k++)
            {
                var interval_add = k * quadSize;
                var x_val = startPoint.X + interval_add;
                var vec = p2 - p1;
                var x_vec = x_val - p1.X;
                var ratio = x_vec / vec.X;
                var vec_point = ratio * vec;
                var trans_point = vec_point + p1;
                newPoints.Add(trans_point);
            }
            for (int k = 0; k < intervals.Y; k++)
            {
                var interval_add = k * quadSize;
                var y_val = startPoint.Y + interval_add;
                var vec = p2 - p1;
                var y_vec = y_val - p1.Y;
                var ratio = y_vec / vec.Y;
                var vec_point = ratio * vec;
                var trans_point = vec_point + p1;
                newPoints.Add(trans_point);
            }

            newPoints = newPoints.OrderBy(p => p.DistanceSquaredTo(p1)).ToList();
            newPoly.AddRange(newPoints);
        }
        return newPoly.ToArray();
    }
}
