using Godot;
using System;
using System.Linq;
using System.Security.Principal;

public partial class PolygonShrink : Node2D
{
    Polygon2D polyNode;

    public override void _Ready()
    {
        polyNode = GetNode<Polygon2D>("Polygon2D");
        var poly = polyNode.Polygon;

        // var expandPoly = Geometry2D.OffsetPolygon(poly, 100)[0];
        // var shrunkPoly = Geometry2D.OffsetPolygon(expandPoly, -200)[0];

        // GD.Print("poly count: ", poly.Length, "  shrunk: ", shrunkPoly.Length);
        // AddChild(new Polygon2D() { Polygon = shrunkPoly, SelfModulate = new Color(GD.RandRange(0, 1), GD.RandRange(0, 1), GD.RandRange(0, 1), 1) });

        var p1 = GetNode<Marker2D>("p1").Position;
        var p2 = GetNode<Marker2D>("p2").Position;
        var p3 = GetNode<Marker2D>("p3").Position;



        Vector2[] points = [p1, p2, p3];
        float[] point_heights = [400, 600, 240];

        var gUtils = new GeometryUtils();
        var rect = gUtils.RectIFromPolygon(poly);

        var img = Image.CreateEmpty((int)rect.Size.X, (int)rect.Size.Y, false, Image.Format.Rgb8);
        var imageTexture = new ImageTexture();

        for (int x = 0; x < rect.Size.X; x++)
        {
            for (int y = 0; y < rect.Size.Y; y++)
            {
                var px = new Vector2(x, y);
                var color = Godot.Colors.White;
                float[] distances = [float.Abs(px.X - p1.X), float.Abs(px.X - p2.X), float.Abs(px.X - p3.X)];

                int closestPoint = 0;
                int secondClosest = 1;

                for (int i = 0; i < points.Length; i++)
                {
                    var dist = distances[i];
                    if (dist < distances[closestPoint])
                    {
                        secondClosest = closestPoint;
                        closestPoint = i;
                    }
                    else if (dist < distances[secondClosest]) secondClosest = i;

                }

                // point is far enough from second closest to not be influenced by it
                var totalDist = distances[secondClosest] + distances[closestPoint];
                var close_weight = 1.0f - (distances[closestPoint] / totalDist);
                var far_weight = 1.0f - close_weight;
                if (totalDist > points[closestPoint].DistanceTo(points[secondClosest]))
                {
                    close_weight = 1; far_weight = 0;
                }

                var p = points[closestPoint];
                var close_height_score = (px.Y - p.Y) / point_heights[closestPoint];
                var far_height_score = (px.Y - points[secondClosest].Y) / point_heights[secondClosest];

                close_height_score *= close_weight;
                far_height_score *= far_weight;

                GD.Print(close_height_score + far_height_score);
                color = new Color(close_height_score + far_height_score, 0, 0);
                img.SetPixel((int)px.X, (int)px.Y, color);
            }

           

        }

        imageTexture.SetImage(img);
        var sprite = new Sprite2D() { Texture = imageTexture };
        AddChild(sprite);

    }

}
