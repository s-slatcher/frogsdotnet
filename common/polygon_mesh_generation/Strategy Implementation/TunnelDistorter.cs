using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TunnelDistorter : GodotObject, IQuadMeshDistorter
{

    public Vector2 Center1;
    public Vector2 Center2;
    public float Radius;
    public Rect2 TunnelEnclosingRect;
    public float maxQuadRadiusRatio = 0.75f;
    public HashSet<Vector2> DistortedVertices = new();

    public GeometryUtils gUtils = new();

    public TunnelDistorter(Vector2 center1, Vector2 center2, float radius)
    {
        
            Center1 = center1;
            Center2 = center2;
        
        Radius = radius;

        SetRect();
       
    }

    private void SetRect()

    {
        var centerRect1 = gUtils.RectFromCircle(Center1, Radius);
        var centerRect2 = gUtils.RectFromCircle(Center2, Radius);

        var poly1 = gUtils.PolygonFromRect(centerRect1).ToList();
        poly1.AddRange(gUtils.PolygonFromRect(centerRect2).ToList());
        var tunnelRect = GeometryUtils.RectFromPolygon(poly1.ToArray());

        TunnelEnclosingRect = tunnelRect;

        
    }

    private Vector2 RelativeCenter(Vector2 point)
    {
        return Geometry2D.GetClosestPointToSegment(point, Center1, Center2);
    }

    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
    {

        if (DistortedVertices.Contains(point)) return currentVertex;
        else DistortedVertices.Add(point);

        var relCent = RelativeCenter(point);

        var position2d = new Vector2(currentVertex.X, currentVertex.Y);
        var distVec = position2d - relCent;
        var dist = distVec.Length(); //distance from explosion center to where vertex would be if not distorted by anything else 
        var newPos = currentVertex;

        if (dist < Radius)
        {
            var depth = Math.Sqrt(Math.Pow(Radius, 2) - Math.Pow(dist, 2)) * -1;
            if (currentVertex.Z > depth) newPos.Z = (float)depth; // compare potential explosion distortion to actual position 
        }

        return newPos;
    }


    public bool DoSubdivide(PolygonQuad node)
    {
        return node.GetWidth() > GetTargetDetailLevel(node) && node.GetWidth() > node.MinimumQuadWidth;
    }


    private float GetTargetDetailLevel(PolygonQuad node)
    {
        
        var maxQuadSize = Radius * maxQuadRadiusRatio;
        var minQuadSize = node.MinimumQuadWidth;
        if (minQuadSize >= maxQuadSize) return minQuadSize;

        var centerDistances = gUtils.PolygonFromRect(node.BoundingRect).Select(point => (RelativeCenter(point) - point).Length());
        var furthestDist = centerDistances.Max();
        var furthestDistRatio = Math.Clamp(furthestDist / Radius, 0, 1);
        var cubedRatio = Math.Pow(furthestDistRatio, 3);
        var quadSizeTarget = Mathf.Lerp(minQuadSize, maxQuadSize, 1 - (float)cubedRatio);
        
        return quadSizeTarget;
    }

    public bool IsActiveForNode(PolygonQuad node)
    {
        // simple intersect check
        var node_rect = node.BoundingRect;
        var doesIntersect = TunnelEnclosingRect.Grow(0.1f).Intersects(node_rect);
        // if (!doesIntersect) GD.Print("node pos size: ", node_rect.Position, node_rect.Size);
        return doesIntersect;


    }

}

