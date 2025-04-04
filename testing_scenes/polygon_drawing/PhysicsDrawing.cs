using Godot;
using System;

public partial class PhysicsDrawing : RigidBody3D
{
    public MeshInstance3D MeshInstance;


    public override void _Ready()
    {
        AddChild(MeshInstance);
        MeshInstance.CreateConvexCollision();
        var collision = MeshInstance.GetChild(0).GetChild(0) as Node3D;
        // collision.Scale = MeshInstance.Scale;
        collision.Reparent(this);
        GetNode<Timer>("Timer").Timeout += () => QueueFree();
    }


}
