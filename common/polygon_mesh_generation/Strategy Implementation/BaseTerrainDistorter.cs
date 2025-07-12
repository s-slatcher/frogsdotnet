using Godot;
using System;
using System.Collections.Generic;
using System.Dynamic;

public partial class BaseTerrainDistorter(float targetSubdivideWidth, Rect2? region = null) : GodotObject, IQuadMeshDistorter
{
    public float TargetSubdivideWidth = targetSubdivideWidth;
    public Rect2? Region = region;

    public Vertex DistortVertex(Vector2 point, Vertex currentVertex, PolygonQuad node)
    {
        return currentVertex;
    }

    public bool DoSubdivide(PolygonQuad node)
    {
        return node.GetWidth() > TargetSubdivideWidth;
    }

    public bool DoWipeChildren(PolygonQuad node)
    {
        return false;
    }

    public bool IndexNode(PolygonQuad node, List<IQuadMeshDistorter> activeDistortersList)
    {
        if (Region == null) return node.GetWidth() > TargetSubdivideWidth;
        var reg = (Rect2)Region;
        return node.BoundingRect.Intersects(reg);
    }

    public Vector2 GetDepthRange(PolygonQuad node)
    {
        // depth range that will never override and never be overridden
        return new Vector2(10000, -10000);
    }
    

}
