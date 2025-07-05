using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Threading;
using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;
using System.Threading.Tasks;

public partial class QuadMeshDistortionApplier : GodotObject
{

    GeometryUtils gUtils = new();

    List<IQuadMeshDistorter> DistorterList = new();
    Dictionary<Rect2, List<IQuadMeshDistorter>> ActiveDistortersMap = new();

    HashSet<Vector2> PointsDistortedThisLoop = new();
    HashSet<Vector2> PointsToRemoveThisLoop = new();

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

    public Vector2 RoundVec(Vector2 vec)
    {
        return new Vector2(float.Round(vec.X, 3), float.Round(vec.Y, 3));      
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

    public async Task AddMeshDistorter(IQuadMeshDistorter meshDistorter)
    {
        PointsDistortedThisLoop = new();
        PointsToRemoveThisLoop = new();

        DistorterList.Add(meshDistorter);
        var lastMesh = QuadMeshHistory[^1];
        var newMesh = new PolygonQuadMesh(lastMesh);
        QuadMeshHistory.Add(newMesh);
        ParentNodeDistortRecursive(newMesh.RootQuad, meshDistorter);

        FilterAndRemovePoints();
    }

    private void FilterAndRemovePoints()
    {
        var quadMesh = GetQuadMesh();
        
        foreach (var point in PointsToRemoveThisLoop)
        {
            if (!PointsDistortedThisLoop.Contains(RoundVec(point)))
            {
                quadMesh.DeIndexPoint(point);
            }
        }
    }


    

    private void ParentNodeDistortRecursive(PolygonQuad node, IQuadMeshDistorter newDistorter)
    {

        bool isActive = newDistorter.IndexNode(node, ActiveDistortersMap[node.BoundingRect]);
        if (!isActive) return;

        bool addedSuccessfully = AddDistorterToNode(node, newDistorter);
        if (!addedSuccessfully) return;
        
        // ActiveDistortersMap[node.BoundingRect].Add(newDistorter);

        if (!node.HasChildren())
        {
            LeafNodeDistortRecursive(node, newDistorter);
            return;
        }

        if (ActiveDistortersMap[node.BoundingRect].Count == 1  && !newDistorter.DoSubdivide(node))
        {
            GroupDescendantPointsToRemove(node);
            node.Children = new();
            LeafNodeDistortRecursive(node, newDistorter) ;
            return;
        }
       
        for (int i = 0; i < node.Children.Count; i++)
        {
            var dupeChild = node.Children[i].Duplicate();
            node.Children[i] = dupeChild;
            ParentNodeDistortRecursive(dupeChild, newDistorter) ;

        }

    }

    private bool AddDistorterToNode(PolygonQuad node, IQuadMeshDistorter newDistorter)
    {
        var prevDistorters = ActiveDistortersMap[node.BoundingRect];
        var newRange = newDistorter.GetDepthRange(node);

        // check if new distorter wipes out data from old distorter, or is wiped out by old ones
        var isEnclosed = false;
        var refinedDistorters = new List<IQuadMeshDistorter>();
        for (int i = 0; i < prevDistorters.Count; i++)
        {
            var distorter = prevDistorters[i];
            var range = distorter.GetDepthRange(node);

            if (newRange.X >= range.Y) refinedDistorters.Add(distorter);  // old distorter is not dominated by new range
            if (range.X < newRange.Y) isEnclosed = true;
        }

        if (!isEnclosed) refinedDistorters.Add(newDistorter);
        ActiveDistortersMap[node.BoundingRect] = refinedDistorters;

        return !isEnclosed;

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

    private void GroupDescendantPointsToRemove(PolygonQuad node)
    {

        var hash = new HashSet<Vector2>();
        var queue = new List<PolygonQuad>() { node };
        int queuePos = 0;
        while (queue.Count > queuePos)
        {
            var qnode = queue[queuePos];
            queuePos += 1;

            for (int i = 0; i < qnode.Polygons.Count; i++) foreach (var point in qnode.Polygons[i]) hash.Add(point);
            queue.AddRange(qnode.Children);
        }
        PointsToRemoveThisLoop.UnionWith(hash);
    }

    private void SetChildNodeDistorters(PolygonQuad childNode)
    {
        var parentList = ActiveDistortersMap[childNode.Parent.BoundingRect];
        var childList = new List<IQuadMeshDistorter>();
        foreach (var distorter in parentList)
        {
            bool isActive = distorter.IndexNode(childNode, childList);
            if (isActive) childList.Add(distorter);
        }
        ActiveDistortersMap[childNode.BoundingRect] = childList;
    }

    private void DistortFace(PolygonQuad node)
    {
        var quadMesh = GetQuadMesh();
        var distortList = ActiveDistortersMap[node.BoundingRect];
        var pointList = node.Polygons.SelectMany(poly => poly).ToList();

        for (int i = 0; i < pointList.Count(); i++)
        {
            var point = pointList[i];
            if (PointsDistortedThisLoop.Contains(RoundVec(point))) continue;
            PointsDistortedThisLoop.Add(RoundVec(point));

            var vertexOrNull = quadMesh.GetVertex(point);
            var vertex = new Vector3(point.X, point.Y, 0);

            // new point, create a default vertex and apply all previous applicable distortions on top of latest
            if (vertexOrNull == null)
            {
                vertex = ApplyDistorts(point, vertex, node, distortList);
                
            }
            // existing point, just apply latest distorter
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
