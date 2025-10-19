using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public partial class TerrainMesh : Node3D
{

    [Export] TerrainTexture terrainTexture;
    [Export] PolygonMesh polyMesh;


    public enum TerrainType
    {
        Landmass,
        FloatingIsland,
    }

    public float MaxDepth = 5;
    public float MinDepth = 5;
    public float QuadDensity = 0.25f;
    public float GrassLength = 4f;

    internal Vector2[] terrainPoly = [];
    public Vector2[] TerrainPolygon
    {
        get { return terrainPoly; }
        set
        {
            terrainPoly = value;
            if (IsNodeReady()) SetMesh();
        }
    }

    public override void _Ready()
    {
        if (terrainPoly.Length != 0) SetMesh();  
    }


    private void SetMesh()
    {

        if (!IsNodeReady()) GD.PrintErr("called generate before in scene tree");



        // SetDomainDepthCurve(terrainPoly);
        // SetHeightDepthCurve(terrainPoly);
        GD.Print(terrainTexture);

        terrainTexture.SetPolygon(terrainPoly, GrassLength);
        var grass_texture = terrainTexture.GetTexture();
        var shader = (ShaderMaterial)polyMesh.MaterialOverride;
        shader.SetShaderParameter("grass_texture", grass_texture);


        polyMesh.QuadDensity = QuadDensity;
        polyMesh.MinDepth = MinDepth;
        polyMesh.GenerateMesh(terrainPoly);
        
    }

    // public void SetHeightDepthCurve(TerrainPolygon terrainPoly)
    // {

    //     polyMesh.HeightDepthCurve = GD.Load<Curve>("uid://dnobssupgeuds");
        
    //     var c = new Curve();
    //     var maxHeight = terrainPoly.BoundingRect.End.Y;

    //     // c.MinDomain = 0;
    //     // c.MaxDomain = 1;
    //     // c.MinValue = MinDepth;
    //     // c.MaxValue = MaxDepth;

    //     // highest depth value aligned with lowest height
    //     var startPoint = new Vector2(0, 1);
    //     var endPoint = new Vector2(1, 0);

    //     c.AddPoint(startPoint);
    //     c.AddPoint(endPoint);
    //     c.BakeResolution = 500;
    //     polyMesh.HeightDepthCurve = c;
    // }

    // public void SetDomainDepthCurve(TerrainPolygon terrainPoly)
    // {
    //     if (terrainPoly.SimplifiedHeightCurve == null || terrainPoly.SimplifiedHeightCurve.PointCount == 0)
    //     {
    //         polyMesh.DomainDepthCurve = null;
    //         return;
    //     }

    //     var baseCurve = terrainPoly.SimplifiedHeightCurve;
    //     var width = terrainPoly.BoundingRect.Size.X;
    //     var c = new Curve();

    //     // c.MinDomain = 0;
    //     // c.MaxDomain = baseCurve.MaxDomain;
    //     // c.MinValue = 0;
    //     // c.MaxValue = 1;


    //     var baseCurvePoints = new List<Vector2>();
    //     for (int i = 0; i < baseCurve.PointCount; i++) baseCurvePoints.Add(baseCurve.GetPointPosition(i));

    //     var newPoints = baseCurvePoints
    //         .Select(p => new Vector2(p.X / width, float.Sqrt(p.Y / baseCurve.MaxValue))).ToList();

    //     var pF = newPoints[0];
    //     var pL = newPoints[^1];

    //     var midValue = float.Sqrt(0.35f);

    //     // conditional tapering on edges of curve 
    //     var startValue = pF.Y > midValue ? float.Max(midValue, pF.Y * 0.5f) : pF.Y;
    //     var endValue = pL.Y > midValue ? float.Max(midValue, pL.Y * 0.5f) : pL.Y;

    //     newPoints.Insert(0, new Vector2(0, startValue));
    //     newPoints.Add(new Vector2(c.MaxDomain, endValue));

    //     foreach (var p in newPoints) c.AddPoint(p);
    //     c.BakeResolution = 500;
    //     polyMesh.DomainDepthCurve = c;
    // }

    public void ExplodeTerrain(Vector3 position, float radius)
    {
        polyMesh.ExplodeTerrain(position, radius);
    }
    



}
