using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;


using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;



public partial class World : Node3D
{

    [Export] PackedScene terrainUnitScene;
    TerrainUnit terrainUnit;

    GeometryUtils gUtils = new();

    public List< Task<Dictionary<Rect2, Mesh>>> meshTaskDictList = new();


    public TerrainMap terrainMap = new TerrainMap(102)
    {
        MaxHeight = 80,
        MinHeight = 15
    };


    public override void _Ready()
    {
        GetNode<LineDrawing>("LineDrawing").lineDrawn += OnLineDrawn;
        
        GenerateTerrain();

        // GetNode<Godot.Timer>("ExplodeTimer").Timeout += DrawTunnel;
        // GetTree().CreateTimer(1).Timeout += () => DisplayQuadMeshPolygons(quadMeshDistortionApplier.GetQuadMesh());
        // GetTree().CreateTimer(3).Timeout += () => distortionQueue.Add(new BaseTerrainDistorter(0.25f, new Rect2(new Vector2(50,30), new Vector2(20,20))));
        GetNode<PlaneMouseCapture>("PlaneMouseCapture").PlaneClicked += OnPlaneClicked;

    }
    
    private void OnLineDrawn(Vector3[] line)
    {
        // distortionQueue.Add(new TunnelDistorter(new Vector2(line[0].X, line[0].Y), new Vector2(line[1].X, line[1].Y), 3));
    }

    public override void _Input(InputEvent @event)
    {

    }

    private void OnPlaneClicked(Vector3 vector)
    {
        var vec2 = new Vector2(vector.X, vector.Y);
        terrainUnit.ExplodeTerrain(vec2, 6);
        // distortionQueue.Add(new TunnelDistorter(vec2, vec2, (float)GD.RandRange(4, 7)));
    }


    public override void _Process(double delta)
    {
        
        

    }
    
    
    private void GenerateTerrain()
    {
        List<Polygon2D> MapPolygonInstances = terrainMap.GenerateNext(60);
        var mapPoly = MapPolygonInstances[0];
        var polygon = mapPoly.Polygon;
        terrainUnit = terrainUnitScene.Instantiate() as TerrainUnit;
        AddChild(terrainUnit);
        
        terrainUnit.SetPolygon(polygon);

    }

    

}

