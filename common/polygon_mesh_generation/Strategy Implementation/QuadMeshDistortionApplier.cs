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
        return PolygonQuadMesh.RoundVector2(vec);
        // return new Vector2(float.Round(vec.X, 3), float.Round(vec.Y, 3));      
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
        var time = Time.GetTicksMsec();

        PointsDistortedThisLoop = new();
        PointsToRemoveThisLoop = new();

        DistorterList.Add(meshDistorter);
        var lastMesh = QuadMeshHistory[^1];
        var newMesh = new PolygonQuadMesh(lastMesh);
        QuadMeshHistory.Add(newMesh);
        ParentNodeDistortRecursive(newMesh.RootQuad, meshDistorter);

        FilterAndRemovePoints();

        GD.Print("DistortionApplier distortion time: ", (Time.GetTicksMsec() - time)); 
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


    
    private int DepthRangeSort(Vector2 a, Vector2 b) { return (int)(a.X - b.X); }
    private bool DepthEnclosesDepth(Vector2 a, Vector2 b) { return a.X < b.Y; }

    private void FilterDistorters(PolygonQuad node)
    {
        var distortList = ActiveDistortersMap[node.BoundingRect];
        if (distortList.Count == 0) return;

        var sortCol = distortList.OrderBy( distorter => distorter.GetDepthRange(node).X);
        var lowestRange = sortCol.ToArray()[0].GetDepthRange(node);

        distortList = distortList.Where( distorter => !DepthEnclosesDepth(lowestRange, distorter.GetDepthRange(node))).ToList();
        ActiveDistortersMap[node.BoundingRect] = distortList;
    }

    private void ParentNodeDistortRecursive(PolygonQuad node, IQuadMeshDistorter newDistorter)
    {

        var distortList = ActiveDistortersMap[node.BoundingRect];
        bool isActive = newDistorter.IndexNode(node, distortList);
        if ( ! isActive) return;

        //filter against other distorts and see if still active
        distortList.Add(newDistorter);
        FilterDistorters(node);
        if ( ! distortList.Contains(newDistorter) ) return;
        
        
        // TODO - get leaf node func back into this one, see if indexing filtering nodes new subdivides can be handled here instead
        if (!node.HasChildren())
        {
            LeafNodeDistortRecursive(node, newDistorter);
            return;
        }

        // 
        if (distortList.Count == 1 && !newDistorter.DoSubdivide(node))
        {
            GroupDescendantPointsToRemove(node);
            node.Children = new();
            LeafNodeDistortRecursive(node, newDistorter);
            return;
        }

        // replace children with duplicates and recurse on duped children
        for (int i = 0; i < node.Children.Count; i++)
        {
            var dupeChild = node.Children[i].Duplicate();
            dupeChild.Parent = node;
            node.Children[i] = dupeChild;
            ParentNodeDistortRecursive(dupeChild, newDistorter);
        }

    }


    private void LeafNodeDistortRecursive(PolygonQuad node, IQuadMeshDistorter newDistorter)
    {
        var activeDistorts = ActiveDistortersMap[node.BoundingRect];
        bool hasNewDistort = activeDistorts.Count > 0 && activeDistorts[^1] == newDistorter;

        if (hasNewDistort && newDistorter.DoSubdivide(node))
        {
            node.Subdivide();
            foreach (var child in node.GetChildren())
            {
                SetChildNodeDistorters(child);
                LeafNodeDistortRecursive(child, newDistorter);
            }
        }

        else DistortFace(node);
        
        
    }

    private void GroupDescendantPointsToRemove(PolygonQuad node)
    {

        var hash = new HashSet<Vector2>();
        var queue = new List<PolygonQuad>() { node };
        int queuePos = 0;
        while (queue.Count > queuePos)
        {
            var qNode = queue[queuePos];
            queuePos += 1;

            for (int i = 0; i < qNode.Polygons.Count; i++) foreach (var point in qNode.Polygons[i]) hash.Add(point);
            queue.AddRange(qNode.Children);
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
        FilterDistorters(childNode);

        
    }

    private void DistortFace(PolygonQuad node)
    {
        var quadMesh = GetQuadMesh();
        var distortList = ActiveDistortersMap[node.BoundingRect];
        var pointList = node.Polygons.SelectMany(poly => poly).ToList();

        for (int i = 0; i < pointList.Count; i++)
        {
            var point = pointList[i];
            if (PointsDistortedThisLoop.Contains(RoundVec(point))) continue;
            PointsDistortedThisLoop.Add(RoundVec(point));

            // vertex is reset, and all previous (still valid) distorts are re-applied
            var vertex = new Vertex() { SourcePosition = point, Normal = Vector3.Back, Position = new Vector3(point.X, point.Y, 0) } ;
            ApplyDistorts(point, vertex, node);
            quadMesh.IndexPoint(pointList[i], vertex);
        }

    }

    private void ApplyDistorts(Vector2 point, Vertex vertex, PolygonQuad node)
    {
        foreach (IQuadMeshDistorter d in ActiveDistortersMap[node.BoundingRect]) d.DistortVertex(point, vertex, node);
        // var currentVertex = vertex;
        // var distortList = ActiveDistortersMap[node.BoundingRect];
        // for (int i = 0; i < distortList.Count; i++)
        // {
        //     distortList[i].DistortVertex(point, vertex, node);
        //     // currentVertex = newVert;
        // }
        
    }

    

 

}
