using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TunnelDistorter : GodotObject, IQuadMeshDistorter
{

    public Vector2 Center1;
    public Vector2 Center2;
    public LineSegment CenterLine;
    public float Radius;
    public Vector2[] MiddlePoly;

    public float MaxDepthDifference = 1.75f;

    public Dictionary<Vector2, Vector3> DistortedVertices = new();
    public Dictionary<Rect2, Vector2> NodeDepthRangeMap = new();
    public Dictionary<Rect2, bool> DoWipeChildrenMap = new();

    public Dictionary<Vector2, float> PointDepthMap = new();
   


    public GeometryUtils gUtils = new();

     

    public TunnelDistorter(Vector2 center1, Vector2 center2, float radius)
    {
        
        Center1 = center1;
        Center2 = center2;
        CenterLine = new LineSegment(Center1, Center2);
        Radius = radius;
        
        if (Center1 != Center2) SetMiddlePoly();

    }
    public TunnelDistorter(Vector2 center, float radius)
    {
        Center1 = center;
        Center2 = center;
        CenterLine = new LineSegment(Center1, Center2);
        Radius = radius;
    }

    
    // total re-write of this needed
    public bool IndexNode(PolygonQuad node, List<IQuadMeshDistorter> activeDistortersList)
    {
        var depthRange = SetDepthRange(node.BoundingRect);
        var TunnelDistorters = new List<TunnelDistorter>();
        bool hasEdge = false;
        foreach (var distorter in activeDistortersList)
        {
            if (distorter is TunnelDistorter distorter1) TunnelDistorters.Add(distorter1);
            else hasEdge = true;
        }
        var depthRanges = TunnelDistorters.Select(tunnel => tunnel.NodeDepthRangeMap[node.BoundingRect]);

        var isEnclosed = false;
        var doesEnclose = true;
        foreach (var range in depthRanges)
        {
            if (!RangeEnclosesRange(depthRange, range)) doesEnclose = false;
            if (range.X < depthRange.Y) isEnclosed = true;
        }


        DoWipeChildrenMap[node.BoundingRect] = doesEnclose;
        if (hasEdge) DoWipeChildrenMap[node.BoundingRect] = false;
        if (isEnclosed) return false;
        return IsActiveForNode(node);
    }

    private bool RangeEnclosesRange(Vector2 range1, Vector2 range2)
    {
        // shallow end (X axis) of first range is still deeper (lower) than deepest end of range2
        return range1.X < range2.Y;
    }

    private Vector2 SetDepthRange(Rect2 region)
    {
        float shallow;
        float deep;

        bool rectOverlapsCenter = gUtils.FilterLineSegmentsByRectIntersection([CenterLine], region).Count != 0;

        // find deepest point on perimeter (closest to center)
        if (rectOverlapsCenter) deep = -Radius;
        else
        {
            var points = gUtils.ClosestPointsOnRectAndSegment(region, CenterLine);
            deep = GetDepthAtPoint(points[0], false);
        }

        // find shallowest point (must be a corner?)
        var cornerDepths = gUtils.PolygonFromRect(region).ToList().Select(point => GetDepthAtPoint(point, false)).ToList();
        cornerDepths.Sort();
        shallow = cornerDepths[^1];
        var depthRange = new Vector2(shallow, deep);
        NodeDepthRangeMap[region] = depthRange;
        return depthRange;
    }

    private void SetMiddlePoly()
    {
        var middleVec = Center2 - Center1;
        var p1 = middleVec.Rotated(float.Pi / 2).Normalized() * Radius + Center1;
        var p2 = middleVec.Rotated(-float.Pi / 2).Normalized() * Radius + Center1;
        var p3 = (middleVec * -1).Rotated(float.Pi / 2).Normalized() * Radius + Center2;
        var p4 = (middleVec * -1).Rotated(-float.Pi / 2).Normalized() * Radius + Center2;
        MiddlePoly = [p1, p2, p3, p4];

    }

    private Vector2 RelativeCenter(Vector2 point)
    {
        return Geometry2D.GetClosestPointToSegment(point, Center1, Center2);
    }

    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
    {

        if (DistortedVertices.ContainsKey(point)) return DistortedVertices[point];
      
        

        var depth = GetDepthAtPoint(point, true);

        var newPos = currentVertex;
        if (currentVertex.Z > depth) newPos.Z = (float)depth; // compare potential explosion distortion to actual position 
        DistortedVertices[point] = newPos;
        return newPos;
    }

    public float GetDepthAtPoint(Vector2 point, bool addSmoothing)
    {

        var relCent = RelativeCenter(point);
        var position2d = new Vector2(point.X, point.Y);
        var distVec = position2d - relCent;
        var dist = distVec.Length();
        if (dist >= Radius) return 0;

        var depth = (float)(Math.Sqrt(Math.Pow(Radius, 2) - Math.Pow(dist, 2)) * -1);
        if (addSmoothing)
        {
            var edgeSmooth = float.Clamp((Radius - dist) / 1f, 0, 1);
            depth *= edgeSmooth;
        }
        
        return depth;
    }


    public bool DoSubdivide(PolygonQuad node)
    {
        
        var depthRange = NodeDepthRangeMap[node.BoundingRect];
        var depthDifference = Math.Abs(depthRange.X - depthRange.Y);
        return depthDifference > MaxDepthDifference && node.GetWidth() > node.MinimumQuadWidth;
    }
    public bool DoWipeChildren(PolygonQuad node)
    {
        return DoWipeChildrenMap[node.BoundingRect];        

    }
    


    public bool IsActiveForNode(PolygonQuad node)
    {
        bool overlapsC1 = gUtils.CircleOverlapsRect(node.BoundingRect, Center1, Radius);
        if (overlapsC1) return true; 
        if (Center1 == Center2) { return false; }

        bool overlapsC2 = gUtils.CircleOverlapsRect(node.BoundingRect, Center2, Radius);
        if (overlapsC2) return true;

        var mergeResult = Geometry2D.MergePolygons(gUtils.PolygonFromRect(node.BoundingRect), MiddlePoly);
        return mergeResult.Count == 1; 

    }

    

}

