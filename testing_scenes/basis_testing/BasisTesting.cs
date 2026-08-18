using Godot;
using System;
public partial class BasisTesting : Node3D
{
    [Export] MeshInstance3D meshNode;
    [Export] MeshInstance3D meshNode2;
    public override void _Ready()
    {
        var Trans1 = meshNode.Transform;
        var Trans2 = meshNode2.Transform;

        var topCorner = new Vector3(-0.5f, 0.5f,0 );
        var bottomCorner = new Vector3(-0.5f, -0.5f, 0);

        var trans1bottom = topCorner * Trans1;
        

        
    }

}
