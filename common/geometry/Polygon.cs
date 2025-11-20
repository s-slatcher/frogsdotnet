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
    public Rect2 BoundingRect;
    List<Vector2> sourcePoints = new();
    PolygonPoint firstPoint;
    PolygonCollisionTree root;
    public HashSet<PolygonCollisionTree> leafNodeCache = new();

    List<Vector2> simplifiedPolyCache = new();

    GeometryUtils gu = new();
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



    public void SetPoints(IEnumerable<Vector2> points, float simplfyingEpsilon)
    {
        sourcePoints = points.ToList();
        this.simplfyingEpsilon = simplfyingEpsilon;
        BuildPolyLinkedList();
        BuildFastCollisionTree();

    }

    private void BuildPolyLinkedList()
    {
        // use first loop to find the bounding rect and build linked list
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;

        firstPoint = new PolygonPoint() { Position = sourcePoints[0] };
        var prev = firstPoint;
        for (int i = 1; i < sourcePoints.Count; i++)
        {
            minX = Math.Min(minX, sourcePoints[i].X);
            minY = Math.Min(minY, sourcePoints[i].Y);
            maxX = Math.Max(maxX, sourcePoints[i].X);
            maxY = Math.Max(maxY, sourcePoints[i].Y);

            var p = new PolygonPoint { Position = sourcePoints[i] };
            prev.nextPoint = p;
            p.prevPoint = prev;

            if (i == sourcePoints.Count - 1)
            {
                p.nextPoint = firstPoint;
                firstPoint.prevPoint = p;
            }

            prev = p;
        }
        var rectPos = new Vector2(minX, minY);
        BoundingRect = new Rect2(rectPos, new Vector2(maxX, maxY) - rectPos);

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
            

            // set points as key and pointing to each other
            var start = segment.Item1;
            var end = segment.Item2;
            start.isKey = end.isKey = true;
            start.nextKeyPoint = end;
            end.prevKeyPoint = start;

            // loop through all regular points between start & end, 
            // and find furthest point from the direct line connecting start to end 
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


            // take the most divergent point as next key point
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
            currPoint = currPoint.nextKeyPoint;
        }
        while (currPoint != firstPoint);

        return simplifiedPolyCache;
    }
    
    public void BuildFastCollisionTree()
    {
        var KeyPointLines = new List<(PolygonPoint, PolygonPoint)>();
        var curr = firstPoint;
        do
        {
            KeyPointLines.Add((curr, curr.nextKeyPoint));
            curr = curr.nextKeyPoint;
        }
        while (curr != firstPoint);

        root = new PolygonCollisionTree(BoundingRect, KeyPointLines, false);

        var queue = new List<PolygonCollisionTree>() { root };
        while (queue.Count > 0)
        {

            var quad = queue[^1];
            queue.RemoveAt(queue.Count - 1);
            if (quad.PolygonLines.Count > 0)
            {
                quad.Subdivide();
                queue.AddRange(quad.Children);
                if (quad.Children.Count == 0) leafNodeCache.Add(quad);
            }

        }
        
        

        
    }   

}


