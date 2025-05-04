using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;


public partial class PolygonQuad : GodotObject
{

    private PolygonQuad(List<Vector2[]> polygons, Rect2 boundingRect)
    {
        Polygons = polygons;
        BoundingRect = boundingRect;

    }

    public PolygonQuad(PolygonQuad polyQuad)
    {
        
    }
    
    public Action Updated;

    public List<Vector2[]> Polygons;
    public List<Vector3[]> Vertices;
    public ExtrudedMesh QuadManager;
    public PolygonQuad Parent;
    public PolygonQuad Root;
    public Rect2 BoundingRect;
    private float minimumQuadWidth;
    private GeometryUtils gUtils = new();
    private bool containsEdgePolygon = true; 

    public Dictionary<string, PolygonQuad> Children = new()
    {   
        {"TL", null},
        {"TR", null},
        {"BL", null},
        {"BR", null}
    };
    

    public static PolygonQuad CreateRootQuad(Vector2[] polygon, float minQuadWidth = 20)
    {
        
        var polyRect = GetRootQuadRect(polygon, minQuadWidth);
    
        var quad = new PolygonQuad([polygon], polyRect)
        {
            minimumQuadWidth = minQuadWidth
        };
        quad.Root = quad;
        return quad;

    }

    private static Rect2 GetRootQuadRect(Vector2[] rootPolygon, float minQuadWidth)
    {
        var gUtils = new GeometryUtils();
        var polyRect = gUtils.RectFromPolygon(rootPolygon);
        polyRect = polyRect.Grow(1);
        polyRect = new Rect2
        (
            new((int)polyRect.Position.X, (int)polyRect.Position.Y), 
            new((int)polyRect.Size.X, (int)polyRect.Size.X)
        );
        
        float targetQuadWidth = Math.Max(polyRect.Size.X, polyRect.Size.Y);
        int currentMultiplier = 2;
        while (currentMultiplier * minQuadWidth <= targetQuadWidth) currentMultiplier *= 2;
        float normalizedRectWidth = currentMultiplier * minQuadWidth;    
        polyRect.Size = new Vector2(normalizedRectWidth, normalizedRectWidth);
        return polyRect;

    }



    public void Subdivide()
    {
        var offsetVector = BoundingRect.Size / 2;
        var baseVector = BoundingRect.Position;
        
        if (containsEdgePolygon) containsEdgePolygon = HasEdgePoly();
        if (offsetVector.X < minimumQuadWidth ) return;

        
        List<string> positionKeys = new(){"TL", "TR", "BR", "BL"};
        List<Vector2> keyOrderedPositionOffsets = new(){
            new(0,0),
            new(offsetVector.X, 0),
            offsetVector,
            new(0, offsetVector.Y)
        };
        
        
        for (int i = 0; i < keyOrderedPositionOffsets.Count; i++)
        {
            var childRect = new Rect2(keyOrderedPositionOffsets[i] + baseVector, offsetVector);
            var childPolyList = new List<Vector2[]>();
            // if (Polygons[0].Length == 0) childPolyList = [Polygons[0]];
            childPolyList.AddRange(GetChildPolygonSlices(childRect));
            if (childPolyList.Count == 0) continue; 
            // else childPolyList = [gUtils.PolygonFromRectI(childRect)];

            PolygonQuad childQuad = new(childPolyList, childRect)
            {
                containsEdgePolygon = containsEdgePolygon,
                minimumQuadWidth = minimumQuadWidth,
                QuadManager = QuadManager,
                Parent = this,
                Root = Root
            };
            
            Children[positionKeys[i]] = childQuad;
        };
        
        // stitching could run *after* full subdividing, so stitched aren't wasted on nodes that will end up subdividing themselves 
        //StitchPointsOnNeighborPolygons();
        

    }

    
    // public void StitchPointsOnNeighborPolygons()
    // {
    //     var start = BoundingRect.Position;
    //     var end = BoundingRect.End;
    //     float halfWidth = BoundingRect.Size.X/2;
        
    //     List<Vector2> stitchPoints = new()
    //     {
    //         start + new Vector2(halfWidth, 0),
    //         start + new Vector2(0, halfWidth),
    //         end - new Vector2(halfWidth, 0),
    //         end - new Vector2(0, halfWidth)
    //     };

    //     // search for target quad using point offset out of own bounds 
    //     var delta = minimumQuadWidth/2f;
    //     List<Vector2> stitchOffset = new()
    //     {
    //         new(0, -delta),
    //         new(-delta, 0),
    //         new(0, delta),
    //         new(delta, 0)
    //     };
        
    //     for (int i = 0; i < stitchPoints.Count; i++)
    //     {
    //         var quad = FindQuadWithPoint(stitchPoints[i] + stitchOffset[i]);
    //         if (quad == null) continue; 

