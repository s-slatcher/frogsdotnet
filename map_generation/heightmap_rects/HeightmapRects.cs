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

    FastNoiseLite HeightNoise = GD.Load<FastNoiseLite>("uid://cvdhvoshnaiqt");

    [Export] float Epsilon = 0.015f;
    [Export]
    public Vector2 Range
    {
        get => range;
        set
        {
            range = value;
            if( IsNodeReady() ) GenerateLandmassRects();
        }
    }

    


    List<List<Platform>> HeightPlatformGroups = new();

    [Export]
    bool AutoUpdate = false;

    int GroupingLayers = 3;

    float InitialTolerance = 1f/10f;

    float toleranceMultiplier = 1.1f;

     
    [Export] public float Amplitude = 50;

    [Export] public int NoiseSeed = 1;

    [Export] public bool RemapValues = true;


    public int PointsPerUnit = 1;

    public override void _Ready()
    {
        // Visible = false;    
        GenerateRectsNew();
    }

    public List<Rect2> GetRects()
    {
        if (landmassRects.Count == 0) GenerateRectsNew(); // GenerateLandmassRects();
        return landmassRects; 
    }

    void SetNoise()
    {
        HeightNoise.Seed = NoiseSeed;
    }


    public override void _PhysicsProcess(double delta)
    {
        // if (AutoUpdate)
        // {
        //     GenerateRectsNew();
        //     UpdateDisplay();
        // }
    }

    private void UpdateDisplay()
    {
        if (!IsNodeReady()) return;
        var line = GetNode<Line2D>("Line2D");
        line.Points = [];
        foreach (var point in pointList)
        {
            line.AddPoint((point + new Vector2(0,1) )* new Vector2(1, Amplitude / 2 ));
        }

        foreach (var node in displayNodes) node.QueueFree();
        foreach (var rect in landmassRects)
        {
            var node = new Polygon2D();
            node.Scale = new Vector2(1, -1);
            node.Polygon = new GeometryUtils().PolygonFromRect(rect);
            AddChild(node);
        }

        

    }



    private void GenerateRectsNew()
    {
        var gUtils = new GeometryUtils();

        SetNoise();
        var startX = range.X;
        var endX = range.Y;
        GD.Print("endx ", endX);
        if (startX > endX) (endX, startX) = (startX, endX);

        var heightMap = GetHeightMap();

        var simpleMap = gUtils.SimplifyPolygon(heightMap.ToArray(), Epsilon);

        GD.Print("simplfied length ", simpleMap.Length);
        var rectList = new List<Rect2>();

        for (int i = 1; i < simpleMap.Length; i++)
        {
            var point = simpleMap[i];
            var lastPoint = simpleMap[i - 1];

            var position = new Vector2(lastPoint.X, 0);
            var width = point.X - lastPoint.X;
            var pointVal = point.Y;
            var extremeVal = Math.Sqrt(Math.Abs(pointVal)) * Math.Sign(pointVal);
            var normalizedHeight = (extremeVal + 1) / 2;
            var height = (float) normalizedHeight * Amplitude;
            

            var rect = new Rect2(position, new Vector2(width, height));
            rectList.Add(rect);

        }
        
        landmassRects = rectList;
        GD.Print("rect list size:L ", landmassRects.Count);
        UpdateDisplay();
    }

    void GenerateLandmassRects()
    {

        SetNoise();
        var startX = range.X;
        var endX = range.Y;
        if (startX > endX) (endX, startX) = (startX, endX);


        var initPlatList = GetInitialPlatformList(startX, endX);

        HeightPlatformGroups = [initPlatList];
        
        var loopTolerance = InitialTolerance;
        for (int i = 1; i < GroupingLayers; i++)
        {
            var lastGroup = HeightPlatformGroups[i - 1];
            loopTolerance *= toleranceMultiplier;
            var group = GroupPlatforms(lastGroup, loopTolerance);
            HeightPlatformGroups.Add(group);
        }


        var finalGroup = HeightPlatformGroups[^1];
        var minVal = 100f;
        landmassRects = new();
        foreach (var plat in finalGroup)
        {
            var leftEdge = plat.LeftEdge;
            minVal = Math.Min(minVal, leftEdge.Y);
            var width = plat.Width;

            var remappedEdge = (leftEdge + new Vector2(0, 2)) * new Vector2(1, Amplitude / 2);
            var rectPos = new Vector2(remappedEdge.X, 0);
            var rectSize = new Vector2(width, remappedEdge.Y);

            var landRect = new Rect2(rectPos, rectSize);
            landmassRects.Add(landRect);
        }


        // UpdateDisplay();
        
    }
    
    public List<Platform> GroupPlatforms(List<Platform> platList, float heightTolerance)
    {
        var groups = new List<List<Platform>>();
        var groupAverages = new List<float>();

        var currentGroup = new List<Platform>() { platList[0] };


        var groupMin = platList[0].LeftEdge.Y;
        var groupMax = platList[0].LeftEdge.Y;


        for (int i = 1; i < platList.Count; i++)
        {
            var plat = platList[i];
            var height = plat.LeftEdge.Y;

            groupMin = Math.Min(groupMin, height);
            groupMax = Math.Max(groupMax, height);

            if (groupMax - groupMin < heightTolerance)
            {
                currentGroup.Add(plat);
            }
            else
            {
                var lastPlat = platList[i - 1];
                var lastHeight = lastPlat.LeftEdge.Y;

                var delta = float.Abs(lastHeight - height);

                var lastGroupAvg = float.Lerp(groupMin, groupMax, 0.5f);



                // start new group with next point, check if last point would fit better in new group
                if (delta / 2 < float.Abs(lastGroupAvg - lastPlat.LeftEdge.Y) && currentGroup.Count > 1)
                {
                    // GD.Print("shifted last point to next group");
                    currentGroup.RemoveAt(currentGroup.Count - 1);
                    groups.Add(currentGroup);
                    currentGroup = new() { lastPlat, plat };
                    groupMin = Math.Min(height, lastHeight);
                    groupMax = Math.Max(height, lastHeight);
                }
                else
                {
                    groups.Add(currentGroup);
                    currentGroup = new() { plat };
                    groupMin = groupMax = height;
                }
            }
        }

        groups.Add(currentGroup);

        var newPlatList = new List<Platform>();


        for (int i = 0; i < groups.Count; i++)
        {
            var groupedPlats = groups[i];
            var firstPlat = groupedPlats[0];
            var lastPlat = groupedPlats[^1];

            var groupX = firstPlat.LeftEdge.X;
            var groupWidth = lastPlat.LeftEdge.X + lastPlat.Width - firstPlat.LeftEdge.X;

            var avgY = groupedPlats.Aggregate(0f, (acc, plat) => acc += plat.LeftEdge.Y / groupedPlats.Count);


            var newPlat = new Platform(new Vector2(groupX, avgY), groupWidth);

            newPlatList.Add(newPlat);

        }

        return newPlatList;

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

    List<Platform> GetInitialPlatformList(float startX, float endX)
    {
        var platList = new List<Platform>();

        var sampleCount = (endX - startX) * PointsPerUnit;

        

        for (int i = 0; i < sampleCount; i++)
        {
            var x = (i / PointsPerUnit) + startX;
            var y = HeightNoise.GetNoise1D(x);

            var ySign = Math.Sign(y);
            var extremeY = float.Sqrt(float.Abs(y)) * ySign;

            var point = new Vector2(x, extremeY);

            var plat = new Platform(point, 1 / PointsPerUnit);
            platList.Add(plat);

        }

        return platList;

    }

    

}
