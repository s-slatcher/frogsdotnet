using Godot;
using System;
using Vector3 = Godot.Vector3;

public partial class IndexedVertex : GodotObject
{   
    public Vector2 SourcePosition = Vector2.Zero;
    public Vector3 Position = Vector3.Zero;
    public Vector3 Normal = Vector3.Forward;
    public Vector3 VertexColor = (Vector3.Forward + new Vector3(1,1,1)) / 2; 
    public Vector2 UV = Vector2.Zero;
    public int ArrayIndex;
}
