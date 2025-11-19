using Godot;
using System;

public partial class PolygonPoint : GodotObject
{
    public Vector2 Position = new();
    public bool isKey = false;
    public PolygonPoint nextPoint;
    public PolygonPoint prevPoint;
    public PolygonPoint nextKeyPoint;
    public PolygonPoint prevKeyPoint;
 
}
