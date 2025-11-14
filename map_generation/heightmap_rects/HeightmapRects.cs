using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

public partial class HeightmapRects : GodotObject
{   
    public struct Platform(Vector2 leftEdge, float width)
    {
        public Vector2 LeftEdge = leftEdge;
        public float Width = width;
    }

    List<Rect2> landmassRects = new();
    List<Vector2> pointList = new();

    public FastNoiseLite Noise = GD.Load<FastNoiseLite>("uid://cvdhvoshnaiqt");  // "landmass_generation_noise"

    public float Epsilon = 0.025f;

    
    public float Jaggedness = 1;
    public float minimumWidth = 3;
    public float Height = 50;
    public int NoiseSeed = 1;

    

    public int PointsPerUnit = 2;


    public List<Rect2> GetRects(Vector2 Range)
    {
        UpdateFrequency();
        GenerateRects(Range);
        return landmassRects;
    }

    private void UpdateFrequency()
    {
        Noise.Frequency = (1 / Height) * Jaggedness;
    }


    private void GenerateRects(Vector2 range)
    {
        
        var gUtils = new GeometryUtils();

        var startX = range.X;
        var endX = range.Y;
        if (startX > endX) { var temp = endX; endX = startX; startX = temp; }
        
        var heightMap = GetHeightMap(range);

        var simpleMap = gUtils.SimplifyPolygon(heightMap.ToArray(), Epsilon);

        var rectList = new List<Rect2>();

        int skippedPoints = 0;

        for (int i = 1; i < simpleMap.Length; i++)
        {
            var point = simpleMap[i];
            var lastPoint = simpleMap[i - 1];

            var position = new Vector2(lastPoint.X, 0);
            var width = point.X - lastPoint.X;

            if (width < minimumWidth)
            {
                skippedPoints++;
                simpleMap[i] = simpleMap[i - 1];
                continue;
            }

            var pointVal = (point.Y + lastPoint.Y) / 2;
            var normalizedHeight = (pointVal + 1) / 2;
            var height = (float)normalizedHeight * Height;
            var rect = new Rect2(position, new Vector2(width, height));
            rectList.Add(rect);

        }

        if (skippedPoints > 0) GD.Print("rects that failed min width check: ", skippedPoints);

        landmassRects = rectList;
    }



    List<Vector2> GetHeightMap(Vector2 range)
    {
        var sampleCount = (range.Y - range.X) * PointsPerUnit;
        pointList = new();
        for (int i = 0; i < sampleCount; i++)
        {
            var x = (i / PointsPerUnit) + range.X;
            var y = Noise.GetNoise1D(x);
            var point = new Vector2(x, y);
            pointList.Add(point);

        }
        return pointList;

    }


}
