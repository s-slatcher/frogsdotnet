using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;

public partial class HeightMap : GodotObject
{
    string seed = "";
    
    // number of noise points sampled per 1/frequency units (1 pseudo-period) of the noise map.
    private const int samplesPerPeriod = 40;    

    public float width = 1;
    public float MaxHeight = 1;
    public float MinHeight = 0;
    public int NoiseLayerTotal;
    List<FastNoiseLite> NoiseLayers = new();
    public float LayerFrequencyMultiplier;
    public float LayerGain;
    public float ExponentialFactor = 1f;

     
    List<Vector2> pointList = []; 
    List<Vector2> pointsOfInterest = [];



    public HeightMap(float mapWidth, int noiseSeed, float noiseFrequency = 0.01f, int noiseLayers = 2, float layerFrequencyMultiplier = 2f, float layerGain = 0.5f )
    {
        this.width = mapWidth;
        this.NoiseLayerTotal = noiseLayers;
        this.LayerFrequencyMultiplier = layerFrequencyMultiplier;
        this.LayerGain = layerGain;

        for (int i = 0; i < noiseLayers; i++)
        {
            // calc frequency now (to bake in noise gen) but layer strength applied when combining values  
            var layerFrequency = noiseFrequency * Math.Pow(layerFrequencyMultiplier, i);
            var noise = new FastNoiseLite()
            {
                FractalType = FastNoiseLite.FractalTypeEnum.None,
                Frequency = (float)layerFrequency,
                Seed = noiseSeed + i,
            };

            NoiseLayers.Add(noise);

        }

    }

    public float getSlope(FastNoiseLite noise, float pos)
    {
        var delta = noise.Frequency / 10;
        var valPlus = noise.GetNoise1D(pos + delta);
        var valMinus = noise.GetNoise1D(pos - delta);
        return (valPlus - valMinus) / (delta * 2);         
    }


    public List<Vector2> GetHeights()
    {
        List<Vector2> points = [];
        List<Vector2> _pointsOfInterest = [];


        var highestFreq = NoiseLayers[NoiseLayers.Count - 1].Frequency;
        var noisePeriod = 1 / highestFreq;
        var sampleTotal = (int) ( width / noisePeriod * samplesPerPeriod );
        var sampleGap = width / sampleTotal;
        
        var slopeTrends = NoiseLayers.Select( noise => Math.Sign( getSlope(noise, 0))).ToList();
        
        float strengthDivisor = 0;
        for (int i = 0; i < NoiseLayers.Count; i++) strengthDivisor += (float)Math.Pow(LayerGain, i);
        

        for (int i = 0; i < sampleTotal; i++)
        {   
            var noisePos = sampleGap * i;
            float combinedHeight = 0;
            
            bool isPointOfInterest = false;

            for (int j = 0; j < NoiseLayers.Count; j++)
            {
                var noiseLayer = NoiseLayers[j];
                var layerStrength = (float) Math.Pow(LayerGain, j);
                var value = noiseLayer.GetNoise1D(noisePos);
                combinedHeight += value * layerStrength;
                
                var slope = getSlope(noiseLayer, noisePos);
                if (Math.Sign(slope) != slopeTrends[j])
                {
                    slopeTrends[j] *= -1;
                    isPointOfInterest = true;
                }
            }
            
            combinedHeight /= strengthDivisor;
            var exponentValue = Math.Sign(combinedHeight) * Math.Pow( Math.Abs(combinedHeight), ExponentialFactor); 
            var mappedValue = (exponentValue + 1) / 2 * (MaxHeight - MinHeight) + MinHeight;
            var mapVector = new Vector2(noisePos, (float) mappedValue);
            
            points.Add(mapVector);
            if (isPointOfInterest) _pointsOfInterest.Add(mapVector);

        }

        pointsOfInterest = _pointsOfInterest;
    
        pointList = points;
        return pointList;
    }

    
    
    // vector2 X value cooresponds to index on list of heights from GetHeights() 
    public List<Vector2> GetPointsOfInterest()
    {
        GetHeights(); // called again in case noise values have changed, to update points of interest
        return pointsOfInterest;

    }




}
