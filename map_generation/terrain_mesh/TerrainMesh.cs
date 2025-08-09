using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TerrainMesh : Node3D
{

    public float TerrainWidth = 50;
    public float SideLength = 5;

    public float MinDepth = 2;
    public float MaxDepth = 8;

    public float QuadDensity = 0.5f;

    [Export] public Curve DepthDomainCurve;
    [Export] public Curve RangeDomainCurve;

    TerrainTexture terrainTexture;
    PolygonMesh polyMesh;


    public override void _Ready()
    {



        terrainTexture = GetNode<TerrainTexture>("TerrainTexture");
        polyMesh = GetNode<PolygonMesh>("PolygonMesh");
        polyMesh.DefaultSideLength = MinDepth;
        polyMesh.QuadDensity = QuadDensity;


    }

    public void GenerateMesh(TerrainPolygon terrainPolygon)
    {

        SetDomainDepthCurve(terrainPolygon);
        SetHeightDepthCurve(terrainPolygon);


        polyMesh.GenerateMesh(terrainPolygon.Polygon);
        terrainTexture.SetPolygon(terrainPolygon.Polygon, 5);

        var shader = (ShaderMaterial)polyMesh.MaterialOverride;
        shader.SetShaderParameter("grass_texture", terrainTexture.GetTexture());

    }

    public void SetHeightDepthCurve(TerrainPolygon terrainPoly)
    {
        var c = new Curve();
        var maxHeight = terrainPoly.BoundingRect.End.Y;

        c.MinDomain = 0;
        c.MaxDomain = maxHeight;
        c.MinValue = MinDepth;
        c.MaxValue = MaxDepth;

        // highest depth value aligned with lowest height
        var startPoint = new Vector2(0, MaxDepth);
        var endPoint = new Vector2(maxHeight, MinDepth);

        c.AddPoint(startPoint);
        c.AddPoint(endPoint);
        c.BakeResolution = 500;
        polyMesh.HeightDepthCurve = c;
    }

    public void SetDomainDepthCurve(TerrainPolygon terrainPoly)
    {
        var baseCurve = terrainPoly.SimplifiedHeightCurve;
        var c = new Curve();

        c.MinDomain = 0;
        c.MaxDomain = baseCurve.MaxDomain;
        c.MinValue = 0;
        c.MaxValue = 1;


        var baseCurvePoints = new List<Vector2>();
        for (int i = 0; i < baseCurve.PointCount; i++) baseCurvePoints.Add(baseCurve.GetPointPosition(i));

        var newPoints = baseCurvePoints.Select(p => new Vector2(p.X, float.Sqrt(p.Y / baseCurve.MaxValue))).ToList();

        var pF = newPoints[0];
        var pL = newPoints[^1];

        var midValue = float.Sqrt(0.5f);

        // conditional tapering on edges of curve 
        var startValue = pF.Y > midValue ? float.Max(midValue, pF.Y * 0.5f) : pF.Y;
        var endValue = pL.Y > midValue ? float.Max(midValue, pL.Y * 0.5f) : pL.Y;

        newPoints.Insert(0, new Vector2(0, startValue));
        newPoints.Add(new Vector2(c.MaxDomain, endValue));

        foreach (var p in newPoints) c.AddPoint(p);
        c.BakeResolution = 500;
        polyMesh.DepthMultiplierDomainCurve = c;
    }

    public void ExplodeTerrain(Vector3 position, float radius)
    {
        polyMesh.ExplodeTerrain(position, radius);
    }
    



}
