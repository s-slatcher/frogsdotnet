using Godot;
using System;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class DebugCameraData: Resource
{

    [Export] public Vector2 Position = new Vector2(0,0);
    [Export] public Vector2 Zoom = new Vector2(1,1);

}
