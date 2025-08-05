using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;

public partial class HeightMap : GodotObject
{
    string seed = "";
    
    // number of noise points sampled per 1/frequency units (1 period, sort of) of the noise map.
    private const int samplesPerPeriod = 40;    

    public float domainPosition = 0;
    
    public float MaxHeight = 1;
    public float MinHeight = 0;
    public int NoiseLayerTotal;
    List<FastNoiseLite> NoiseLayers = new();
    public float LayerFrequencyMultiplier;
    public float LayerGain;
    public bool RemapToHeightRange = false;



    public HeightMap(int noiseSeed, float noiseFrequency = 0.01f, int noiseLayers = 2, float layerFrequencyMultiplier = 2f, float layerGain = 0.5f )
    {
        this.NoiseLayerTotal = noiseLayers;
        this.LayerFrequencyMultiplier = layerFrequencyMultiplier;
        this.LayerGain = layerGain;

        for (int i = 0; i < noiseLayers; i++)
        {
             
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


    public List<Vector2> GetNextHeights(float width)
    {
        
        List<Vector2> _pointsOfInterest = [];


        var highestFreq = NoiseLayers[NoiseLayers.Count - 1].Frequency;
        var noisePeriod = 1 / highestFreq;
        var sampleTotal = width / noisePeriod * samplesPerPeriod;
        var sampleGap = width / sampleTotal;
        
        var slopeTrends = NoiseLayers.Select( noise => Math.Sign( getSlope(noise, 0))).ToList();
        
        float strengthDivisor = 0;
        for (int i = 0; i < NoiseLayers.Count; i++) strengthDivisor += (float)Math.Pow(LayerGain, i);
        
        var minValue = float.MaxValue;
        var maxValue = float.MinValue;

        for (int i = 0; i < sampleTotal; i++)
        {   
            var noisePos = sampleGap * i;
            float combinedHeight = 0;
            
            bool isPointOfInterest = false;

            for (int j = 0; j < NoiseLayers.Count; j++)
            {
                var noiseLayer = NoiseLayers[j];
                var layerStrength = (float) Math.Pow(LayerGain, j);

                var value = noiseLayer.GetNoise1D(noisePos + domainPosition);

                combinedHeight += value * layerStrength;
                
                var slope = getSlope(noiseLayer, noisePos);
                if (Math.Sign(slope) != slopeTrends[j])
                {
                    slopeTrends[j] *= -1;
                    isPointOfInterest = true;
                }
            }
            
            combinedHeight /= strengthDivisor;
            var mappedValue = (combinedHeight + 1) / 2 * (MaxHeight - MinHeight) + MinHeight;
            
            minValue = Math.Min(minValue, mappedValue);
            maxValue = Math.Max(maxValue, mappedValue);

            
            var mapVector = new Vector2(noisePos, (float) mappedValue);
            
            
            if (isPointOfInterest) _pointsOfInterest.Add(mapVector);

        }

        if (RemapToHeightRange) _pointsOfInterest = Remap(_pointsOfInterest, minValue, maxValue);


        domainPosition += width;
        return _pointsOfInterest;
    }


    List<Vector2> Remap(List<Vector2> points, float sampleMin, float sampleMax)
    {
        var newPoints = new List<Vector2>();

        for (int i = 0; i < points.Count; i++)
        {
            var pointHeight = points[i].Y;
            var remapHeight = (pointHeight - sampleMin) / (sampleMax - sampleMin) * (MaxHeight - MinHeight) + MinHeight;
            newPoints.Add(new Vector2(points[i].X, remapHeight));
        }
        return newPoints;

    }
    
    

}
