using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EdgeTextureGenerator : GodotObject
{

    public Vector2[] Polygon;
    public int PixelPerUnit = 8;
    public float edgeDistanceLimit = 1; // measured in units based on provided polygon, not image pixels
    public float edgeBuffer = 1; // expand lines by x polygon units so grass doesn't end abruptly at edges 
    private GeometryUtils gUtils =  new();
    

    public Image Generate()
    {

        

        var lineSegments = gUtils.LineSegmentsFromPolygon(Polygon);
        
        Rect2I rectI = gUtils.RectIFromPolygon(Polygon);
        Rect2I imageRect = new(rectI.Position * PixelPerUnit, rectI.End * PixelPerUnit);


        var img = Image.CreateEmpty(imageRect.Size.X, imageRect.Size.Y, false, Image.Format.Rgb8);
        img.Fill(Colors.Black);

        List<LineSegment> horizontalEdges = lineSegments.Where(lineSeg => lineSeg.GetNormal().Y > 0.9).ToList();
        
        var pixelRectMap = new Dictionary<LineSegment, Rect2I>();
        
        foreach (var edge in horizontalEdges)
        {
            // convert edge to pixel units
            var pixelEdge = new LineSegment(edge.Start * PixelPerUnit, edge.End * PixelPerUnit);
            var expandedEdge = new LineSegment(pixelEdge.Start, pixelEdge.End);
            expandedEdge.ExpandLine(edgeBuffer * PixelPerUnit, edgeBuffer * PixelPerUnit);
            var edgeLimitVector = pixelEdge.GetNormal() * -1 * edgeDistanceLimit * PixelPerUnit;
            
            // create a rect that encloses the expanded, pixel-unit line and (at least) all the points within the edge distance limit
            Rect2I pixelEdgeRect = gUtils.RectIFromPolygon([expandedEdge.Start, expandedEdge.End, expandedEdge.Start + edgeLimitVector, expandedEdge.End + edgeLimitVector]);
            pixelRectMap[pixelEdge] = pixelEdgeRect; 
            
        }

        // loop over each new rect and color in the larger texture based on those pixels distance to the contained edge segment
        foreach (var edge in pixelRectMap.Keys)
        {
            Rect2I rect = pixelRectMap[edge];
            var pixelLimit = edgeDistanceLimit * PixelPerUnit; 
            // translation applied to points relative to this rect's origin, to make them relative to the larger image rect's origin
            var pixelTranslation = rect.Position - imageRect.Position;
            for (int x = rect.Position.X; x < rect.Size.X + rect.Position.X; x++)
            {
                for (int y = rect.Position.Y; y < rect.Size.Y + rect.Position.Y; y++ )
                {
                    var point = new Vector2I(x , y);
                    
                    if (!imageRect.HasPoint(point + imageRect.Position)) continue;

                    var closePoint = Geometry2D.GetClosestPointToSegment( point, edge.Start, edge.End);
                    var dist = (point - closePoint).Length(); 
                    
                    if (dist > pixelLimit) continue;

                    var angleFactor = edge.GetNormal().Dot((closePoint - point).Normalized());
                    // angleFactor = (angleFactor + 1) / 2;  // map to 0 - 1;
                    if (angleFactor < 0) continue;

                    // angleFactor *= angleFactor;
                    var distFactor = 1f - (dist / pixelLimit);
                    var redVal = distFactor * angleFactor;
                    var pixelColor = img.GetPixelv(point);
                    if (redVal > pixelColor.R) img.SetPixelv(point, new Color(redVal, 0, 0, 1)); // set the red channel in texture to match distance factor

                }
            }

        }

        return img;

    }


}
