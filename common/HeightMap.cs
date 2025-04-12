using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HeightMap : GodotObject
{
    string seed = "";
    public int width = 1;
    public float MaxHeight = 1;
    public float MinHeight = 0;
    public FastNoiseLite noise;

    List<float> pointList = []; 
    List<Vector2> pointsOfInterest = [];

// todo:
//  - map heights to value range (so no negatives, and returns useable heights)
// - allow remapping values, to increase or decrease extremity of height changes


    public HeightMap(int mapWidth, int noiseSeed)
    {
        this.width = mapWidth;
        this.noise = new FastNoiseLite
        {
            
            Frequency = 0.04f, // roughly changes slope direction every 10 units at 0.05
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = 2,
            DomainWarpFractalLacunarity = 2f,
            Seed = noiseSeed,
        };

    }

    public float getSlope(float pos)
    {
        var delta = noise.Frequency / 10;
        var valPlus = noise.GetNoise1D(pos + delta);
        var valMinus = noise.GetNoise1D(pos - delta);
        return (valPlus - valMinus) / (delta * 2);         
    }

    public float getSlopeOfSlope(float pos)
    {
        var delta = noise.Frequency / 5;
        var slopePlus = getSlope(pos + delta);
        var slopeMinus = getSlope(pos - delta);
        return (slopePlus - slopeMinus) / (delta * 2); 

    }

    public List<float> GetHeights(Vector2? remapRange = null)
    {
        List<float> list = [];

        List<Vector2> _pointsOfInterest = [];

        int firstSlopeTrend = Math.Sign(getSlope(0));
        // int secondSlopeTrend = Math.Sign(getSlopeOfSlope(0));

        for (int i = 0; i < width; i++)
        {

            var val = noise.GetNoise1D(i);
            var mappedVal = (val + 1) / 2 * (MaxHeight - MinHeight) + MinHeight;
            list.Add(mappedVal);
            
            var firstSlope = getSlope(i);
            // svar secondSlope = getSlopeOfSlope(i); 


            if (Math.Sign(firstSlope) != firstSlopeTrend)
            {
                _pointsOfInterest.Add(new Vector2(i, mappedVal));
                firstSlopeTrend *= -1;
            }

            // second slope offering too many points, put on cooldown?

            // if (Math.Sign(secondSlope) != secondSlopeTrend)
            // {
            //     _pointsOfInterest.Add(new Vector2(i, val));
            //     secondSlopeTrend *= -1;
            // }
        }

        pointsOfInterest = _pointsOfInterest;
        
        
    
        pointList = list;
        return pointList;
    }

    
    
    // vector2 X value cooresponds to index on list of heights from GetHeights() 
    public List<Vector2> GetPointsOfInterest()
    {
        if (pointsOfInterest.Count == 0) GetHeights(); // must be called to cache point list
        return pointsOfInterest;

    }




}
