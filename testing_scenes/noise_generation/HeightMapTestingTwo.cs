using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class HeightMapTestingTwo : Node2D
{




    List<List<Platform>> HeightPlatformGroups = new();
   
    [Export(PropertyHint.Range, "1,10,1")]
    int GroupingLayers { get; set; } = 3;

    [Export(PropertyHint.Range, "0.001, 0.5, 0.01")]
    float InitialTolerance = 1f/10f;

    [Export(PropertyHint.Range,"1, 8, 0.05")]
    float toleranceMultiplier = 1.5f;

     
    [Export]
    float amplitude = 50;

    [Export]
    float LineWidth = 1f;

    [Export]
    public int noiseSeed = 1;

    [Export]
    public bool MaintainHeightRange = true;

    [Export]
    public float WidthDeleteThreshold = 10;

    public struct Platform(Vector2 leftEdge, float width)
    {
        public Vector2 LeftEdge = leftEdge;
        public float Width = width;
    }

    public override void _Ready()
    {
        GenerateHeightMaps();
    }

    
    public void GenerateHeightMaps()
    {

        var noiseTex = (NoiseTexture2D)GD.Load("uid://cjyk0mpa11tnb");
        var noise = (FastNoiseLite)noiseTex.Noise;
        noise.Seed = noiseSeed;

        HeightPlatformGroups = new();
        var initialPlatformList = new List<Platform>();

        var mapLength = 500;
        var sampleGap = 1;
        int pointCount = (int)mapLength / sampleGap;

        float max = -1;
        float min = 1;

        for (int i = 0; i < pointCount; i++)
        {
            var x = i * sampleGap;
            var height = noise.GetNoise1D(x);

            max = Math.Max(max, x);
            min = Math.Min(min, x);

            initialPlatformList.Add(new(new Vector2(x, height), sampleGap));
        }

        HeightPlatformGroups.Add(initialPlatformList);

        var heightRange = new Vector2(min, max);
        var loopTolerance = InitialTolerance;

        for (int i = 1; i < GroupingLayers; i++)
        {
            var lastGroup = HeightPlatformGroups[i - 1];
            loopTolerance *= toleranceMultiplier;
            var group = GroupPlatforms(lastGroup, loopTolerance, heightRange);
            HeightPlatformGroups.Add(group);
        }


    }

    public List<Platform> GroupPlatforms(List<Platform> platList, float heightTolerance, Vector2 heightRange)
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

            var groupHeightRange = GetGroupHeightRange(groupedPlats);

            var groupX = firstPlat.LeftEdge.X;
            var groupWidth = lastPlat.LeftEdge.X + lastPlat.Width - firstPlat.LeftEdge.X;

            var avgY = groupedPlats.Aggregate(0f, (acc, plat) => acc += plat.LeftEdge.Y / groupedPlats.Count);
            
            // 0 - 1 range reflecting how thes groups average height compares to total height map range
            var heightFactor = (avgY - heightRange.X) / (heightRange.Y - heightRange.X);
            var groupY = float.Lerp(groupHeightRange.X, groupHeightRange.Y, heightFactor);

            if (!MaintainHeightRange) groupY = avgY;  

            var newPlat = new Platform(new Vector2(groupX, groupY), groupWidth);

            newPlatList.Add(newPlat);

        }

        return newPlatList;

    }

    public Vector2 GetGroupHeightRange(List<Platform> platformList)
    {
        float max = -10;
        float min = 10;
        foreach (var p in platformList)
        {
            max = Math.Max(max, p.LeftEdge.Y);
            min = Math.Min(min, p.LeftEdge.Y);
        }

        return new Vector2(min, max);

    }

    public override void _PhysicsProcess(double delta)
    {
        GenerateHeightMaps();
        QueueRedraw();
    }

    public override void _Draw()
    {
        // foreach (var p in PointPositions)
        // {
        //     DrawCircle(p, 1.5f, Colors.Red);
        // }
        var yOffset = amplitude * 1.5f;
        var rng = new RandomNumberGenerator();
        rng.Seed = 1;

        for (int i = 0; i < HeightPlatformGroups.Count; i++)
        {
            var offsetVec = new Vector2(0, i * yOffset);
            Vector2 lastPos = Vector2.Zero;
            foreach (var plat in HeightPlatformGroups[i])
            {
                var lineColor = Colors.Red;
                var amplitudeVec = new Vector2(1, amplitude);
                var platStart = plat.LeftEdge * amplitudeVec;
                var platEnd = platStart + new Vector2(plat.Width, 0);

                if (plat.Width < WidthDeleteThreshold && i == HeightPlatformGroups.Count - 1)
                {
                    float randTarget = rng.Randf();
                    float skipFactor = plat.Width / WidthDeleteThreshold;
                    // skipFactor = float.Sqrt(skipFactor);
                    if (skipFactor < randTarget)
                    {
                        lineColor = Colors.Pink;
                        continue;
                    }

                }

                if (lastPos != Vector2.Zero) DrawLine(platStart + offsetVec, lastPos, lineColor, LineWidth);
                DrawLine(platStart + offsetVec, platEnd + offsetVec, lineColor, LineWidth);
                lastPos = platEnd + offsetVec;
            }

        }

        
        
    }


}
