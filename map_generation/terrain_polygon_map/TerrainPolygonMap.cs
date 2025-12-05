using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using Vector2 = Godot.Vector2;


public partial class TerrainPolygonMap : GodotObject
{
    [Export] public TerrainPolygonMapSettings Settings = new();
    HeightmapRects heightMap = new();
    RandomNumberGenerator rng = new();

    float NoiseStart = 0;  // tallys up over lifetime of terrain map to avoid repeat noise on same seed


    

    public Vector2[] GetSimpleTerrainPoly(List<Rect2> platformList)
    {

        rng.Seed = (ulong)Settings.Seed;
         // convert rects to line segments;
        var platformLines = platformList.Select(rect => new LineSegment(rect.End - new Vector2(rect.Size.X, 0), rect.End)).ToList();
        var landPoly = new List<Vector2>();
        
        // set initial platform position, and final rect
        var initPlatform = new LineSegment(Vector2.Zero, Vector2.Zero);
        var lastPlatform = initPlatform;
        LineSegment finalPlat = new(new Vector2(0, 0), new Vector2(1, 0));
        platformLines.Add(finalPlat);

        // align each platform, form cliffs and caves between them
        for (int i = 0; i < platformLines.Count; i++)
        {

            // step 1: get the platform and translate it to meet previous at an angle.
            var platform = platformLines[i];
            platform = TranslatePlatform(platform, lastPlatform);
            platformLines[i] = platform;
            landPoly.Add(lastPlatform.End);
            //step 1: get height difference and check conditions for cave

            Vector2 cliffVector = platform.Start - lastPlatform.End;
            float heightDelta = cliffVector.Y;
            float absHeight = float.Abs(heightDelta);

            bool cliffSupportsCave = absHeight > Settings.MinHeightAllowance;

            if (cliffSupportsCave && rng.Randf() < Settings.BaseCaveChance)
            {

                // step 2: find cave height and floor position on cliff
                float caveHeight = rng.RandfRange(Settings.MinCaveHeight, Settings.MaxCaveHeight);
                float maxFloorPosition = (absHeight - caveHeight) / absHeight;  // limit how close cave ceiling to can be to top platform
                float caveStartProgress = float.Clamp(rng.Randfn(0.5f, 0.15f), 0, maxFloorPosition); ;  // cave floor averages in middle, usually extends to upper half of cliff 

                //step 3: establish cliff slope direction 
                Vector2 lowPos; Vector2 highPos;
                if (heightDelta > 0) (lowPos, highPos) = (lastPlatform.End, platform.Start);
                else (lowPos, highPos) = (platform.Start, lastPlatform.End);

                // step 4: set cave floor width and how far tucked into the mountain it is
                Vector2 caveCliffPosition = (highPos - lowPos) * caveStartProgress + lowPos;
                var caveDepth = rng.RandfRange(Settings.MinCaveDepth, Settings.MaxCaveDepth) * Math.Sign(heightDelta);
                var caveTranslate = (1 - Settings.CaveInsetPercentage) * caveDepth * -1;
                Vector2 caveEnd = caveCliffPosition + new Vector2(caveTranslate, 0);

                // step 5: find where cliff roof reconnects with cliff line using cave height 
                Vector2 ceilingConnectPoint = new();
                float caveHeightAsProgress = caveHeight / cliffVector.Length();
                float caveCeilingProgress = caveStartProgress + caveHeightAsProgress;
                ceilingConnectPoint = (highPos - lowPos) * caveCeilingProgress + lowPos;

                List<Vector2> cavePoints = [caveEnd, new Vector2(caveDepth, 0) + caveEnd, ceilingConnectPoint];
                if (heightDelta < 0) cavePoints.Reverse();
                landPoly.AddRange(cavePoints);

            }

            landPoly.Add(platform.Start);
            lastPlatform = platform;
        }

        // add in a bottom peak points (for floating islands)
        var width = landPoly[^1].X;
        var midPoint = new Vector2(width/2, 0);
        var platWidth = Settings.BottomPlatformWidth;
        var platHeight = Settings.BottomPointHeight;
        var bottomPlat = new LineSegment(
            new Vector2(platWidth/2, -platHeight) + midPoint,
            new Vector2(platWidth/-2, -platHeight) + midPoint
        );
        landPoly.AddRange([bottomPlat.Start, bottomPlat.End]);


        var landPolyArr = landPoly.ToArray();
        if (Geometry2D.TriangulatePolygon(landPolyArr).Length == 0) GD.Print("terrain polygon: failed to triangulate simple poly");
        return landPolyArr;
    }

  

    private LineSegment TranslatePlatform(LineSegment platform, LineSegment lastPlatform)
    {
        var heightDelta = platform.Start.Y - lastPlatform.End.Y;
        var cliffLength = heightDelta / Math.Cos(Settings.MeanCliffGrade);
        var baseWidth = (float)Math.Sqrt(cliffLength * cliffLength - heightDelta * heightDelta);
        var newPlatStart = lastPlatform.End + new Vector2(baseWidth, heightDelta);

        return new LineSegment(newPlatStart, newPlatStart + platform.DirectionVector());
    }

   
    


}

