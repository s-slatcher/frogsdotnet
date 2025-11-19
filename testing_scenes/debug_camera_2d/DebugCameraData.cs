using Godot;
using System;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class DebugCameraData: Resource
{

    [Export] public Vector2 Position = new(0,0);
    [Export] public Vector2 Zoom = new(1,1);
    [Export] public bool FlipY = false;

}
