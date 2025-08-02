using Godot;
using System;
using Vector3 = Godot.Vector3;

public partial class IndexedVertex : GodotObject
{
    public Vector2 SourcePosition;
    public Vector3 Position = Vector3.Zero;
    public Vector3 Normal;
    public Vector3 VertexColor; 
    public Vector2 UV;
    public Color Custom0;
    public Color Custom1;
    public int ArrayIndex;
}
