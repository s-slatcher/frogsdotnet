using Godot;
using System;

public partial class BaseTerrainMesh : Node3D
{

    [Export] PolygonMesh polyMesh;
    [Export] Curve BaseWidthCurve;
    [Export] Curve BaseHeightCurve;

    public float MaxDepth = 5;
    public float MinDepth = 5;
    public float QuadDensity = 0.25f;

    internal TerrainPolygon terrainPoly;
    public TerrainPolygon TerrainPolygon
    {
        get { return terrainPoly; }
        set
        {
            terrainPoly = value;
            SetMesh();
        }
    }


    

    public override void _Ready()
    {
        polyMesh.DefaultDepth = MaxDepth;
        polyMesh.QuadDensity = QuadDensity;

    }
   
    public void ExplodeTerrain(Vector3 position, float radius)
    {
        polyMesh.ExplodeTerrain(position, radius);
    }
   
    internal virtual void SetMesh()
    {
        polyMesh.GenerateMesh(terrainPoly.Polygon);

    }
    
   

    

}
