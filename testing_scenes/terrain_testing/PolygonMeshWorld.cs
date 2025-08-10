using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vector2 = Godot.Vector2; 
using Vector3 = Godot.Vector3; 

public partial class PolygonMeshWorld : Node3D
{
    [Export] PackedScene explodeScene;
    [Export] PackedScene terrainMeshScene;
    TerrainMesh terrainMesh;


    Vector2 radiusRange = new Vector2(2, 8);

    Dictionary<Rect2, TerrainMesh> TerrainRegionMap = new();

    public override void _Ready()
    {

        PlaneMouseCapture planeCap = GetNode<PlaneMouseCapture>("PlaneMouseCapture");
        planeCap.PlaneClicked += OnPlaneClicked;

        PopulateMap();

        // var time = Time.GetTicksMsec();

        // var terrain = new TerrainMap(15);
        // terrain.MinHeight = 10;
        // terrain.MaxHeight = 100;
        // var terrainPolygon = terrain.GenerateNextTerrainPolygon(100);
        // // var poly = terrainPolygon.Polygon;
        // // var curve = terrainPolygon.SimplifiedHeightCurve;

        // // var polygon = new GeometryUtils().PolygonFromRect(new Rect2(Vector2.Zero, new Vector2(20, 20)));

        // terrainMesh = GetNode<TerrainMesh>("TerrainMesh");
        // terrainMesh.SideLength = 5;
        // // terrainMesh.DepthDomainCurve = terrainPolygon.SimplifiedHeightCurve;
        // terrainMesh.GenerateMesh(terrainPolygon);

        // GD.Print("TERRAIN GENERATED: ", Time.GetTicksMsec() - time);


        // AddChild(meshInst);

    }

    private void OnPlaneClicked(Vector3 vector)
    {
        var randRadius = (float)GD.RandRange(radiusRange.X, radiusRange.Y);
        var center2d = new Vector2(vector.X, vector.Y);
        var explodeRect = new Rect2(Vector2.Zero, new Godot.Vector2(randRadius * 2, randRadius * 2));
        explodeRect.Position = center2d - new Vector2(randRadius, randRadius);

        foreach (var rect in TerrainRegionMap.Keys)
        {
            var mesh = TerrainRegionMap[rect];
            var newRect = new Rect2(rect.Position + new Vector2(mesh.Position.X, mesh.Position.Y) , rect.Size);

            if (explodeRect.Intersects(newRect))
            {
                GD.Print("explosion intersected");
                mesh.ExplodeTerrain(vector, randRadius);
            }

        }

        var explosion = (Node3D)explodeScene.Instantiate();
        explosion.Position = vector + new Vector3(0, 0, 1);
        explosion.Scale = new Vector3(1, 1, 1) * randRadius;

        AddChild(explosion);

    }


    public override void _Process(double delta)
    {
        // float dt = (float)delta;
        // var randPos = new Vector3(GD.RandRange(0, 150), GD.RandRange(0, 100), 0);
        // var radiusAdd = (1.0 - randPos.Y / 100) * 5;
        // radiusAdd = float.Clamp((float)radiusAdd, 1, 10/3);
        // terrainMesh.ExplodeTerrain(
        //     randPos, (float)GD.RandRange(1 * radiusAdd, 3 * radiusAdd)
        // );
    }


    public void PopulateMap()
    {
        int numOfIslands = 3;
        float islandWidth = 45;

        var terrain = new TerrainMap(15);
        terrain.MinHeight = 10;
        terrain.MaxHeight = 100;

        var widthMean = 80f;
        var gapMean = 30f;

        var startX = 0f;

        for (int i = 0; i < numOfIslands; i++)
        {
            var terrainPoly = terrain.GenerateNextTerrainPolygon((float)GD.Randfn(widthMean, 5f));
            var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            AddChild(terrainMesh);
            terrainMesh.Translate(new Vector3(startX, 0, 0));
            startX += terrainPoly.BoundingRect.Size.X;
            startX += (float)GD.Randfn(gapMean, 5f);
            terrainMesh.GenerateMesh(terrainPoly);

            TerrainRegionMap[terrainPoly.BoundingRect] = terrainMesh;
        }  



    }


}
