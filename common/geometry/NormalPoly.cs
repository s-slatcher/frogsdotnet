using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;


public partial class NormalPoly : RefCounted
{
    private Vector2[] SourcePoints;
    public Vector2[] Polygon = [];
    public Rect2I Rect;
    public Vector2 Position;

    public NormalPoly(Vector2[] pointSet, int margin = 1)
    {
        SourcePoints = pointSet;

        var gu = new GeometryUtils();
        
        Rect2 initRect = gu.RectFromPolygon(pointSet);
        var pos = initRect.Position;
        Position = pos;
        
        
        // translate poly to first normalize to 0,0 position, then add small margin to corner
        Vector2 cornerMargin = new Vector2I(margin,margin);
        var translatedPoly = gu.TranslatePolygon(pointSet, -pos + cornerMargin);
        
        // compensate for margin added to polygon corner, and extra margin on top and right sides
        // add an additional 1 meter to compensate for flooring value 
        var normalRect = new Rect2I();
        var marginSize = initRect.Size + (cornerMargin * 2); 
        normalRect.Size = new Vector2I((int)(marginSize.X+1), (int)(marginSize.Y+1));

        Polygon = translatedPoly;
        Rect = normalRect;

    }

    public IEnumerable<LinkedPoint> GetIterator()
    {
        int len = Polygon.Length;
        for (int i = 0; i < len; i++)
        {
            yield return (
                new LinkedPoint()
                {
                    current = Polygon[i],
                    prev = i == 0 ? Polygon[len - 1] : Polygon[i - 1],
                    next = i == len - 1 ? Polygon[0] : Polygon[i + 1]
                }
            );

        }

    }

    public Vector2 GetPointAsUV(int index)
    {
        return Polygon[index] / Rect.Size;
    }

}

public struct LinkedPoint
{
    public Vector2 current;
    public Vector2 prev;
    public Vector2 next;
}
