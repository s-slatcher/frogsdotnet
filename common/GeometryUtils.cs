using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Serialization;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;


public partial class GeometryUtils : GodotObject
{
    //gives actual modulo not remainder (to get a wrapping array index)
    int Mod(int x, int m) {
        return (x%m + m)%m;
    }

    public Vector3 AddDepth(Vector2 vec2, float depth = 0f)
    {
        return new Vector3(vec2.X, vec2.Y, 0);
    }
    
    public static Rect2 RectFromPolygon(Vector2[] polygon)
    {
        Rect2 rect = new();

        Vector2 min = new(int.MaxValue, int.MaxValue);
        Vector2 max = min * -1;

        foreach (Vector2 point in polygon)
        {
            max.X = Math.Max(max.X, point.X);
            max.Y = Math.Max(max.Y, point.Y);

            min.X = Math.Min(min.X, point.X);
            min.Y = Math.Min(min.Y, point.Y);
        }
        rect.Position = min;
        rect.End = max;

        return rect;
    }

    public Rect2I RectIFromPolygon(Vector2[] polygon)
    {
        Rect2I rectI = new();
        Rect2 rect = RectFromPolygon(polygon);
        var grownRect = rect.Grow(1);
        
        rectI.Position = new Vector2I((int)grownRect.Position.X, (int)grownRect.Position.Y);
        rectI.Size = new Vector2I((int)grownRect.Size.X, (int)grownRect.Size.Y);
        
        return rectI;
    }

    public Vector2[] PolygonFromRect(Rect2 rect)
    {
        
        Vector2[] vecArray = [
            rect.Position,
            rect.Position + new Vector2(rect.Size.X, 0),
            rect.End,
            rect.End + new Vector2( -rect.Size.X, 0)
        ];

        return vecArray;
    }
    public Vector2[] PolygonFromRectI(Rect2I rect)
    {   
        Vector2[] vecArray = [
            rect.Position,
            rect.Position + new Vector2(rect.Size.X, 0),
            rect.End,
            rect.End + new Vector2( -rect.Size.X, 0)
        ];

        return vecArray;

    }

    public Rect2 RectFromCircle(Vector2 center, float radius)
    {
        var rad_vec = new Vector2(radius, radius);
        var rect2 = new Rect2();
        rect2.Position = center + rad_vec;
        rect2.End = center - rad_vec;
        rect2 = rect2.Abs();
        return rect2;
    }

    public Vector2[] ScalePolygon(Vector2[] polygon, Vector2 scaling)
    {
        return polygon.Select(point => point * scaling).ToArray();
    }

    public Vector2[] TranslatePolygon(Vector2[] polygon, Vector2 translation)
    {
        return polygon.Select(point => point + translation).ToArray();
    }

    public Vector2[] RotatePolygon(Vector2[] polygon, float rotation)
    {
        new Transform2D();
        return polygon.Select(point => point.Rotated(rotation)).ToArray();
    }


    public double AreaOfTriangle(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return AreaOfTriangle3D(AddDepth(p1), AddDepth(p2), AddDepth(p3));
    }
    public double AreaOfTriangle3D(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        double a = (p1 - p2).Length();
        double b = (p2 - p3).Length();
        double c = (p3 - p1).Length();
        double s = (a + b + c)/2;
        return Math.Sqrt( s * (s-a) * (s-b) * (s-c) );  
    }

    
    //-----------------------------------------------------------------
    // Boolean operations (e.g., clip, intersect, merge) on polygons or on groups of polygons 
    //-----------------------------------------------------------------

    public List<Vector2[]> ClipPolygonRecursive(Vector2[] basePolygon, Vector2[] clippingPolygon)
    {
        
        List<Vector2[]> clippedPolygonList = [..Geometry2D.ClipPolygons(basePolygon, clippingPolygon)];  

        // Clockwise polygon at index 1 means clipping failed due to clip producing enclosed hole in shape
        if (clippedPolygonList.Count > 1 && Geometry2D.IsPolygonClockwise( clippedPolygonList[1] ))
        {
            // recursively break down basePoly and re-attempt carve until IsPolygonClockwise fails
            clippedPolygonList = new();
            List<Vector2[]> splitPolygons = SplitPolygons(basePolygon, clippingPolygon); 
            clippedPolygonList.AddRange(splitPolygons.SelectMany(p => ClipPolygonRecursive(p, clippingPolygon)));
            
        }
        
        return clippedPolygonList;

    }

