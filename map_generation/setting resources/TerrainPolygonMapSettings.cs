using Godot;
using System;


public partial class TerrainPolygonMapSettings : Resource
{

    [Export] public int Seed;
    // loads "noise_edge_poly_noise.tres" by default
    [ExportGroup("height map settings")]
    [Export] public FastNoiseLite HeightMapNoise = GD.Load<FastNoiseLite>("uid://cvdhvoshnaiqt");  // "landmass_generation_noise"
    [Export] public float MaxHeight = 60;
    [Export] public float Jaggedness = 1;
    [Export(PropertyHint.Range, "0.001, 0.05, 0.001")] public float SmoothingEpsilon = 0.025f;

    [ExportGroup("edge definition")]
    [Export] public FastNoiseLite EdgeNoise = GD.Load<FastNoiseLite>("uid://dw8jx8pbm8eps");
    [Export] public float NoiseHeightCutoff = 0.5f;
    [Export] public float MaxYComponentForNoise = 0.75f;
    [Export] public float NoiseClampingFactor = 0.8f; // ~0.8 limits clamping but seems to prevent most noise line overlaps
    [Export] public float MeanCliffGrade = float.Pi / 15;
    [Export] public float BottomPointHeight = 0;
    [Export] public float BottomPlatformWidth = 0;
    [Export] public float TargetCurveBuffer = 1;  
    [Export] public float TargetNoiseHeight = 3f;

    [ExportGroup("caves")]
    [Export] public float MinCaveHeight = 12;
    [Export] public float MaxCaveHeight = 20;
    [Export] public float MinHeightAllowance = 30;
    [Export] public float BaseCaveChance = 0.5f;
    [Export] public float MinCaveDepth = 5;
    [Export] public float MaxCaveDepth = 10;
    [Export] public float CaveInsetPercentage = 0.75f;

}
