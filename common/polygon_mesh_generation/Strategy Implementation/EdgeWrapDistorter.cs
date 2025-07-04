using Godot;
using System;
using System.Collections.Generic;

public partial class EdgeWrapDistorter : GodotObject, IQuadMeshDistorter
{

    public float EdgeRadius = 1;
    public float EdgeExtension = 2;
    private float edgeRatio;
    private Curve3D edgeRadiusCurve = (Curve3D)GD.Load("uid://c6avem4lbyumt").Duplicate();
    GeometryUtils gUtils = new();
    public HashSet<Vector2> DistortedVertices = new();


    public Dictionary<Rect2, List<LineSegment>> EdgesByRectMap = new();
    public float MaxDepthDifference = 0.5f;

    public Dictionary<Rect2, Vector2> DepthRangeMap = new();


    public EdgeWrapDistorter(float edgeRadius = 1, float edgeExtension = 1)
    {
        EdgeRadius = edgeRadius;
        EdgeExtension = edgeExtension;
        SetupCurve();
    }


    // SETS DEPTH RANGE BEFORE KNOW IF ACTIVE OR NOT.. WHICH IS WHY IT IS SOMETIMES EMPTY WHEN CHECKED IN INDEXING
    public bool IndexNode(PolygonQuad node, List<IQuadMeshDistorter> activeDistortersList)
    {
        SetNodeEdgeData(node);
        SetDepthRange(node);
        return EdgesByRectMap[node.BoundingRect].Count > 0;
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

    public Vector3 DistortVertex(Vector2 point, Vector3 currentVertex, PolygonQuad node)
    {

        if (DistortedVertices.Contains(point)) return currentVertex;
        else DistortedVertices.Add(point);


        double nearestEdgeSqr = double.MaxValue;
        var edgeInfluenceSquare = EdgeRadius * EdgeRadius;

        Vector2 nearestEdgeDirection = Vector2.Zero;

        var edgeList = EdgesByRectMap[node.BoundingRect];



        var newVertex = currentVertex;

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
            newVertex = rotatedCurvePos + curveOrigin3D;
            // vertex.Normal = rotatedNormal.Normalized();
            // vertex.VertexColor = (vertex.Normal + new Vector3(1, 1, 1)) / 2;
            if (nearestEdgeDelta < 0.005)
            {
                newVertex.Z -= EdgeExtension;
            }

        }

        return newVertex;
    }

    public bool DoSubdivide(PolygonQuad node)
    {
 
        var depthRange = DepthRangeMap[node.BoundingRect];
        var depthDifference = Math.Abs(depthRange.X - depthRange.Y);
        return depthDifference > MaxDepthDifference && node.GetWidth() > node.MinimumQuadWidth;
        // return node.GetWidth() > GetTargetWidth(node) && node.GetWidth() > node.MinimumQuadWidth;
    }

    private float GetTargetWidth(PolygonQuad node)
    {
        var closestEdgeDelta = gUtils.ShortestDistanceBetweenSegmentAndRect(node.BoundingRect, EdgesByRectMap[node.BoundingRect][0]);
        var edgeProgress = (float)(EdgeRadius - closestEdgeDelta) * edgeRatio;
        edgeProgress = (float)Math.Pow(float.Clamp(edgeProgress, 0, 1), 2);

        // lerps from 0 (min quad size) to 8
        var widthTarget = float.Lerp(0, 8, 1 - edgeProgress);
        return widthTarget;
    }


    public bool IsActiveForNode(PolygonQuad node)
    {
        SetNodeEdgeData(node);
        // GD.Print(node.BoundingRect);
        return EdgesByRectMap[node.BoundingRect].Count > 0;

    }

    private void SetNodeEdgeData(PolygonQuad node)
    {
        if (EdgesByRectMap.ContainsKey(node.BoundingRect)) return;

        else if (node.Parent == null) EdgesByRectMap[node.BoundingRect] = gUtils.LineSegmentsFromPolygon(node.Root.Polygons[0]);

        else
        {
            var edgeMap = gUtils.SortLineSegmentsByDistanceToRect(node.BoundingRect, EdgesByRectMap[node.Parent.BoundingRect], EdgeRadius);
            EdgesByRectMap[node.BoundingRect] = edgeMap;
        }

    }

    public bool DoWipeChildren(PolygonQuad node)
    {
        return false;
    }

    

    //TODO : this is ugly and messy, doesn't deal well with empty edge lists
    private Vector2 SetDepthRange(PolygonQuad node)
    {
        float shallow;
        float deep;
        Vector2 depthRange;

        if (EdgesByRectMap[node.BoundingRect].Count == 0)
        {
            depthRange = new Vector2(0, 0);
            DepthRangeMap[node.BoundingRect] = depthRange;
            return depthRange;
        }

        Vector2[] closePoints = gUtils.ClosestPointsOnRectAndSegment(node.BoundingRect, EdgesByRectMap[node.BoundingRect][0]);
        var dist = (closePoints[0] - closePoints[1]).Length();
        var maxEdgeProgress = (EdgeRadius - dist) * edgeRatio;
        maxEdgeProgress = float.Min(maxEdgeProgress, 1);

        if (maxEdgeProgress < 0) depthRange = Vector2.Zero;
        else
        {
            float maxDistFromEdge = dist + node.GetWidth() * float.Sqrt(2);
            var minEdgeProgress = (EdgeRadius - maxDistFromEdge) * edgeRatio;
            minEdgeProgress = float.Max(0, minEdgeProgress);


            shallow = edgeRadiusCurve.SampleBaked(minEdgeProgress).Z;
            deep = edgeRadiusCurve.SampleBaked(maxEdgeProgress).Z;
            if (dist < 0.05) deep -= 1000;
            depthRange = new Vector2(shallow, deep);

        }

        DepthRangeMap[node.BoundingRect] = depthRange;
        return depthRange;

    }

    public Vector2 GetDepthRange(PolygonQuad node)
    {
        if (!DepthRangeMap.ContainsKey(node.BoundingRect)) SetDepthRange(node);
        return DepthRangeMap[node.BoundingRect]; 
    }

}
