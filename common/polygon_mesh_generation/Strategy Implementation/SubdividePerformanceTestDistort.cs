using Godot;
using System;
using System.Collections.Generic;

public partial class SubdividePerformanceTestDistort : GodotObject, IQuadMeshDistorter
{
    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
    {
        return currentVertex;
    }

    public bool DoSubdivide(PolygonQuad node)
    {
        throw new NotImplementedException();
    }

    public bool DoWipeChildren(PolygonQuad node)
    {
        throw new NotImplementedException();
    }

    public bool IndexNode(PolygonQuad node, List<IQuadMeshDistorter> activeDistortersList)
    {
        throw new NotImplementedException();
    }

}