    //         if (quad.BoundingRect.Size >= BoundingRect.Size)
    //         {
    //             quad.InsertPointOnPolygons(stitchPoints[i]);
                
    //         }
    //     }  
        
    // }

    public void SimplifyPolygon(){
        var onlyEdgesPoly = new List<Vector2>();
        foreach (var point in Polygons[0])
        {
            if (PolyPointIsOnGridEdge(point)) onlyEdgesPoly.Add(point);
        }
        Polygons[0] = onlyEdgesPoly.ToArray();
    }

    private List<Vector2[]> GetChildPolygonSlices(Rect2 childRect)
    {
        List<Vector2[]>  childPolyList = new(); 
        var childRectPoly = gUtils.PolygonFromRect(childRect);
        
        if (!containsEdgePolygon) 
        {
            if (Polygons[0].Length == 0) GD.Print("empty poly");
        
            return new List<Vector2[]>(){childRectPoly}; 
            
        }

        foreach (var polygon in Polygons)
            {
                var intersectResults = Geometry2D.IntersectPolygons(polygon, childRectPoly);
                childPolyList.AddRange(intersectResults);
            }
        // empty polygon added so Polygons array is safe to access
       
        return childPolyList; 

    }


    private bool HasEdgePoly()
    {
        if (Polygons.Count > 1) return true;
        var poly = gUtils.PolygonFromRect(BoundingRect);

        foreach (var point in Polygons[0]){
            if ( ! poly.Contains(point)) return true; 
        }
        return false;
    }

    private bool PolyPointIsOnGridEdge(Vector2 point)
    {  
        var r = BoundingRect;
        var pos = point - r.Position;
        if (pos.X == 0|| pos.Y == 0 || pos.X == r.Size.X || pos.Y == r.Size.Y) return true;
        return false; 
    }
    
    public bool HasChildren()
    {
        return GetChildren().Count > 0;
        
    }

    public List<PolygonQuad> GetChildren()
    {
        return Children.Values.Where(child => child != null).ToList();
    }

    // assumes point is NOT on edge of quad bounds, so returns only one quad (or none).
    // private PolygonQuad FindQuadWithPoint(Vector2 point)
    // {
        
    //     int queuePosition = 0;
    //     var queue = new List<PolygonQuad>{Root};
    //     while (queue.Count > queuePosition)
    //     {
            
    //         var quad = queue[queuePosition];
    //         queuePosition +=1;
    //         var floatRect = new Rect2(quad.BoundingRect.Position, quad.BoundingRect.Size);
    //         if (!floatRect.HasPoint(point)) continue;
            
    //         if (!quad.HasChildren()) return quad; 

    //         foreach (var child in quad.GetChildren()) if (child != null) queue.Add(child);
                
    //     }
    //     return null;
    // }  

    

    // public void InsertPointOnPolygons(Vector2 point)
    // {
        
    //     for (int i = 0; i < Polygons.Count; i++)
    //     {
    //         var polygon = Polygons[i];
    //         var length = polygon.Length;
    //         for (int j = 0; j < length; j++)
    //         {
    //             Vector2 p1 = polygon[j];
    //             int p2Index = j+1 < length ? j+1 : 0;
    //             Vector2 p2 = polygon[p2Index];
                
    //             if (PointSitsOnEdgeLine(p1, p2, point))
    //             {
    //                 List<Vector2> insertPoly = polygon.ToList();
    //                 insertPoly.Insert(p2Index, point);

    //                 Polygons[i] = insertPoly.ToArray();
    //                 break;
    //             }


    //         }
    //     }

    // }


    // private bool PointSitsOnEdgeLine(Vector2 polyPoint1, Vector2 polyPoint2, Vector2 newPoint)
    // {
    //     if (polyPoint1.IsEqualApprox(newPoint) || polyPoint2.IsEqualApprox(newPoint)) return false;
    //     int vecAxis = SharedVectorAxis(polyPoint1, polyPoint2);
    //     if (vecAxis == -1) 
    //     {
    //         return false; 
    //     }
    //     int otherAxis = vecAxis == 1? 0: 1;

    //     if (SharedVectorAxis(polyPoint1, newPoint) == vecAxis 
    //     && newPoint[otherAxis] < Math.Max(polyPoint1[otherAxis], polyPoint2[otherAxis])
    //     && newPoint[otherAxis] > Math.Min(polyPoint1[otherAxis], polyPoint2[otherAxis])) return true;
        
    //     return false;

    // }

    // private int SharedVectorAxis(Vector2 p1, Vector2 p2)
    // {
    //     if (Math.Abs(p1[0] - p2[0]) < 0.001 ) return 0;
    //     if (Math.Abs(p1[1] - p2[1]) < 0.001 ) return 1;
    //     return -1; 

    // }

  

}
