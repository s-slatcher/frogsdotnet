using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PolygonCollisionTree: GodotObject
{
    // static PolygonCollisionTree CreateTreeRoot(Polygon polygon)
    // {
    //     var root = new PolygonCollisionTree(polygon.BoundingRect){};
    //     return root;
    // }
    public readonly Rect2 BoundingRect;
    public float MinimumQuadWidth = 6;
    public PolygonCollisionTree Parent;
    public List<PolygonCollisionTree> Children = new();

    public List<(PolygonPoint, PolygonPoint)> PolygonLines = new(); 

    public PolygonCollisionTree(Rect2 boundingRect, List<(PolygonPoint, PolygonPoint)> polygonLines, bool refineEdges = true)
    {
        BoundingRect = boundingRect;
        PolygonLines = polygonLines;
        if (refineEdges) RefineEdgeList(); // disable only for root quad assuming bounding box provided includes all points
    }
    public void Subdivide()
    {
        if (Children.Count > 0) return;

        var sizeVector = BoundingRect.Size / 2;
        var baseVector = BoundingRect.Position;

        if (sizeVector.X < MinimumQuadWidth) return;


        List<Vector2> childOffsets = new(){
            new(0,0),
            new(sizeVector.X, 0),
            sizeVector,
            new(0, sizeVector.Y)
        };


        for (int i = 0; i < childOffsets.Count; i++)
        {
            var childRect = new Rect2(childOffsets[i] + baseVector, sizeVector);
            PolygonCollisionTree child = new(childRect, PolygonLines);
            Children.Add(child);
        }

    
    }
    
    public void RefineEdgeList()
    {
        var gu = new GeometryUtils();
        var refinedList = PolygonLines.Where(t => gu.IsPolygonLineIntersectingRect(BoundingRect, t.Item1, t.Item2));
        PolygonLines = refinedList.ToList();
    }
    

}
