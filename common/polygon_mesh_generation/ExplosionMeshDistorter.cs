using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class ExplosionMeshDistorter : QuadTreeMeshDistorter
{

    public float Radius;
    public Vector2 Center;
    public Dictionary<PolygonQuad, float> QuadExplosionDeltaMap;
    public float maxQuadRadiusRatio = 0.5f;


    public ExplosionMeshDistorter(PolygonQuadMesh quadMesh, Vector2 center, float radius) : base(quadMesh)
    {
        Radius = radius;
        Center = center;
    }

    protected override Vector3 GetDistortedVertex(Vector2 point, PolygonQuad node)
    {
        
        var vertex = QuadMesh.GetVertex(point) ?? new Godot.Vector3(point.X, point.Y, 0);
        return DamageVertex(vertex);


    }

    protected override float GetTargetDetailLevel(PolygonQuad node)
    {
        var maxQuadSize = Radius * maxQuadRadiusRatio;
        var minQuadSize = node.MinimumQuadWidth;
        if (minQuadSize >= maxQuadSize) return minQuadSize;

        var centerDistances = gUtils.PolygonFromRect(node.BoundingRect).Select(point => (Center - point).Length());
        var furthestDist = centerDistances.Max();
        var furthestDistRatio = Math.Clamp(furthestDist / Radius, 0, 1);
        var quadSizeTarget = Mathf.Lerp(minQuadSize, maxQuadSize, 1 - furthestDistRatio);
        
        return quadSizeTarget;
    }

    protected override bool DoTraverseNode(PolygonQuad node)
    {
        
        var rect = node.BoundingRect;
        var clampedX = Math.Clamp(Center.X, rect.Position.X, rect.End.X);
        var clampedY = Math.Clamp(Center.Y, rect.Position.Y, rect.End.Y);
        var closestPoint = new Vector2(clampedX, clampedY);
        // if (closestPoint == Center) return true;


        var dist = (Center - closestPoint).Length();

        return dist <= Radius;
        
    }

    protected override bool DoSubdivide(PolygonQuad node)
    {
        
        return node.GetWidth() > GetTargetDetailLevel(node) && node.GetWidth() > node.MinimumQuadWidth;
    }

    private Vector3 DamageVertex(Vector3 vertex)
    {
        

        var position2d = new Vector2(vertex.X, vertex.Y);
        var distVec = position2d - Center;
        var dist = distVec.Length(); //distance from explosion center to where vertex would be if not distorted by anything else 

        if (dist < Radius)
        {
            // vertex.Z = 1;
            var depth = Math.Sqrt(Math.Pow(Radius, 2) - Math.Pow(dist, 2));
            if (vertex.Z < depth) vertex.Z = -(float)depth; // compare potential explosion distortion to actual position 
        }
        
        return vertex;
    }



}
