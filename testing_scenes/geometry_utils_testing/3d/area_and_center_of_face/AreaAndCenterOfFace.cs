using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

public partial class AreaAndCenterOfFace : Node3D
{
    public override void _Ready()
    {
        var posVec = new Vector2(1,1);
        var width_vec = new Vector2(5,0);
        var height_vec = new Vector2(0,10);
        var points2d = new List<Vector2>()
        {
            new Vector2() + posVec,
            width_vec + posVec,
            width_vec/2 + height_vec + posVec,
            height_vec + (width_vec/2) + posVec
        };
        var points3d = points2d.Select(p => new Vector3(p.X, p.Y, 4).Rotated(Vector3.Right, 0f)).ToList();

        
        (float, Vector3) areaCen = GeometryUtils.AreaAndCenterOf3DPolyFace(points3d);
        GD.Print("area: ", areaCen.Item1);
        GD.Print("center: ", areaCen.Item2);

        var naiveCentroid = new Vector3();
        foreach (var p in points3d) naiveCentroid += p;
        naiveCentroid /= points3d.Count;

        GD.Print("non weighted centroid: ", naiveCentroid);

    }

}
