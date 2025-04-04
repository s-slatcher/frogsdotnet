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
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;


public partial class GeometryUtils : GodotObject
{

    public Rect2 RectFromPolygon(Vector2[] polygon)
    {
        Rect2 rect = new();
        
        Vector2 min = new(int.MaxValue, int.MaxValue);
        Vector2 max = min * -1;

        foreach(Vector2 point in polygon)
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

    public Vector2[] ScalePolygon(Vector2[] polygon, Vector2 scaling)
    {
        return polygon.Select(point => point * scaling).ToArray();
    }

    public double AreaOfTriangle(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        double a = (p1 - p2).Length();
        double b = (p2 - p3).Length();
        double c = (p3 - p1).Length();
        double s = (a + b + c)/2;
        return Math.Sqrt( s * (s-a) * (s-b) * (s-c) );  
    }
    public double AreaOfTriangle3D(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        double a = (p1 - p2).Length();
        double b = (p2 - p3).Length();
        double c = (p3 - p1).Length();
        double s = (a + b + c)/2;
        return Math.Sqrt( s * (s-a) * (s-b) * (s-c) );  
    }


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

   
    public List<LineSegment> FilterLineSegmentsByRectIntersection( List<LineSegment> lineSegments, Rect2 rect )
    {
        List<LineSegment> intersectingLineSegments = new();
        List<LineSegment> rectLineSegments = LineSegmentsFromPolygon( PolygonFromRect(rect) );

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
            }
            else
            {
                foreach (var rectSeg in rectLineSegments)
                {
                    var distance = ShortestDistanceBetweenLineSegments(lineSeg, rectSeg);
                    nearestEdgeDistance = Math.Min(nearestEdgeDistance, distance);
                }  
            }

            if (nearestEdgeDistance < maxDistance) 
            {
               reducedLineSegList.Add(lineSeg);
               lineSegDistances[lineSeg] = 0;
            } 

        }

        return reducedLineSegList;

    }

    public double ShortestDistanceBetweenLineSegments(LineSegment segment1, LineSegment segment2)
    {
        Vector2[] closestPoints = Geometry2D.GetClosestPointsBetweenSegments(segment1.Start, segment1.End, segment2.Start, segment2.End);
        return (closestPoints[0] - closestPoints[1]).Length();
    } 

    public Vector3 AddDepth(Vector2 vec2, float depth = 0f)
    {
        return new Vector3(vec2.X, vec2.Y, 0);
    }

    public bool IsPolygonsEqualApprox(Vector2[] poly1, Vector2[] poly2)
    {
        if (poly1.Length != poly2.Length) return false;
        return new HashSet<Vector2>(poly1).SetEquals(new HashSet<Vector2>(poly2));
    }

    
}
