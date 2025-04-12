using Godot;
using System;
using System.Drawing;

public partial class HeightMapTestingScene : Node2D
{
    int seed = 0;

    public override void _Ready()
    {   
        seed = (int)GD.Randi();
        var heightMap = new HeightMap(300, seed);
        heightMap.noise.Frequency = 0.025f;
        heightMap.MaxHeight = 60;
        // heightMap.MinHeight = 5;
        // heightMap.ContrastMultiplier = 1;
        // var heightFactor = 40;
        var heights = heightMap.GetHeights();
        var pointsOfInterest = heightMap.GetPointsOfInterest();
        
       

        var polyline = new Line2D();
        for (int i = 0; i < heights.Count; i++)
        {
            polyline.AddPoint(new Vector2(i, heights[i]));
        }

        var polylinePOIs = new Line2D();
        for (int i = 0; i < pointsOfInterest.Count; i++)
        {
            var pointPos = pointsOfInterest[i];
            polylinePOIs.AddPoint( new Vector2(pointPos.X, pointPos.Y)); 
            polylinePOIs.AddPoint( new Vector2(pointPos.X, pointPos.Y - 8)); 
            polylinePOIs.AddPoint( new Vector2(pointPos.X, pointPos.Y)); 
            
        }

        polylinePOIs.Width = 2f;
        polylinePOIs.Position = new Vector2(0, 100);
        polylinePOIs.Modulate = Colors.Orange;
        
        polyline.Width = 2f;
        
        AddChild(polyline);
        AddChild(polylinePOIs);

        GenerateHeightMapPolygons();
    }

    void GenerateHeightMapPolygons()
    {
        
        var heightMap = new HeightMap(300, seed);
        heightMap.noise.Frequency = 0.025f;
        heightMap.MaxHeight = 80;
        var points = heightMap.GetPointsOfInterest();

        var gu = new GeometryUtils();

        for (int i = 1; i < points.Count - 1; i++)   // range excludes first and last point
        {
            var point = points[i];
            var lastPointDist = points[i-1].X - point.X;
            var nextPointDist = points[i+1].X - point.X;
            var startCorner = new Vector2( point.X + (lastPointDist / 2) , 0);
            var endCorner = new Vector2(point.X + (nextPointDist / 2), point.Y);
            
            var rect = new Rect2(){Position = startCorner, End = endCorner};
            var poly = gu.PolygonFromRect(rect);
            var poly2d = new Polygon2D(){Polygon = poly};
            poly2d.Scale = new Vector2(1, 1);
            poly2d.Position = new Vector2(0, -90);
            AddChild(poly2d);

        }

    }


}
