using Godot;
using System;
using System.Numerics;
using System.Text;
using Vector2 = Godot.Vector2;

public partial class LineSegment(Vector2 start, Vector2 end) : GodotObject
{
    public Vector2 Start { get; set; } = start;
    public Vector2 End { get; set; } = end;

    public Vector2 GetNormal() 
    {
        return (End-Start).Rotated( (float)(Math.PI/2) ).Normalized();       
    }

    
    public void ExpandLine(float startExpand, float endExpand)
    {
        var normalized = (End - Start).Normalized();
        End += endExpand * normalized;
        Start -= startExpand * normalized;

    }

    

}
