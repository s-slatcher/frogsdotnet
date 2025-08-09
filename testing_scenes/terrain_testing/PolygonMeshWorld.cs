using Godot;
using System;

public partial class PolygonMeshWorld : Node3D
{
    [Export] PackedScene explodeScene;
    TerrainMesh terrainMesh;

    Vector2 radiusRange = new Vector2(2, 5);

    public override void _Ready()
    {

        PlaneMouseCapture planeCap = GetNode<PlaneMouseCapture>("PlaneMouseCapture");
        planeCap.PlaneClicked += OnPlaneClicked;

        var time = Time.GetTicksMsec();

        var terrain = new TerrainMap(15);
        terrain.MinHeight = 10;
        terrain.MaxHeight = 100;
        var terrainPolygon = terrain.GenerateNextTerrainPolygon(100);
        var poly = terrainPolygon.Polygon;
        var curve = terrainPolygon.SimplifiedHeightCurve;

        // var polygon = new GeometryUtils().PolygonFromRect(new Rect2(Vector2.Zero, new Vector2(20, 20)));

        terrainMesh = GetNode<TerrainMesh>("TerrainMesh");
        terrainMesh.SideLength = 5;
        // terrainMesh.DepthDomainCurve = terrainPolygon.SimplifiedHeightCurve;
        terrainMesh.GenerateMesh(terrainPolygon);

        GD.Print("TERRAIN GENERATED: ", Time.GetTicksMsec() - time);


     

        // AddChild(meshInst);

    }

    private void OnPlaneClicked(Vector3 vector)
    {
        var randRadius = (float)GD.RandRange(radiusRange.X, radiusRange.Y);
        
        terrainMesh.ExplodeTerrain(vector, randRadius);
        var explosion = (Node3D)explodeScene.Instantiate();
        explosion.Position = vector + new Vector3(0,0,1);
        explosion.Scale = new Vector3(1, 1, 1) * randRadius;
        
        AddChild(explosion);

    }


    public override void _Process(double delta)
    {
        if (Time.GetTicksMsec() < 2000) return;
        float dt = (float)delta;
        // radiusRange += new Vector2(0.75f * dt, 1.1f * dt);
        // if (Time.GetTicksMsec() < 2500) return;
        // var randPos = new Vector3(GD.RandRange(0, 150), GD.RandRange(0, 100), 0);
        // var radiusAdd = (1.0 - randPos.Y / 100) * 5;
        // terrainMesh.ExplodeTerrain(
        //     randPos, (float)GD.RandRange(1 * radiusAdd, 3 * radiusAdd)
        // );
    }


}
