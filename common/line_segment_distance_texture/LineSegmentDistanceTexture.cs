using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Xml.Linq;
using Vector2 = Godot.Vector2;

public partial class LineSegmentDistanceTexture : GodotObject
{
    NormalPoly Poly;
    
    List<LineSegment> lines;
    
    Rect2I normalRect;

    public int PixelPerUnit = 4;
    public float PerpDist = 5;
    public float ParallelDist = 0.1f;
    public float EdgeBuffer = 1;

    public int totalPixelsDrawn = 0;

    ImageTexture cachedImage;

    public LineSegmentDistanceTexture(List<LineSegment> lineSegments, Rect2I boundingRect)
    {
        if (boundingRect.Position != Vector2I.Zero) GD.Print("bounding rect not normalized for LineSegmentDistanceTexture. this will break the sampled data!");
        lines = lineSegments;
        normalRect = boundingRect;

    }

    public LineSegmentDistanceTexture(NormalPoly poly)
    {
        normalRect = poly.Rect;
        var gd = new GeometryUtils();
        lines = gd.LineSegmentsFromPolygon(poly.Polygon);
    }

    public ImageTexture GetTexture()
    {
        if (cachedImage != null) return cachedImage;

        GenerateImageTexture();
        return cachedImage;

    }

    private void GenerateImageTexture()
    {
        var time = Time.GetTicksMsec();    
        var img = GetImage();

        for (int i = 0; i < lines.Count; i++)
        {
            DrawLine(lines[i], img);
        }

        cachedImage = ImageTexture.CreateFromImage(img);

        GD.Print(totalPixelsDrawn);

        GD.Print("total time to texture: ", Time.GetTicksMsec() - time);



    }
    private void DrawLine(LineSegment line, Image img)
    {
        
        var parLine = line.GetExpandedLine(ParallelDist, ParallelDist);


        var perpLine = new LineSegment(
            parLine.Start,
            parLine.Start - (line.GetNormal() * PerpDist)
        );
        
        
        var parLinePixels = parLine.Length() * PixelPerUnit * 1.5f; // to safely hit enough pixel spots, 45 deg angles may need double the pixel count
        var perpLinePixels = perpLine.Length() * PixelPerUnit * 1.5f;

        var parDir = parLine.DirectionVector().Normalized();
        var perpDir = perpLine.DirectionVector().Normalized();

        
        var distPerPixel = parLine.Length() / parLinePixels;
        for (int i = 0; i < parLinePixels; i++)
        {
            var progressPar = parDir * (i * distPerPixel);
            for (int j = 0; j < perpLinePixels; j++)
            {
                var progressPerp = perpDir * (j * distPerPixel);
                var pos = progressPerp + progressPar + parLine.Start;

                var closePos = Geometry2D.GetClosestPointToSegment(pos, line.Start, line.End);
                var pVec = pos - closePos;
                
                var parComponent = pVec.Project(parDir);
                var relativeParVec = parComponent / ParallelDist;
                var relativePerpVec = (pVec - parComponent) / PerpDist;
                var normalizedDist = relativeParVec.Length() + relativePerpVec.Length();

                
                var color = new Godot.Color(1 - normalizedDist, 0,0,1);


                PaintToImage(pos, color, img);

            }
            
        }


    // private void DrawLineToImage(LineSegment line, Image img)
    // {
    //     var lineVec = line.DirectionVector();
    //     var lineNorm = line.GetNormal();
        
    //     // get angle of vec 
    //     var ang = lineVec.AngleTo(Vector2.Right);
        
        
    //     var flatLine = new Vector2( lineVec.Length() , 0);
    //     var flatNormDir = Math.Sign(lineNorm.Rotated(ang).Y);  // depending on angle of line, the rotated normal of the flat line can point up or down; 

        
        
        
    //     // STEPS OF FOR-LOOP
    //     // start with normalized (rotated and translated) meter positions 
    //     // multiply by pixel per unit so for loop hits fractional spots between meter positions
    //     // convert back to meter, do distance calc to paint pixel
    //     // de-normalize position (translate and rotate back)
    //     // convert to pixel position
    //     // draw color to image 
        
    //     var startPoint = new Vector2(-ParallelDist, EdgeBuffer * flatNormDir);
    //     var endPoint = new Vector2(flatLine.X + ParallelDist, PerpDist * (-flatNormDir));
    //     var lineRect = new Rect2(){Position = startPoint, End = endPoint}.Abs();
    //     startPoint = lineRect.Position;
    //     endPoint = lineRect.Position;
        
    //     var distRatioVec = new Vector2(ParallelDist, PerpDist);

    //     for (int x = (int)startPoint.X * PixelPerUnit; x > endPoint.X * PixelPerUnit; x++)
    //     {
    //         for (int y = (int)startPoint.Y * PixelPerUnit; y > endPoint.Y * PixelPerUnit; y++)
    //         {
    //             var p = new Vector2(x,y) / PixelPerUnit;
    //             var closeP = Geometry2D.GetClosestPointToSegment(p, Vector2.Zero, flatLine);
    //             var pVec = p - closeP;
    //             var normalizedDist = float.Clamp( 1 - (pVec/distRatioVec).Length() , 0, 1);  // points right line clamped to 1, distances beyond max clamped to 0;
                
    //             var color = new Godot.Color(1, 0, 0, 1);
    //             var deNormalizedPos = p.Rotated(-ang) + line.Start;

    //             // if (!normalRect.HasPoint(new Vector2I((int)deNormalizedPos.X, (int)deNormalizedPos.Y))) continue;
    //             var pixelPosition = PixelPos(deNormalizedPos);
                
    //             var curImgColor = img.GetPixelv(pixelPosition);
    //             if (color.R > curImgColor.R) img.SetPixelv(pixelPosition, color);
    //         }
    //     }

    // }

    

        



    }



    private Vector2I PixelPos(Vector2 point)
    {
        return new Vector2I((int)(point.X * PixelPerUnit), (int)(point.Y * PixelPerUnit) );
    }

    private void PaintToImage(Vector2 pos, Godot.Color color, Image img)
    {
        var posI = new Vector2I((int)Math.Round(pos.X), (int)Math.Round(pos.Y));
        var pixelPos = PixelPos(pos);

        if (!normalRect.HasPoint(posI)) return;

        if (img.GetPixelv(pixelPos).R > color.R) return;

        totalPixelsDrawn ++;
        img.SetPixelv(pixelPos, color);
        
    }


    // red and green float channel -- 
    // store distances as "UV traveled from edge to reach point", convert in shader by multiplying by bounding rect
    // use blue channel for is inside/ is outside? 
    // start image as white (1,1 means max travel distance to reach obj)


    Image GetImage()
    {
        Rect2I imageRect = new Rect2I(){Size = normalRect.Size * PixelPerUnit};
        GD.Print("image rect size: ", imageRect.Size);  
        var img = Image.CreateEmpty(imageRect.Size.X, imageRect.Size.Y, false, Image.Format.Rgh); 
        img.Fill(Godot.Colors.Black);
        return img;

    }

}
