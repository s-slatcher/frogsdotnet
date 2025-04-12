using Godot;
using System;
using System.Linq;

public partial class MeshExtruderTest : Node3D
{
    public ExtrudedMesh em;
    public Polygon2D poly2d;

    public override void _Ready()
    {   
        poly2d = GetNode<Polygon2D>("CanvasLayer/Polygon2D");
        Vector2[] polygon = poly2d.Polygon;

        var curve2d = GetNode<Path2D>("CanvasLayer/Path2D");
        var curve = curve2d.Curve;
        
        curve.BakeInterval = 14;
        var curvePolygon = curve.Tessellate();
        var scaledCurvePolygon = new GeometryUtils().ScalePolygon(curvePolygon, new Vector2(1/50f,1/50f));

        var scaledPolygon = new GeometryUtils().ScalePolygon(polygon, new Vector2(1/15f,1/15f));

        var time = Time.GetTicksMsec();
        em = new(scaledCurvePolygon, 0.125f, 0.5f, 3);
        
        GetNode<MeshInstance3D>("ExtrudedMeshContainer").Mesh = em.GetMesh();
        GetNode<MeshInstance3D>("ExtrudedMeshContainerBack").Mesh = em.GetMeshBack();
        GD.Print("mesh gen time = " + (Time.GetTicksMsec() - time));
        GetNode<MeshInstance3D>("ExtrudedWireframeContainer").Mesh = em.GetWireframeMesh();

        
    }

   

}
