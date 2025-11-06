using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Reflection.Metadata.Ecma335;

public partial class HeightmapRects : Node2D
{
    public struct Platform(Vector2 leftEdge, float width)
    {
        public Vector2 LeftEdge = leftEdge;
        public float Width = width;
    }

    List<Rect2> landmassRects = new();
    List<Vector2> pointList = new();

    List<Polygon2D> displayNodes = new();

    Vector2 range = new Vector2(0, 1000);

    FastNoiseLite HeightNoise = GD.Load<FastNoiseLite>("uid://cvdhvoshnaiqt");  // "landmass_generation_noise"
    

    [Export] public bool ExtremifyHeights = false;
    [Export] public float Epsilon = 0.025f;
    [Export] public float Jaggedness = 1;
    [Export] public float minimumWidth = 3;

    public Vector2 Range
    {
        get => range;
        set
        {
            range = value;
            if( IsNodeReady() ) GenerateRects();
        }
    }

    


    List<List<Platform>> HeightPlatformGroups = new();

    [Export] bool AutoUpdate = false;
    [Export] public float Height = 50;
    [Export] public int NoiseSeed = 1;


    public int PointsPerUnit = 2;

    public override void _Ready()
    {
        // Visible = false;    
        GenerateRects();
    }

    public List<Rect2> GetRects()
    {
        GenerateRects(); 
        return landmassRects; 
    }

    void SetNoise()
    {
        HeightNoise.Seed = NoiseSeed;
        var frequency = (1 / Height ) * Jaggedness ;
        HeightNoise.Frequency = frequency;
    }


    public override void _PhysicsProcess(double delta)
    {
        // if (AutoUpdate)
        // {
        //     GenerateRectsNew();
        //     UpdateDisplay();
        // }
    }




    private void GenerateRects()
    {
        var gUtils = new GeometryUtils();

        SetNoise();

        var startX = range.X;
        var endX = range.Y;
        if (startX > endX) { var temp = endX; endX = startX; startX = temp; };

        var heightMap = GetHeightMap();

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

            if (ExtremifyHeights) pointVal = float.Sqrt(float.Abs(pointVal)) * Math.Sign(pointVal);
            var normalizedHeight = (pointVal + 1) / 2;


            var height = (float)normalizedHeight * Height;


            var rect = new Rect2(position, new Vector2(width, height));
            rectList.Add(rect);

        }

        if (skippedPoints > 0) GD.Print("rects that failed min width check: ", skippedPoints);
    
        
        landmassRects = rectList;
    }

   

    List<Vector2> GetHeightMap()
    {
        var sampleCount = (range.Y - range.X) * PointsPerUnit;
        pointList = new();
        for (int i = 0; i < sampleCount; i++)
        {
            var x = (i / PointsPerUnit) + range.X;
            var y = HeightNoise.GetNoise1D(x);
            var point = new Vector2(x, y);
            pointList.Add(point);

        }
        return pointList;

    }
    

}
