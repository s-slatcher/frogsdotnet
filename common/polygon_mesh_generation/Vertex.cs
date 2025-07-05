using Godot;
using System;

public partial class Vertex : GodotObject
{
    public Vector2 SourcePosition = Vector2.Zero;
    public Vector3 Position = Vector3.Zero;
    public Vector3 Normal = Vector3.Forward;
    public Vector3 VertexColor = (Vector3.Forward + new Vector3(1,1,1)) / 2; 
    public Vector2 UV = Vector2.Zero;
    
}
