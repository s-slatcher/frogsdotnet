using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;

public partial class DistortionUpdatingTesting : Node3D
{
    [Export] public PackedScene terrainUnitScene;

    float currentMapPos = 0;


    public TerrainMap terrain = new TerrainMap(102)
    {
        MaxHeight = 80,
        MinHeight = 15
    };

    public GeometryUtils gUtils = new();

    public override void _Ready()
    {
        GetNode<Godot.Timer>("Timer").Timeout += OnTimeout;

        GenerateTerrain();


    }

    private void OnTimeout()
    {

        GenerateTerrain(); 

    }


    private void GenerateTerrain()
    {
        List<Polygon2D> MapPolygonInstances = terrain.GenerateNext(60);
        var mapPoly = MapPolygonInstances[0];
        var polygon = mapPoly.Polygon;
        var terrainUnit = terrainUnitScene.Instantiate() as TerrainUnit;

        AddChild(terrainUnit);

        terrainUnit.SetPolygon(polygon);

        terrainUnit.Position = new Vector3(currentMapPos, 0, 0);

        var width = terrainUnit.GetRect2().Size.X;
        float randomGap = GD.RandRange(15, 35);
        currentMapPos += width + randomGap;

        GD.Print(width, " = width of terrain ");
    }
    

}
    