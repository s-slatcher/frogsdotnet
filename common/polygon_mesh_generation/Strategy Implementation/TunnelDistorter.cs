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
    public Dictionary<Rect2, Vector2> DepthRangeMap = new();
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


    
    public bool IndexNode(PolygonQuad node, List<IQuadMeshDistorter> activeDistortersList)
    {
        var depthRange = SetDepthRange(node);
        return depthRange.X != 0 || depthRange.Y != 0;
    }

    

    private Vector2 SetDepthRange(PolygonQuad node)
    {
        var region = node.BoundingRect;
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
        DepthRangeMap[region] = depthRange;
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
        
        var depthRange = DepthRangeMap[node.BoundingRect];
        var depthDifference = Math.Abs(depthRange.X - depthRange.Y);
        return depthDifference > MaxDepthDifference && node.GetWidth() > node.MinimumQuadWidth;
    }
    public bool DoWipeChildren(PolygonQuad node)
    {
        return DoWipeChildrenMap[node.BoundingRect];

    }

    
    public Vector2 GetDepthRange(PolygonQuad node)
    {
        if (!DepthRangeMap.ContainsKey(node.BoundingRect)) SetDepthRange(node);
        return DepthRangeMap[node.BoundingRect]; 
    }

    

}

