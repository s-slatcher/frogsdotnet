using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;

public partial class EdgeWrapMeshDistort : GodotObject
{
    GeometryUtils gUtils = new();

    public PolygonQuadMesh QuadMesh;
    PolygonQuad NewRootNode; 
    
    public HashSet<Vector2> VerticesUpdated = new(); 
    public Dictionary<PolygonQuad, List<LineSegment>> NodeEdgeMap = new();
    
    public float EdgeRadius = 1; 
    public float EdgeExtension = 2; 
    private float edgeRatio;
    private Curve3D edgeRadiusCurve = (Curve3D)GD.Load("uid://c6avem4lbyumt").Duplicate();
    
    

    public EdgeWrapMeshDistort(PolygonQuadMesh quadMesh, float edgeRadius = 1, float edgeExtension = 2)
    {
        // duplicates the PolygonQuad 
        QuadMesh = new PolygonQuadMesh(quadMesh);
        
        NewRootNode = QuadMesh.RootQuad.Duplicate();
        QuadMesh.RootQuad = NewRootNode;

        NodeEdgeMap[NewRootNode] = gUtils.LineSegmentsFromPolygon(NewRootNode.Polygons[0]);
        EdgeExtension = edgeExtension;
        EdgeRadius = edgeRadius;
        SetupCurve();
        
    }

    public void ApplyDistort()
    {
        DistortFacesRecurisive(NewRootNode);

    }

    private void SetupCurve()
    {
        edgeRadiusCurve.SetPointOut(0, edgeRadiusCurve.GetPointOut(0) * EdgeRadius);
        edgeRadiusCurve.SetPointPosition(1, edgeRadiusCurve.GetPointPosition(1) * EdgeRadius);
        edgeRadiusCurve.SetPointIn(1, edgeRadiusCurve.GetPointIn(1) * EdgeRadius);

        edgeRadiusCurve.BakeInterval = EdgeRadius / 100f;
        var length = edgeRadiusCurve.GetBakedLength();
        edgeRatio = length / EdgeRadius;
    }



    private void DistortFacesRecurisive(PolygonQuad node)
    {

        
        
        if (TargetDetailLevelMet(node) || node.GetWidth() == node.MinimumQuadWidth)
        {
            foreach (var point in node.Polygons.SelectMany(poly => poly)) DistortPoint(point);

            return;
        }

        if (node.HasChildren())
        {
            // replace nodes children with duplicates, to prepapre an alternatve branch in case of future subdivision
            for (int i = 0; i < node.Children.Count; i++) node.Children[i] = node.Children[i].Duplicate();

        }
        else node.Subdivide();
        
        foreach (var child in node.Children) DistortFacesRecurisive(child);

    }

    public bool TargetDetailLevelMet(PolygonQuad node)
    {
        return node.GetWidth() <= TargetDetailLevel(node);
    }

    private void DistortPoint(Vector2 point)
    {

        if (VerticesUpdated.Contains(point)) return;
        VerticesUpdated.Add(point);


        
        QuadMesh.IndexPoint(point, new Vector3(point.X, point.Y, 1));


    }

    

    private float TargetDetailLevel(PolygonQuad node)
    {
       
        if (!NodeEdgeMap.ContainsKey(node)) NodeEdgeMap[node] = gUtils.SortLineSegmentsByDistanceToRect(node.BoundingRect, NodeEdgeMap[node.Parent], EdgeRadius);
        if (NodeEdgeMap[node].Count == 0) return float.MaxValue;
        
        var closestEdgeDelta = gUtils.ShortestDistanceBetweenSegmentAndRect(node.BoundingRect, NodeEdgeMap[node][0]);
        var edgeProgress = (float)(EdgeRadius - closestEdgeDelta) * edgeRatio;
        edgeProgress = (float)Math.Pow(float.Clamp(edgeProgress, 0, 1), 2);
        // lerps from 0 (min quad size) to 8 for now, until better system for picking subdivide level
        var widthTarget = float.Lerp(0, 8, 1 - edgeProgress);

        
        return widthTarget;
    }
    

    public Vector3 UpdateVertex(Vector2 point, Vector3 VertexPosition)
    {
        throw new NotImplementedException();
    }


    public bool IsPointUpdated(Vector2 point)
    {
        return VerticesUpdated.Contains( point );
    }

    
}


