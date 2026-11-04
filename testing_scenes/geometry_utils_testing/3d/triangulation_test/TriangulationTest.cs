using Godot;
using System;
using System.Collections.Generic;

public partial class TriangulationTest : Node2D
{
    public override void _Ready()
    {
        var points = new List<Vector3>();
        var p1 = Vector3.Back;
        var p2 = p1 + Vector3.Right;
        var p3 = p2 + Vector3.Forward;
        var p4 = Vector3.Zero;
        points = [p1,p2,p3,p4];

        var tris = GeometryUtils.TriangulateFace3D(points);

        foreach (var tri in tris)
        {
            GD.Print("tri: ");
            foreach (var index in tri) GD.Print(points[index]);
        }


    }

}
