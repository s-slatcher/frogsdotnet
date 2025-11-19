using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using Vector2 = Godot.Vector2;

public partial class Polygon : GodotObject
{
    List<Vector2> sourcePoints = new();
    PolygonPoint firstPoint;
    List<Vector2> simplifiedPolyCache = new();
    float simplfyingEpsilon = 0;

    // public float SimplifyingEpsilon
    // {
    //     get => simplfyingEpsilon;
    //     set
    //     {
    //         simplfyingEpsilon = value;
    //         SimplfyPolygon();
    //     }
    // }


    private void SimplfyPolygon()
    {
        var gu = new GeometryUtils();


        // if (simplfyingEpsilon == 0)
        // {
        //     simplfiedPolyCache = [.. sourcePoints];
        // }
        // else
        // {
        //     simplfiedPolyCache = gu.SimplifyPolygon(sourcePoints.ToArray(), simplfyingEpsilon).ToList();
        // }

        
        
        

    }


    public void SetPoints(IEnumerable<Vector2> points, float simplfyingEpsilon)
    {
        sourcePoints = points.ToList();
        this.simplfyingEpsilon = simplfyingEpsilon;
        BuildPolyLinkedList();

    }

    private void BuildPolyLinkedList()
    {
        firstPoint = new PolygonPoint() { Position = sourcePoints[0] };
        var prev = firstPoint;
        for (int i = 1; i < sourcePoints.Count; i++)
        {
            var p = new PolygonPoint { Position = sourcePoints[i] };
            prev.nextPoint = p;
            p.prevPoint = prev;

            if (i == sourcePoints.Count - 1) p.nextPoint = firstPoint;
        }

        // initiate first key points by linking in their opposite direction
        // run through list checking which point can be considered "key" and giving them their own link
        var lastPoint = firstPoint.prevPoint;
        var segments = new List<(PolygonPoint, PolygonPoint)>
        {
            (firstPoint, lastPoint)
        };
        firstPoint.prevKeyPoint = lastPoint;
        lastPoint.nextKeyPoint = firstPoint;

        while (segments.Count > 0)
        {
            var segment = segments[^1];
            segments.RemoveAt(segments.Count - 1);
            var start = segment.Item1;
            var end = segment.Item2;

            // set points as key and pointing to each other
            start.nextKeyPoint = end;
            end.prevKeyPoint = start;

            var curr = start.nextPoint;
            var maxDistSqr = simplfyingEpsilon;
            PolygonPoint nextKey = new();

            while (curr != end)
            {
                var p = curr.Position;
                var close = Geometry2D.GetClosestPointToSegment(p, start.Position, end.Position);
                var distSqr = close.DistanceSquaredTo(p);
                if (maxDistSqr < distSqr)
                {
                    maxDistSqr = distSqr;
                    nextKey = curr;
                }

                curr = curr.nextPoint;
            }
            

            // take the most divergent point from through line as next key point
            // if max dist remains unchanged, none of the points in between keys qualifed as key points
            if (maxDistSqr != simplfyingEpsilon)
            {
                segments.Add(new(start, nextKey));
                segments.Add(new(nextKey, end));
            }

        }

        


    }


    public List<Vector2> GetFullDetailPolygon()
    {
        return sourcePoints;
    }

    public List<Vector2> GetSimplifiedPolygon()
    {
        // linked list hasnt been built yet (empty point list)
        if (firstPoint == null) return new();
        // cache has already been built
        if (simplifiedPolyCache.Count != 0) return simplifiedPolyCache; 
        
        //build cache and return
        var currPoint = firstPoint;
        do
        {
            simplifiedPolyCache.Add(currPoint.Position);
            currPoint = firstPoint.nextKeyPoint;
        }
        while (currPoint != firstPoint);

        return simplifiedPolyCache;
    }

}


