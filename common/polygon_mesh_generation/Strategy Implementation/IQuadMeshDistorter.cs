using Godot;
using System;

public interface IQuadMeshDistorter
{
    

    bool IsActiveForNode(PolygonQuad node);

    bool DoSubdivide(PolygonQuad node);

    Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node);

}
