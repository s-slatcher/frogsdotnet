using Godot;
using System;
using System.Collections.Generic;

public partial class MapGenerator : Node3D
{

    // will need to again split up these settings files, since height map rects and edge noising will be separate now
    [Export] public MapSettings MapSettings;
    [Export] public TerrainPolygonMapSettings TerrainPolySettings;
    HeightmapRects heightMap = new();

    public override void _Ready()
    {

    }

    public void GenerateTerrainMap()
    {
        SetHeightMap();

        // pre-lim steps:
        // 1. get a full set of height map rects for the entire width
        // 2. split into islands (minor breaks at low points and major breaks at division levels ) 
        // 3. convert each group into a simplified map shape (cliff edges and cave insets)
        // 4. use simply polygons to find placements for complex islands (random sampling )

        var mapRange = new Vector2(0, MapSettings.Width);
        var rects = heightMap.GetRects(mapRange);
        // initialize first group with first rect
        var landGroups = GroupHeightMapRects(rects);
        
        



    }
    
    private List<List<Rect2>> GroupHeightMapRects(List<Rect2> rects)
    {
        var landGroups = new List<List<Rect2>>() { new() { rects[0] } };
        var currGroupWidth = rects[0].Size.X;

        var groupTargetWidth = MapSettings.Width / MapSettings.MajorDivisions;

        for (int i = 1; i < rects.Count; i++)
        {
            var rect = rects[i];
            var rectSize = rect.Size.X;
            if (currGroupWidth + (rectSize / 2) > groupTargetWidth)
            {
                landGroups.Add(new() { rect });
                currGroupWidth = rectSize;
            }
            else
            {
                landGroups[^1].Add(rect);
                currGroupWidth += rectSize;                
            }
        }
        

        return landGroups;
        
    } 

    private void SetHeightMap()
    {
        heightMap.Noise = TerrainPolySettings.HeightMapNoise;
        heightMap.Epsilon = TerrainPolySettings.SmoothingEpsilon;
        heightMap.Height = TerrainPolySettings.MaxHeight;
        heightMap.Jaggedness = TerrainPolySettings.Jaggedness;
    }

}
