using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class ExplosionDistorter(Vector2 center, float radius) : GodotObject, IQuadMeshDistorter
{

    GeometryUtils gUtils = new();
    public float Radius = radius;
    public Vector2 Center = center;
    public HashSet<Vector2> DistortedVertices = new();
    
    public float maxQuadRadiusRatio = 0.75f;

    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
    {
        
        if (DistortedVertices.Contains(point)) return currentVertex;
        else DistortedVertices.Add(point);

        var position2d = new Vector2(currentVertex.X, currentVertex.Y);
        var distVec = position2d - Center;
        var dist = distVec.Length(); //distance from explosion center to where vertex would be if not distorted by anything else 
        var newPos = currentVertex;
        
        if (dist < Radius)
        {
            // vertex.Z = 1;
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

        var centerDistances = gUtils.PolygonFromRect(node.BoundingRect).Select(point => (Center - point).Length());
        var furthestDist = centerDistances.Max();
        var furthestDistRatio = Math.Clamp(furthestDist / Radius, 0, 1);
        var cubedRatio = Math.Pow(furthestDistRatio, 3);
        var quadSizeTarget = Mathf.Lerp(minQuadSize, maxQuadSize, 1 - (float)cubedRatio);
        
        return quadSizeTarget;
    }

    public bool IsActiveForNode(PolygonQuad node)
    {
        var rect = node.BoundingRect;
        var clampedX = Math.Clamp(Center.X, rect.Position.X, rect.End.X);
        var clampedY = Math.Clamp(Center.Y, rect.Position.Y, rect.End.Y);
        var closestPoint = new Vector2(clampedX, clampedY);
        // if (closestPoint == Center) return true;

        var allowance = 0.1f;
        var dist = (Center - closestPoint).Length();

        return dist <= Radius + allowance;
    }

}


