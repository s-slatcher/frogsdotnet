using Godot;
using System;
using System.ComponentModel;
[Tool]
public partial class NewTerrainTesting : Node3D
{
    
    [Export] Node PolygonNodeContainer;
    [Export] ShaderMaterial material;
    [Export] PackedScene terrainScene;

    [Export] public float MaxTerrainHeight = 30;
    [Export] public int TotalLandmasses = 1;
    [Export] public int SubdivideLevels = 3; 

     [ExportToolButton("Generate Terrain")]
     public Callable GenerateTerrainButton => Callable.From(OnGenerateTerrain);

    GeometryUtils gu = new();

    public override void _Ready()
    {
        OnGenerateTerrain();  
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
            
            GeneratePolyTerrainChunk(rect);
            // meshInst.MaterialOverride = material;
            currentPos += new Vector2(rect.Size.X *  (1- GD.Randf() /3), 0);
        }
        
    }

    public void GeneratePolyTerrainChunk(Rect2 rect)
    {
        
        var poly = gu.PolygonFromRect(rect);
        var norm = new NormalPoly(poly, 0);
        var terrainChunk = (TerrainChunk)terrainScene.Instantiate();
        terrainChunk.DoStretchMesh = true;
        terrainChunk.MaxHeight = MaxTerrainHeight;
        terrainChunk.SubdivideLevels = SubdivideLevels;
        terrainChunk.depthAtBase = GD.RandRange(3,4);
        AddChild(terrainChunk);

        terrainChunk.SetPolygon(norm);
        terrainChunk.Position = new Vector3(norm.Position.X, norm.Position.Y, 0);

    }

}
