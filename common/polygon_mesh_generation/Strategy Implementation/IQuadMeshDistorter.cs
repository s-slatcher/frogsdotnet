using Godot;
using System;
using System.Collections.Generic;

public interface IQuadMeshDistorter
{
    //returns true if distorter is active in the region of that quad
    bool IndexNode(PolygonQuad node, List<IQuadMeshDistorter> activeDistortersList);

    // bool IsActiveForNode(PolygonQuad node);

    bool DoSubdivide(PolygonQuad node);

    bool DoWipeChildren(PolygonQuad node);


    Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node);
}
