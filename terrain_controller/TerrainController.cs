using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel;

public partial class TerrainController : Node
{

    [Export] MapGenerator mapGenerator;
    [Export] TerrainVisualizationController terrainVisualizer; 
    [Export] PackedScene destructibleCollisionPolyScene;

    
    public override void _Ready()
    {
        GenerateTerrain();
    }

      private void GenerateTerrain()
    {
        List<NormalPoly> polygons = mapGenerator.GetPolygons();
        
        
        // logic
        foreach (var poly in polygons)
        {
            var collisionPoly = (DestructibleCollisionPolygon)destructibleCollisionPolyScene.Instantiate();
            collisionPoly.AddPolygon(poly);
            AddChild(collisionPoly);

        }

        //visuals
        foreach ( var poly in polygons)
        {
            terrainVisualizer.AddPolygon(poly);
        }
       
        

      
    }


}
