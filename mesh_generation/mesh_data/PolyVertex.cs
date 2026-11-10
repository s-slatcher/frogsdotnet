using Godot;
using System;
using System.Collections.Generic;

public partial class PolyVertex : GodotObject
{
    public PolyVertex()
    {
        
    }
    public PolyVertex(Vector3 Pos)
    {
        Position = Pos;
    }
    public Vector3 Position = new();
    public Vector2 UV = new();

    public Vector3 GetAveragedNormal()
    {
        return new();
    }
}