    public List<Vector2[]> ClipPolygonGroupsRecursive(List<Vector2[]> basePolygonList, List<Vector2[]> clippingPolygonList)
    {
        var basePolygonTotalList = new List<Vector2[]>(basePolygonList);

        foreach (var clippingPolygon in clippingPolygonList)
        {
            var newCarveTotalList = new List<Vector2[]>();

            foreach (var basePolygon in basePolygonTotalList)
            {
                newCarveTotalList.AddRange( ClipPolygonRecursive(basePolygon, clippingPolygon) );
            }
            basePolygonTotalList = newCarveTotalList;
        }

        return basePolygonTotalList;
    }
    

    private List<Vector2[]> SplitPolygons(Vector2[] basePolygon, Vector2[] clippingPolygon)
    {
        Vector2 midPosition = GetAveragePosition(clippingPolygon);
        Rect2 baseRect = RectFromPolygon(basePolygon);
        
        
        float midOffsetLeft = (midPosition - baseRect.Position).X;
        float midOffsetRight = (midPosition - baseRect.End).X;

        var rightOffsetRect = new Rect2( baseRect.Position + new Vector2(midOffsetLeft, 0), baseRect.Size);
        var rightOffsetPolygon = PolygonFromRect(rightOffsetRect);

        var leftOffsetRect = new Rect2( baseRect.Position + new Vector2(midOffsetRight, 0), baseRect.Size);
        var leftOffsetPolygon = PolygonFromRect(leftOffsetRect);

        List<Vector2[]> splitResults = ClipPolygonRecursive(basePolygon, rightOffsetPolygon);
        splitResults.AddRange( ClipPolygonRecursive(basePolygon, leftOffsetPolygon) );

        return splitResults;

    }
    
    private Vector2 GetAveragePosition(Vector2[] clippingPolygon)
    {
        Vector2 accum = new(0,0);
        foreach(var point in clippingPolygon){
            accum += point;
        }
        return accum / clippingPolygon.Length;
    }
   

    public List<LineSegment> LineSegmentsFromPolygon(Vector2[] polygon)
    {
        List<LineSegment> lineSegments = new();
        
        if (!Geometry2D.IsPolygonClockwise(polygon)) polygon = polygon.Reverse().ToArray();
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = i+1 < polygon.Length ? polygon[i+1] : polygon[0];
            lineSegments.Add( new LineSegment(p1, p2) ); 
        }
        
