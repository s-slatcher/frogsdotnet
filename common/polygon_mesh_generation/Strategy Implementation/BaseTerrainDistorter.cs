using Godot;
using System;
using System.Collections.Generic;
using System.Dynamic;

public partial class BaseTerrainDistorter(float targetSubdivideWidth, Rect2? region = null) : GodotObject, IQuadMeshDistorter
{
    public float TargetSubdivideWidth = targetSubdivideWidth;
    public Rect2? Region = region;

    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
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
        return IsActiveForNode(node);
    }

    public bool IsActiveForNode(PolygonQuad node)
    {
        if (Region == null) return node.GetWidth() > TargetSubdivideWidth;
        var reg = (Rect2)Region;
        return node.BoundingRect.Intersects(reg);
    }
    
    

}
