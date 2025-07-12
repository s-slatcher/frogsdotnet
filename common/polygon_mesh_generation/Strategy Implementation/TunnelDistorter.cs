using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Linq;

public partial class TunnelDistorter : GodotObject, IQuadMeshDistorter
{

    public Vector2 Center1;
    public Vector2 Center2;
    public LineSegment CenterLine;
    public float Radius;
    public Vector2[] MiddlePoly;

    public float MaxDepthDifference = 1.5f;

    public Dictionary<Rect2, Vector2> DepthRangeMap = new();
    public Dictionary<Rect2, bool> DoWipeChildrenMap = new();

    public Dictionary<Vector2, float> PointDepthMap = new();

    Dictionary<Vector2, DistortData> PointDataMap = new();


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
        return depthRange != Vector2.Zero;
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
            deep = GetDepthAtPoint(points[0]);
        }

        // find shallowest point (must be a corner?)
        var cornerDepths = gUtils.PolygonFromRect(region).ToList().Select(point => GetDepthAtPoint(point)).ToList();
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

    public Vertex DistortVertex(Vector2 point, Vertex currentVertex, PolygonQuad node)
    {
        var depth = GetDepthAtPoint(point);
        // IndexPoint(point);

        if (currentVertex.Position.Z > depth)
        {
            currentVertex.Position = PointDataMap[point].Position;
            currentVertex.Normal = PointDataMap[point].Normal;
        }
    
        return currentVertex;
    }

    private void IndexPoint(Vector2 point)
    {
        throw new NotImplementedException();
    }


    public float GetDepthAtPoint(Vector2 point)
    {
        if (PointDepthMap.ContainsKey(point)) return PointDepthMap[point];



        var relCent = RelativeCenter(point);
        var position2d = new Vector2(point.X, point.Y);
        var distVec = position2d - relCent;
        var dist = distVec.Length();
        if (dist >= Radius) return 0;
        var depth = (float)(Math.Sqrt(Math.Pow(Radius, 2) - Math.Pow(dist, 2)) * -1);


        var edgeSmooth = float.Clamp((Radius - dist) / 1f, 0, 1);
        depth *= edgeSmooth;

        var pos3d = new Vector3(point.X, point.Y, depth);

        var normal = (gUtils.AddDepth(relCent) - pos3d)
                    .Normalized()
                    .Lerp(Vector3.Back, 1.0f - edgeSmooth);


        PointDataMap[point] = new DistortData(pos3d, normal);

        PointDepthMap[point] = depth;
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

internal class DistortData(Vector3 position, Vector3 normal)
{
    public Vector3 Position = position;
    public Vector3 Normal = normal;
}