        return lineSegments; 
    }

    public Vector2[] SimplifyPolygon(Vector2[] polygon, float epsilon)
    {
        var deletablePoints = new HashSet<int>();

        var segments = new List<(int, int)>
        {
            (0, polygon.Length - 1)
        };

        float epSquare = epsilon * epsilon;

        while (segments.Count > 0)
        {
            var segment = segments[^1];
            segments.RemoveAt(segments.Count - 1);

            int start = segment.Item1;
            int end = segment.Item2;

            if (end == start + 1) continue;

            var p1 = polygon[start];
            var p2 = polygon[end];



            float maxDistSquare = 0;
            int maxDistIndex = -1;

            for (int i = start + 1; i < end; i++)
            {
                var p = polygon[i];
                var closePoint = Geometry2D.GetClosestPointToSegment(p, p1, p2);
                var distSquare = closePoint.DistanceSquaredTo(p);
                if (maxDistSquare < distSquare)
                {
                    maxDistSquare = distSquare;
                    maxDistIndex = i;
                }
            }

            if (maxDistSquare > epSquare)
            {
                segments.Add((start, maxDistIndex));
                segments.Add((maxDistIndex, end));
            }
            else
            {
                for (int i = start + 1; i < end; i++)
                {
                    deletablePoints.Add(i);
                }
            }

        }

        var simplePoly = new List<Vector2>();
        for (int i = 0; i < polygon.Length; i++)
        {
            if (deletablePoints.Contains(i)) continue;
            simplePoly.Add(polygon[i]);
        }
        return simplePoly.ToArray();

    }


    //-----------------------------------------------------------------
    // Rect2 and LineSegment overlapping checks and distance checks 
    //-----------------------------------------------------------------

    public List<LineSegment> FilterLineSegmentsByRectIntersection(List<LineSegment> lineSegments, Rect2 rect)
    {
        List<LineSegment> intersectingLineSegments = new();
        List<LineSegment> rectLineSegments = LineSegmentsFromPolygon(PolygonFromRect(rect));

        foreach (var lineSegment in lineSegments)
        {
            if (rect.HasPoint(lineSegment.Start) || rect.HasPoint(lineSegment.End))
            {
                intersectingLineSegments.Add(lineSegment);
                continue;
            }

            foreach (var rectSegment in rectLineSegments)
            {
                Vector2 intersection = (Vector2)Geometry2D.SegmentIntersectsSegment(lineSegment.Start, lineSegment.End, rectSegment.Start, rectSegment.End);
                // documentation says to expect null on failed intersection but actually returns Vector2(0,0) ?  
                if (intersection != Vector2.Zero)
                {
                    intersectingLineSegments.Add(lineSegment);
                    break;
                }

            }

        }

        return intersectingLineSegments;

    }

    public double ShortestDistanceBetweenSegmentAndRect(Rect2 rect, LineSegment lineSegment)
    {
        var closestDistance = double.MaxValue;
        foreach ( var rectSeg in LineSegmentsFromPolygon(PolygonFromRect(rect)))
        {
            var dist = ShortestDistanceBetweenLineSegments(lineSegment, rectSeg);
            closestDistance = Math.Min(closestDistance, dist);
        }
        return closestDistance;
    }


    // should just handle two groups of line segments, run nested loop
    public Vector2[] ClosestPointsOnRectAndSegment(Rect2 rect, LineSegment lineSeg)
    {
        var segments = LineSegmentsFromPolygon(PolygonFromRect(rect));
        Vector2[] closePoints = [Vector2.Zero, Vector2.Zero];
        var closeDistance = float.MaxValue;
        foreach (var seg in segments)
        {
            var points = Geometry2D.GetClosestPointsBetweenSegments(seg.Start, seg.End, lineSeg.Start, lineSeg.End);
            var dist = (points[0] - points[1]).Length();
            closeDistance = Math.Min(dist, closeDistance);
            if (dist == closeDistance) closePoints = points;
        }

        return closePoints;
    }

    public List<LineSegment> SortLineSegmentsByDistanceToRect(Rect2 rect, List<LineSegment> lineSegments, float maxDistance)
    {
        List<LineSegment> reducedLineSegList = new();
        Dictionary<LineSegment, float> lineSegDistances = new();

        var rectLineSegments = LineSegmentsFromPolygon(PolygonFromRect(rect));

        foreach (var lineSeg in lineSegments)
        {

            var nearestEdgeDistance = double.MaxValue;
            if (rect.HasPoint(lineSeg.Start) || rect.HasPoint(lineSeg.End))
            {
                nearestEdgeDistance = 0;
                lineSegDistances[lineSeg] = 0;
            }
            else
            {
                nearestEdgeDistance = ShortestDistanceBetweenSegmentAndRect(rect, lineSeg);
            }

            if (nearestEdgeDistance < maxDistance)
            {
                reducedLineSegList.Add(lineSeg);
                lineSegDistances[lineSeg] = (float)nearestEdgeDistance;

            }

        }

        return reducedLineSegList.OrderBy(el => lineSegDistances[el]).ToList();

    }

    public double ShortestDistanceBetweenLineSegments(LineSegment segment1, LineSegment segment2)
    {
        Vector2[] closestPoints = Geometry2D.GetClosestPointsBetweenSegments(segment1.Start, segment1.End, segment2.Start, segment2.End);
        return (closestPoints[0] - closestPoints[1]).Length();
    } 

    
    public bool CircleOverlapsRect(Rect2 rect, Vector2 center, float radius)
    {
        
        var clampedX = Math.Clamp(center.X, rect.Position.X, rect.End.X);
        var clampedY = Math.Clamp(center.Y, rect.Position.Y, rect.End.Y);
        var closestPoint = new Vector2(clampedX, clampedY);
        var dist = (center - closestPoint).Length();
        return dist <= radius;
    }

    

    public bool IsPolygonsEqualApprox(Vector2[] poly1, Vector2[] poly2)
    {
        if (poly1.Length != poly2.Length) return false;
        return new HashSet<Vector2>(poly1).SetEquals(new HashSet<Vector2>(poly2));
    }



    //-----------------------------------------------------------------
    // Curve2D transformations and conversion from polygon
    //-----------------------------------------------------------------

    public Curve2D PointsToCurve(Vector2[] points, float smoothingFactor = 0, bool curveIsClosed = true)
    {

        int pointLen = points.Length;
        var smoothedCurve = new Curve2D();
        smoothingFactor = Math.Clamp(smoothingFactor, 0, 1.5f);  
        
        for (int i = 0; i < points.Length; i++)
        {

            var p = points[i];
            var last = points[ Mod(i-1, pointLen) ];
            var next = points[ Mod(i+1, pointLen) ];

            var vecLast = p - last;
            var vecNext = next - p;

            // ignore previous or next point direction if i is first or last point in curve respectively, and curve is not closed. 
            if (!curveIsClosed && i == 0 ) vecLast = vecNext * -1;
            if (!curveIsClosed && i == pointLen-1) vecNext = vecLast * -1;


    
            var vecAvg = ((vecLast.Normalized() ) + (vecNext.Normalized()) ) / 2;
            // var vecAvg = ((vecLast ) + (vecNext ) ) / 2;
            var handleDir = vecAvg.Normalized();


            // if (vecLast.AngleTo(vecNext) > Math.PI/2;
            
            // var handleAngle = (Math.PI - angle) / 2;
            // bool nextIsShorter = vecLast.Length() > vecNext.Length();
            // float angle = 0;
            // if (nextIsShorter) angle = handleDir.AngleTo(vecNext);
            // else angle = (handleDir).AngleTo(vecLast);
            var shortLength = Math.Min(vecNext.Length(), vecLast.Length());
            
            

            // var handleNextLength = shortLength * 0.5 / Math.Cos(angle) * smoothingFactor ;
            // var handleLastLength = shortLength * 0.5 / Math.Cos(angle) * smoothingFactor ;
            var handleNextLength = (vecNext.Length() * 0.5) / Math.Cos(handleDir.AngleTo(vecNext)) * smoothingFactor ;
            var handleLastLength = (vecLast.Length() * 0.5) / Math.Cos((-1 * handleDir).AngleTo(-1 * vecLast)) * smoothingFactor ;

            var handleOut = handleDir * (float)handleNextLength;
            var handleIn = handleDir * -1 * (float)handleLastLength;            
            smoothedCurve.AddPoint(p, handleIn, handleOut );
        }

        if (!curveIsClosed) return smoothedCurve;
        else
        {
            // smoothly enclose the shape by splitting the that last point's 'in' and 'out' handles across two nearly overlapping points
            // var delta = 0.1f;
            // var firstPointPos = smoothedCurve.GetPointPosition(0);
            // var firstInVec = smoothedCurve.GetPointIn(0);
            // smoothedCurve.SetPointIn(0, Vector2.Zero);
            // var appendedPointPos = firstPointPos + firstInVec.Normalized() * delta;
            // smoothedCurve.AddPoint(appendedPointPos, firstInVec, Vector2.Zero); 
            return smoothedCurve;
        }
        
    }

    public Curve2D ScaleCurve(Curve2D curve, Vector2 scale)
    {
        var newCurve = new Curve2D();
        for (int i = 0; i < curve.PointCount; i++)
        {
            var pointPos = curve.GetPointPosition(i);
            var pointIn = curve.GetPointIn(i);
            var pointOut = curve.GetPointOut(i);
            
            newCurve.AddPoint(pointPos * scale);
            newCurve.SetPointIn(i, pointIn * scale);
            newCurve.SetPointOut(i, pointOut * scale);
        }
        return newCurve;
    }

    public Curve2D TranslateCurve(Curve2D curve, Vector2 translation)
    {
        var translatedCurve = new Curve2D();
        for (int i = 0; i < curve.PointCount; i++)
        {
            translatedCurve.AddPoint(curve.GetPointPosition(i) + translation);
            translatedCurve.SetPointIn(i, curve.GetPointIn(i));
            translatedCurve.SetPointOut(i, curve.GetPointOut(i));
        }
        return translatedCurve;
    } 
    

    public Curve2D SliceCurve(Curve2D curve, int from, int to, bool repositionStartPoint)
    {
        var slicedCurve = curve.Duplicate() as Curve2D;
        var translate = slicedCurve.GetPointPosition(0) - slicedCurve.GetPointPosition(from);
        for (int i = curve.PointCount-1; i > -1; i--)
        {
            if (i > to || i < from) slicedCurve.RemovePoint(i);
            else {
                if (repositionStartPoint) 
                {
                    slicedCurve.SetPointPosition(i, slicedCurve.GetPointPosition(i) + translate );
                }
            }
        }
        return slicedCurve;
    }


    public Curve2D RotateCurve(Curve2D curve, float rotation, float? customHandleRotation = null)
    {
        var rotatedCurve = new Curve2D();
        if (customHandleRotation == null) customHandleRotation = rotation;
        for (int i = 0; i < curve.PointCount; i++)
        {
            var pointPos = curve.GetPointPosition(i).Rotated(rotation);
            var pointIn = curve.GetPointIn(i);
            var pointOut = curve.GetPointOut(i);
            
            pointIn = curve.GetPointIn(i).Rotated((float)customHandleRotation);
            pointOut = curve.GetPointOut(i).Rotated((float)customHandleRotation);
            
            rotatedCurve.AddPoint(pointPos);
            rotatedCurve.SetPointIn(i, pointIn);
            rotatedCurve.SetPointOut(i, pointOut);
        }
        return rotatedCurve;
    }

}
