using Godot;
using System;
using System.Collections.Generic;

public partial class PolyVertex : GodotObject
{
    public Vector3 Position = new();
    /// <summary>
    /// All the edges where this vertex is the 'startVert'
    /// </summary>
    public HashSet<HalfEdge> LeadingHalfEdges = new();

    public Vector3 GetAveragedNormal()
    {
        return new();
    }
}
