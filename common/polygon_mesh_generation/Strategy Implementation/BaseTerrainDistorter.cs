using Godot;
using System;
using System.Dynamic;

public partial class BaseTerrainDistorter (float targetSubdivideWidth) : GodotObject, IQuadMeshDistorter
{
    public float TargetSubdivideWidth = targetSubdivideWidth;

    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
    {
        return currentVertex;
    }

    public bool DoSubdivide(PolygonQuad node)
    {
        return node.GetWidth() > TargetSubdivideWidth;
    }

    public bool IsActiveForNode(PolygonQuad node)
    {
        return true;
    }

}
