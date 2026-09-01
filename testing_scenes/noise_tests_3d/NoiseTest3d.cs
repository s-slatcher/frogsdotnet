using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;

public partial class NoiseTest3d : Node3D
{
    int circleRad = 20;
    int totalSamples = 500;
    float offsetHeight = 10;

    [Export] public FastNoiseLite fastNoise;

    public override void _Ready()
    {

        
        var anglerPerSample = 2*float.Pi / totalSamples;
        List<Vector3> pointList = new();
        
        for (int i = 0; i < totalSamples; i++)
        {
            var ang = i * anglerPerSample;
            var pos = (circleRad*Vector2.Right).Rotated(ang);
            var offset = offsetHeight * fastNoise.GetNoise3D(pos.X, 0, pos.Y);

            // map 2d circle to xz, with height from noise
            var pos3d = new Vector3(pos.X, offset, pos.Y); 
            pointList.Add(pos3d);
        }

        foreach(var point in pointList) AddPointToScene(point);

    }

    void AddPointToScene(Vector3 point)
    {
        var meshInst = new MeshInstance3D();
        var sphere = new SphereMesh
        {
            Radius = 0.25f,
            Height = 0.5f
        };
        meshInst.Mesh = sphere;
        AddChild(meshInst);
        meshInst.Position = point;
    }

}
