using Godot;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;

public partial class DebugExplosionGenerator : Node3D
{

    [Export] public Vector2 radiusRange = new Vector2(1,5);

    [Export] private PlaneMouseCapture capturePlane;
    [Export] private PackedScene explodeScene;

    public Action<Vector3, float> AreaExploded;
    public override void _Ready()
    {
        capturePlane.PlaneClicked += OnPlaneClicked;
    }

      private void OnPlaneClicked(Vector3 pos)
    {
        
        var randRadius = (float)GD.RandRange(radiusRange.X, radiusRange.Y);
        var center2d = new Vector2(pos.X, pos.Y);


        var explodeRect = new Rect2()
        {
            Size = new Godot.Vector2(randRadius * 2, randRadius * 2),
            Position = center2d - new Vector2(randRadius, randRadius)
        };

        AreaExploded.Invoke(pos, randRadius);

        var explosion = (Node3D)explodeScene.Instantiate();
        explosion.Position = pos + new Vector3(0, 0, 1);
        explosion.Scale = new Vector3(1, 1, 1) * randRadius;

        AddChild(explosion);
        

    }

}
