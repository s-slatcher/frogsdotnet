using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;

public partial class QuadMeshDistortionApplier : GodotObject
{

    GeometryUtils gUtils = new();

    List<IQuadMeshDistorter> DistorterList = new();

    Dictionary<Rect2, List<IQuadMeshDistorter>> ActiveDistortersMap = new();

    public List<PolygonQuadMesh> QuadMeshHistory = new();

    public QuadMeshDistortionApplier(PolygonQuadMesh polygonQuadMesh)
    {
        QuadMeshHistory.Add(polygonQuadMesh);
        ActiveDistortersMap[polygonQuadMesh.RootQuad.BoundingRect] = new();
    }

    public PolygonQuadMesh GetQuadMesh()
    {
        return QuadMeshHistory[^1];
    }

    public List<IQuadMeshDistorter> DistortersActiveOnQuad(Rect2 QuadRegion)
    {
        if (!ActiveDistortersMap.ContainsKey(QuadRegion))
        {
            GD.Print("region not linked to quad node");
            return new();
        }
        return ActiveDistortersMap[QuadRegion];
    }

    public void AddMeshDistorter(IQuadMeshDistorter meshDistorter)
    {
        DistorterList.Add(meshDistorter);
        var lastMesh = QuadMeshHistory[^1];
        var newMesh = new PolygonQuadMesh(lastMesh);
        QuadMeshHistory.Add(newMesh);
        ParentNodeDistortRecursive(newMesh.RootQuad, meshDistorter);
    }


    private void ParentNodeDistortRecursive(PolygonQuad node, IQuadMeshDistorter newDistorter)
    {
        
        if (!newDistorter.IsActiveForNode(node)) return;

        ActiveDistortersMap[node.BoundingRect].Add(newDistorter);

        if ( ! node.HasChildren())
        {
            LeafNodeDistortRecursive(node, newDistorter);
            return;
        }
        
        for (int i = 0; i < node.Children.Count; i++)
        {
            var dupeChild = node.Children[i].Duplicate();
            node.Children[i] = dupeChild;
            ParentNodeDistortRecursive(dupeChild, newDistorter);
        }

    }

    private void LeafNodeDistortRecursive(PolygonQuad node, IQuadMeshDistorter newDistorter)
    {
        if (!ActiveDistortersMap[node.BoundingRect].Contains(newDistorter))
        {
            
            DistortFace(node);
            return;
        }

        var doSubdivide = newDistorter.DoSubdivide(node);

        if (doSubdivide)
        {
            node.Subdivide();
            foreach (var child in node.GetChildren())
            {
                SetChildNodeDistorters(child);
                LeafNodeDistortRecursive(child, newDistorter);
            }
        }
        else
        {
            DistortFace(node);
        }
    }

    private void SetChildNodeDistorters(PolygonQuad childNode)
    {
        var parentList = ActiveDistortersMap[childNode.Parent.BoundingRect];
        var childList = parentList.Where(distorter => distorter.IsActiveForNode(childNode));
        ActiveDistortersMap[childNode.BoundingRect] = childList.ToList();
    }

    private void DistortFace(PolygonQuad node)
    {
        var quadMesh = GetQuadMesh();
        var distortList = ActiveDistortersMap[node.BoundingRect];
        var pointList = node.Polygons.SelectMany(poly => poly).ToList();
        // var vertexList = pointList.Select(point => quadMesh.GetVertex(point) ?? new Vector3(point.X, point.Y, 0)).ToList();

        for (int i = 0; i < pointList.Count(); i++)
        {
            var point = pointList[i];
            var vertexOrNull = quadMesh.GetVertex(point);
            var vertex = new Vector3(point.X, point.Y, 0);

            // new point
            // create a default vertex and apply all previous applicable distortions on top of latest 
            if (vertexOrNull == null)
            {
                vertex = ApplyDistorts(point, vertex, node, distortList);
                
            }
            // exisitng point, just apply latest distorter
            else
            {
                if (distortList.Count > 0) vertex = ApplyDistorts(point, (Vector3)vertexOrNull, node, new() { distortList[^1] });
            }
            quadMesh.IndexPoint(pointList[i], vertex);
        }

    }

    private Vector3 ApplyDistorts(Vector2 point, Vector3 vertex, PolygonQuad node, List<IQuadMeshDistorter> distorterList)
    {
        var currentVertex = vertex;
        for (int i = 0; i < distorterList.Count; i++)
        {
            var newVert = distorterList[i].DistortVertex(point, currentVertex, node);
            currentVertex = newVert;
        }
        return currentVertex;
    }

    

 

}
