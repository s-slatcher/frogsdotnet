using Godot;
using Godot.NativeInterop;
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

    RandomNumberGenerator rng = new();

    Vector2 radiusRange = new Vector2(2, 8);

    Vector2 lastClick = Vector2.Zero;

    Dictionary<Rect2, TerrainMesh> TerrainRegionMap = new();

    public override void _Ready()
    {

        rng.Seed = 1;

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
            var newRect = new Rect2(rect.Position + new Vector2(mesh.Position.X, mesh.Position.Y), rect.Size);

            if (explodeRect.Intersects(newRect))
            {
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
        int numOfLandmasses = 3;

        var terrain = new TerrainMap((int)GD.Randi());
        terrain.MinHeight = 10;
        terrain.MaxHeight = 80;

        var numOfFloatingIslands = 3;


        var widthMean = 50f;
        var gapMean = 60f;

        var startX = 0f;

        var curvePoints = new List<Vector2>();

        Curve combinedCurve = new();

        for (int i = 0; i < numOfLandmasses; i++)
        {
            var terrainPoly = terrain.GenerateNextTerrainPolygon((float)rng.Randfn(widthMean, 5f));
            var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            AddChild(terrainMesh);
            terrainMesh.QuadDensity = 0.5f;
            terrainMesh.Translate(new Vector3(startX, 0, 0));

            var curve = terrainPoly.SimplifiedHeightCurve;

            for (int j = 0; j < curve.PointCount; j++)
            {
                var p = curve.GetPointPosition(j);
                var pTranlate = p + new Vector2(startX, 0);
                curvePoints.Add(pTranlate);
            }

            startX += terrainPoly.BoundingRect.Size.X;
            startX += (float)rng.Randfn(gapMean, 5f);

            terrainMesh.TerrainPolygon = terrainPoly;

            TerrainRegionMap[terrainPoly.BoundingRect] = terrainMesh;

        }

        foreach (var p in curvePoints)
        {
            // var meshInst = new MeshInstance3D();
            // var sphereMesh = new SphereMesh();
            // meshInst.Mesh = sphereMesh;
            // sphereMesh.Radius = 5;
            // sphereMesh.Height = 10;
            // meshInst.Position = new Vector3(p.X, p.Y, 0);
            // AddChild(meshInst);

            combinedCurve.AddPoint(p);
        }

        var totalWidth = startX;
        var floatingIslandGap = totalWidth / (numOfFloatingIslands + 1);

        for (int i = 0; i < numOfFloatingIslands; i++)
        {
            var randOffset = GD.RandRange(0, 10);
            var xCoord = (i + 1) * floatingIslandGap;
            var heightValue = combinedCurve.SampleBaked(xCoord + randOffset);
            heightValue = float.Max(heightValue, 30);
            var randHeightOffset = GD.RandRange(50, 100);

            var islandPosition = new Vector2(xCoord + randOffset, heightValue + randHeightOffset);

            var randHeight = GD.RandRange(10, 20);
            var islandPolygon = new NoiseEdgePoly(randHeight/2, 8, randHeight * 2f, true).Polygon;
            var terrainPoly = new TerrainPolygon()
            {
                Polygon = islandPolygon,
                BoundingRect = GeometryUtils.RectFromPolygon(islandPolygon),
                SimplifiedHeightCurve = new()
            };

            var terrainMesh = (TerrainMesh)terrainMeshScene.Instantiate();
            AddChild(terrainMesh);
            terrainMesh.QuadDensity = 0.5f;
            terrainMesh.Translate(new Vector3(islandPosition.X, islandPosition.Y, 0));
            terrainMesh.TerrainPolygon = terrainPoly;

            


        }



    }

   


}
