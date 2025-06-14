using System;
using System.Collections.Generic;
using System.Numerics;
using Godot;

using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;


public interface IMeshDistorter 
{

    void IndexFace(Vector2[] face2D, Vector3[] face3D);

    bool ShouldContinueDownNode(PolygonQuad node);
    bool ShouldSubdivide(PolygonQuad node);
    bool ShouldDeSubdivide(PolygonQuad node);
    
    Vector3 UpdateVertex(Vector2 point, Vector3 VertexPosition);


}