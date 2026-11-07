    using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
public partial class NewTerrainTesting : Node3D
{
    
    [Export] Node PolygonNodeContainer;
    [Export] ShaderMaterial material;
    [Export] PackedScene terrainScene;

    [Export] public float MaxTerrainHeight = 30;
    [Export] public int TotalLandmasses = 1;
    [Export] public float RoundingDistance = 1.5f;
    [Export] public float RoundingToleranceDegrees = 8f;
    [Export] public float SubdivideTargetArea = 1;

    [Export] public bool GenerateTerrainButton
    {
        get {return false;}
        set {OnGenerateTerrain();}
    }

    GeometryUtils gu = new();

    public override void _Ready()
    {
        // OnGenerateTerrain();  
    }

    public void OnGenerateTerrain()
    {
        var time = Time.GetTicksMsec();
        ClearTerrain();
        GenerateTerrain();
        // GD.Print("NewTerrainTesting   time to gen: ", Time.GetTicksMsec() - time);
    }

    private void ClearTerrain()
    {
        foreach (var child in GetChildren()) if (child is TerrainChunk) child.Free();
    }


    public void GenerateTerrain()
    {
        //generate a series of box polygons of various heights

        var currentPos = new Vector2();
        var gu = new GeometryUtils();
        
        for (int i = 0; i < TotalLandmasses; i++)
        {
            var rect = new Rect2(){
                Position = currentPos, 
                Size = new Vector2(GD.RandRange(5,5), (float)GD.RandRange(5, MaxTerrainHeight) ) 
            };
            var poly = gu.PolygonFromRect(rect);
            var norm = new NormalPoly(poly, 0);
            GeneratePolyTerrainChunk(norm);
            // meshInst.MaterialOverride = material;
            currentPos += new Vector2(rect.Size.X *  (1- GD.Randf() /3), 0);
        }


        
    }



    public void GeneratePolyTerrainChunk(NormalPoly norm)
    {
        
        var terrainChunk = (TerrainChunk)terrainScene.Instantiate();
        AddChild(terrainChunk);
        terrainChunk.TargetSubdivideArea = SubdivideTargetArea;
        terrainChunk.RoundingDistance = RoundingDistance;
        terrainChunk.RoundingToleranceDegrees = RoundingToleranceDegrees;
        terrainChunk.Position = new Vector3(norm.Position.X, norm.Position.Y, 0);

        var time = Time.GetTicksMsec();
        terrainChunk.SetPolygon(norm);
        GD.Print(Time.GetTicksMsec() - time);

    }

}
