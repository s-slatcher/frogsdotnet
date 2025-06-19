using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

public partial class QuadTreeMeshDistorter : GodotObject
{
    public GeometryUtils gUtils = new();

    public PolygonQuadMesh QuadMesh;
    
    public HashSet<Vector2> VerticesUpdated = new(); 
    

    public QuadTreeMeshDistorter(PolygonQuadMesh quadMesh)
    {
        // shallow duplicates quad mesh and its quad tree root node. 
        QuadMesh = new PolygonQuadMesh(quadMesh);
        QuadMesh.RootQuad = QuadMesh.RootQuad.Duplicate();
    }

    public void ApplyDistort()
    {
        DistortFacesRecurisive(QuadMesh.RootQuad);
    }



    private void DistortFacesRecurisive(PolygonQuad node)
    {

        if (!DoTraverseNode(node))
        {
            foreach (var point in node.Polygons.SelectMany(poly => poly)) TryDistortPoint(point, node);
            return;
        }
       
        // replace nodes children with duplicates if children already exist
        if (node.HasChildren()) for (int i = 0; i < node.Children.Count; i++) node.Children[i] = node.Children[i].Duplicate();
        //else checks if subdivsion needed to reach target detail level
        else if (DoSubdivide(node)) node.Subdivide();
        // still no children -- distort points and return
        else
        {
            foreach (var point in node.Polygons.SelectMany(poly => poly)) TryDistortPoint(point, node);
            return;
        }
        
        // if not returned yet -- quad must have children to recurse further 
        foreach (var child in node.Children) DistortFacesRecurisive(child);
            
        
    }

    private void TryDistortPoint(Vector2 point, PolygonQuad node)
    {
        if (VerticesUpdated.Contains(point)) return;
        VerticesUpdated.Add(point);

        Vector3 vertexPosition = GetDistortedVertex(point, node);
        QuadMesh.IndexPoint(point, vertexPosition);
    }


    protected virtual bool DoTraverseNode(PolygonQuad node)
    {
        return true;
    }
    
    protected virtual bool DoSubdivide(PolygonQuad node)
    {
        return node.GetWidth() > GetTargetDetailLevel(node) && node.GetWidth() > node.MinimumQuadWidth;
    }

    protected virtual Vector3 GetDistortedVertex(Vector2 point, PolygonQuad node)
    {
        return new Vector3(point.X, point.Y, 0);
    }


    protected virtual float GetTargetDetailLevel(PolygonQuad node)
    {
        return float.MaxValue;
    }
}
