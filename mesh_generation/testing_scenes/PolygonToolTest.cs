using Godot;
using System;

[Tool]
public partial class PolygonToolTest : Node3D
{
    [Export] MeshInstance3D meshNode;
    [Export] Polygon2D polyNode;
    public override void _Ready()
    {
        var polyMeshTool = new PolygonMeshTool();
        
        var polygon = new NormalPoly(polyNode.Polygon, 0);

        polyMeshTool.SetPolygon(polygon);

        var mesh = polyMeshTool.GetMesh();

        meshNode.Mesh = mesh;


    }

}
