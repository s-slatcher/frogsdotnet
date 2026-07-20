using Godot;
using System;
using System.Numerics;
using System.Text;
using Vector2 = Godot.Vector2;

public partial class LineSegment: GodotObject
{
    
    public Vector2 Start { get; set; } 
    public Vector2 End { get; set; }
    
    public LineSegment(Vector2 start, Vector2 end)
    {
        this.Start = start;
        this.End = end;
    }
    
    public Vector2 GetNormal()
    {
        return (End - Start).Rotated((float)(Math.PI / 2)).Normalized();
    }

    public Vector2 DirectionVector()
    {
        return End - Start;
    }

    public float Length()
    {
        return Start.DistanceTo(End);
    }

    public void Translate(Vector2 translation)
    {
        Start += translation; End += translation;
    }
    
    public void ExpandLine(float startExpand, float endExpand)
    {
        var normalized = (End - Start).Normalized();
        Start -= startExpand * normalized;
        End += endExpand * normalized;

    }

    public LineSegment GetExpandedLine(float startExpand, float endExpand)
    {
        var normalized = (End - Start).Normalized();
        return new LineSegment(Start - startExpand * normalized, End + endExpand * normalized );
    }

}


