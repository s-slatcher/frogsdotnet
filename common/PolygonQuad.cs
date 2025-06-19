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

    private PolygonQuad(List<Vector2[]> polygons, Rect2 boundingRect, float minimumQuadWidth)
    {
        Polygons = polygons;
        BoundingRect = boundingRect;
        MinimumQuadWidth = minimumQuadWidth;

    }


  

    public List<Vector2[]> Polygons;
    public List<Vector3[]> Vertices;
    public ExtrudedMesh QuadManager;
    public PolygonQuad Parent;
    public PolygonQuad Root;
    public Rect2 BoundingRect;
    public readonly float MinimumQuadWidth;
    private GeometryUtils gUtils = new();
    private bool containsEdgePolygon = true; 

    public List<PolygonQuad> Children = new();
    

    public static PolygonQuad CreateRootQuad(Vector2[] polygon, float minQuadWidth = 20)
    {
        
        var polyRect = GetRootQuadRect(polygon, minQuadWidth);

        var quad = new PolygonQuad([polygon], polyRect, minQuadWidth);
    
        quad.Root = quad;
        return quad;

    }

    private static Rect2 GetRootQuadRect(Vector2[] rootPolygon, float minQuadWidth)
    {
        var gUtils = new GeometryUtils();
        var polyRect = GeometryUtils.RectFromPolygon(rootPolygon);
        // polyRect = polyRect.Grow(1);
        polyRect = new Rect2
        (
            new((int)polyRect.Position.X, (int)polyRect.Position.Y), 
            new((int)(polyRect.Size.X+1), (int)(polyRect.Size.X+1))
        );
        
        float targetQuadWidth = Math.Max(polyRect.Size.X, polyRect.Size.Y);
        int currentMultiplier = 2;
        while (currentMultiplier * minQuadWidth <= targetQuadWidth) currentMultiplier *= 2;
        float normalizedRectWidth = currentMultiplier * minQuadWidth;    
        polyRect.Size = new Vector2(normalizedRectWidth, normalizedRectWidth);
        return polyRect;

    }

    public PolygonQuad Duplicate()
    {
        
        
        var quad = new PolygonQuad(this.Polygons, this.BoundingRect, this.MinimumQuadWidth);
        quad.Children = new(Children);
        return quad;
    }

    public void Subdivide()
    {
        var offsetVector = BoundingRect.Size / 2;
        var baseVector = BoundingRect.Position;
        
        if (containsEdgePolygon) containsEdgePolygon = HasEdgePoly();
        if (offsetVector.X < MinimumQuadWidth ) return;

        
        
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

            PolygonQuad childQuad = new(childPolyList, childRect, MinimumQuadWidth)
            {
                containsEdgePolygon = containsEdgePolygon,
                QuadManager = QuadManager,
                Parent = this,
                Root = Root
            };
            
            Children.Add(childQuad);
        };
        
    }

    

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

    public float GetWidth()
    {
        return BoundingRect.Size.X;
    }

    public List<PolygonQuad> GetChildren()
    {
        return Children;
    }



  

}
