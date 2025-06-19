using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;

public partial class EdgeWrapMeshDistorter : QuadTreeMeshDistorter
{

    public float EdgeRadius = 1; 
    public float EdgeExtension = 2; 
    private float edgeRatio;
    private Curve3D edgeRadiusCurve = (Curve3D)GD.Load("uid://c6avem4lbyumt").Duplicate();
    
    public Dictionary<PolygonQuad, List<LineSegment>> NodeEdgeMap = new();
    
    public EdgeWrapMeshDistorter(PolygonQuadMesh quadMesh, float edgeRadius = 1, float edgeExtension = 2) : base(quadMesh)
    {

        NodeEdgeMap[QuadMesh.RootQuad] = gUtils.LineSegmentsFromPolygon(QuadMesh.RootQuad.Polygons[0]);
        EdgeExtension = edgeExtension;
        EdgeRadius = edgeRadius;
        SetupCurve();

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

    protected override bool DoTraverseNode(PolygonQuad node)
    {
        if (!NodeEdgeMap.ContainsKey(node)) NodeEdgeMap[node] = gUtils.SortLineSegmentsByDistanceToRect(node.BoundingRect, NodeEdgeMap[node.Parent], EdgeRadius);

        // continue traverse so long as any edge data exists
        return NodeEdgeMap[node].Count != 0;
    }

    
    protected override float GetTargetDetailLevel(PolygonQuad node)
    {
        // assumes nodeEdgeMap  will already contain entry for node being passed to this function

        var closestEdgeDelta = gUtils.ShortestDistanceBetweenSegmentAndRect(node.BoundingRect, NodeEdgeMap[node][0]);
        var edgeProgress = (float)(EdgeRadius - closestEdgeDelta) * edgeRatio;
        edgeProgress = (float)Math.Pow(float.Clamp(edgeProgress, 0, 1), 2);

        // lerps from 0 (min quad size) to 8 for now,
        // TODO: more robust system for picking quad size
        var widthTarget = float.Lerp(0, 8, 1 - edgeProgress);
        return widthTarget;
    }

    protected override Vector3 GetDistortedVertex(Vector2 point, PolygonQuad faceNode)
    {
        double nearestEdgeSqr = double.MaxValue;
        var edgeInfluenceSquare = EdgeRadius * EdgeRadius;

        Vector2 nearestEdgeDirection = Vector2.Zero;

        var edgeList = NodeEdgeMap[faceNode];

        // var vertex = new IndexedVertex() { SourcePosition = point, Position = new Vector3(point.X , point.Y, 0) };
        var vertexPos = new Vector3(point.X, point.Y, 0);

        foreach (var lineSeg in edgeList)
        {
            var norm = lineSeg.GetNormal();
            var offset = norm * 0.001f;  // offset values away from edge to avoid divide-by-zero
            var l1 = lineSeg.Start + offset;
            var l2 = lineSeg.End + offset;

            var closePoint = Geometry2D.GetClosestPointToSegment(point, l1, l2);

            var vecToLine = closePoint - point;
            if (Math.Abs(vecToLine.AngleTo(norm)) > Math.PI / 2) continue;

            var distSqr = vecToLine.LengthSquared();

            if (distSqr < edgeInfluenceSquare)
            {
                if (distSqr < nearestEdgeSqr) nearestEdgeSqr = distSqr;
                var inverseLengthWeightedVector = (edgeInfluenceSquare - distSqr) / edgeInfluenceSquare * vecToLine.Normalized(); // longer vectors become smaller in weighting
                var angleProjectedWeightedVector = inverseLengthWeightedVector.Project(norm); // further lessen in weighting if direction to edge is indirect 
                // if (angleProjectedWeightedVector.Dot(norm) < 0) continue;
                nearestEdgeDirection += angleProjectedWeightedVector;
            }

        }
        nearestEdgeDirection = nearestEdgeDirection.Normalized();
        var nearestEdgeDelta = Math.Sqrt(nearestEdgeSqr);
        if (nearestEdgeDirection != Vector2.Zero)
        {
            var curveProgress = (float)(EdgeRadius - nearestEdgeDelta) * edgeRatio;
            Transform3D curvePointTransform = edgeRadiusCurve.SampleBakedWithRotation(curveProgress);

            var edgePos2D = point + (float)nearestEdgeDelta * nearestEdgeDirection;

            var curveOrigin2D = edgePos2D - (nearestEdgeDirection * EdgeRadius);
            var curveOrigin3D = gUtils.AddDepth(curveOrigin2D, 0);

            var curvePos = curvePointTransform.Origin;
            var rotatedCurvePos = curvePos.Rotated(Vector3.Back, Vector2.Right.AngleTo(nearestEdgeDirection));

            var curveNormal = curvePointTransform.Basis.X;
            var rotatedNormal = curveNormal.Rotated(Vector3.Back, Vector2.Right.AngleTo(nearestEdgeDirection));
            vertexPos = rotatedCurvePos + curveOrigin3D;
            // vertex.Normal = rotatedNormal.Normalized();
            // vertex.VertexColor = (vertex.Normal + new Vector3(1, 1, 1)) / 2;
            if (nearestEdgeDelta < 0.005)
            {
                vertexPos.Z -= EdgeExtension;
            }

        }

        return vertexPos;
    }


    
}


