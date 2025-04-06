using Godot;
using System;

public partial class EdgeTextureGenerator : GodotObject
{

    public Vector2[] Polygon; 
    public int PixelPerUnit = 8;
    private GeometryUtils gUtils =  new();
    

    public void Generate()
    {
        
        var lineSegments = gUtils.LineSegmentsFromPolygon(Polygon);
        Rect2I rectI = gUtils.RectIFromPolygon(Polygon);
        var img = Image.CreateEmpty(rectI.Size.X * PixelPerUnit, rectI.Size.Y * PixelPerUnit, false, Image.Format.Rh);

        // loop each line segment -- draw a box around it.
        // loop the coords of that box, filling in pixels on the main image based on proximity to the edge
        // overwrite pixels if a point is closer to an edge than the previous color indicates
        // finish image and assign to ImageTexture





    }


}
