using Godot;
using System;

public partial class MeshBevelingTest : Node3D
{
    CsgPolygon3D csgNode;
    public override void _Ready()
    {
       csgNode = GetNode<CsgPolygon3D>("PolygonMesh");
        
        CallDeferred("GetMeshData");
    //    var mesh = (Mesh)(GodotObject)csgNode.GetMeshes()[1];
       
    //    GD.Print(mesh.GetSurfaceCount());

    }

    public void GetMeshData()
    {
        var mesh = (Mesh)(GodotObject)csgNode.GetMeshes()[1];
       
        GD.Print(mesh.GetSurfaceCount());

        AddChild(new MeshInstance3D(){Mesh = mesh});
        
    }

}
