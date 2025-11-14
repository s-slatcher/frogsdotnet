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


public partial class NewNoiseEdgePolyTesting : Node2D
{
    Polygon2D polyInput;
    Polygon2D polyResult;
    Line2D outline;

    HeightmapRects heightMap = new();
    GeometryUtils gu = new();

    
    // LOADS "DefaultTerrainEdgeSettings.tres" ; resource has script TerrainEdgeSettings.cs
    [Export] TerrainPolygonMapSettings Settings;     
    
    public TerrainPolygonMap terrainMap = new();

    [Export(PropertyHint.Range, "0, 1, 0.1")] float UpdateSpeed = 0;
    [Export] bool CycleHeightMapSeed = false;
    [Export] bool CycleEdgeNoiseSeed = false;
    [Export] float Width = 100;
    [Export] bool DrawLines = false;


    double timeSinceUpdate = 0;


    public override void _PhysicsProcess(double delta)
    {
        timeSinceUpdate += delta;
        var deltaTarget = (1.05 - UpdateSpeed);

        if (timeSinceUpdate > deltaTarget && UpdateSpeed != 0)
        {
            timeSinceUpdate = 0;

            Update();
        }

    }

    private void Update()
    {

        



        // checks if polygon has self intersection without visualizing

        // var tris = Geometry2D.TriangulatePolygon(polyResult.Polygon);

        // outline.ClearPoints();


        // // draws outlines
        // if (!DrawLines) return;
        // foreach (var p in terrainPolygon)
        // {
        //     outline.AddPoint(p);
        // }
    }

    public override void _Ready()
    {

        polyInput = GetNode<Polygon2D>("P");
        polyResult = GetNode<Polygon2D>("P2");
        outline = GetNode<Line2D>("Outline");

        terrainMap.Settings = Settings;
        var poly = terrainMap.GetTerrainPolygon(new Vector2(0, Width));
        polyResult.Polygon = poly;

    }


}

