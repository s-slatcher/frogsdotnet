using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Vector2 = Godot.Vector2;

public partial class MapNoiseWallCurve : Node2D
{
    [Export] bool GenerateNewCurveResource = false;
    [Export] NoiseTexture2D NoiseTex;
    GeometryUtils gu = new();
    
    float pointDistance = 10;
    int pointCount = 300;
    float curveHeightMultiplier = 60;
    


    public override void _Ready()
    {

        var noisePoints = new List<Vector2>();

        var path2d = GetNode<Path2D>("Path2D");
        if (GenerateNewCurveResource)
        {
            var noise = NoiseTex.Noise as FastNoiseLite;
             noise.Seed = (int)GD.Randi();
            //noise.Seed = 10;
            
            
            var sampleDistance = 2500;
            var sampleGap = 5;


            List<float> heights = [];
            for (int i = 0; i < sampleDistance; i += sampleGap)
            {
                var height = noise.GetNoise1D(i)* curveHeightMultiplier;
                var lastHeight = noise.GetNoise1D(i-sampleGap) * curveHeightMultiplier;
                var nextHeight = noise.GetNoise1D(i+sampleGap) * curveHeightMultiplier;

                var vecLast = new Vector2(i, height) - new Vector2(i-sampleGap, lastHeight);
                var vecNext = new Vector2(i+sampleGap, nextHeight) - new Vector2(i, height);
                var angle = vecLast.AngleTo(vecNext);
                
                if (Math.Abs(angle) > 0.2) noisePoints.Add(new Vector2(i, height )); 

            }

            
            DirAccess.MakeDirAbsolute("user://resource_save");
            Error error = ResourceSaver.Save(path2d.Curve, "user://resource_save/" + "curve.tres");
            GD.Print(error);
        }

        // debugPreviewCurve(path2d.Curve, 0);
        var smoothCurve = gu.PointsToCurve(noisePoints.ToArray(), 0.25f, false);
        debugPreviewCurve(smoothCurve, 100);
        GenerateCurveSlicePoly(smoothCurve);

        // debugPreviewCurve(path2d.Curve, 0);
        // var scaledCurve = gu.ScaleCurve(path2d.Curve, new Vector2(1, 0.5f));
        // debugPreviewCurve(scaledCurve, 100);
        
        // var slicedCurve = gu.SliceCurve(path2d.Curve, 10, 30, true );
        // var rotatedCurve = gu.RotateCurve(slicedCurve, 0.5f);
        // debugPreviewCurve(rotatedCurve, 150);

    }

    

    private void GenerateCurveSlicePoly(Curve2D curve)
    {
        var height = 500;
        var widthTop = 150;
        var widthBase = 500;

        var curveStartLeft = new Vector2(0 - widthBase/2 , 0);
        var curveEndLeft = new Vector2(0 - widthTop/2, height);
        var curveStartRight = curveStartLeft + new Vector2(widthBase, 0);
        var curveEndRight = curveEndLeft + new Vector2(widthTop, 0);

        var curveLeftVector = curveEndLeft - curveStartLeft;
        var curveRightVector = curveEndRight - curveStartRight;
        var curveDistance = curveLeftVector.Length();
        

        // will need a general way to get a "length" of curve in terms of X distance
        
        var curveMaxWidth = curve.GetPointPosition(curve.PointCount - 1).X;
        
        var indexOffset =  GD.RandRange(0, 50);
        var endIndex = FindCurvePointClosestToWidth(curve, curve.GetPointPosition(indexOffset).X + curveDistance); 
        GD.Print(curveDistance);
        GD.Print(curve.GetPointPosition(endIndex).X - curve.GetPointPosition(indexOffset).X);
        var curveSlice = gu.SliceCurve(curve, indexOffset, endIndex, true);
        var uprightCurve = gu.RotateCurve(curveSlice, (float)Math.PI/2);
        // var curveSliceRight = gu.SliceCurve(curve, indexOffset + index, indexOffset + index + index, true);
        
        var rotatedCurve = gu.RotateCurve(uprightCurve, (float)Math.Atan2(curveLeftVector.Y, curveLeftVector.X) - (float)Math.PI/2 );
        var translatedCurve = gu.TranslateCurve(rotatedCurve, curveStartLeft);

        var curveTopPos = translatedCurve.GetPointPosition(translatedCurve.PointCount-1) + new Vector2(10, 10);
        translatedCurve.AddPoint(curveTopPos);
        translatedCurve.SetPointOut(translatedCurve.PointCount-1, new Vector2(10, 0));
        translatedCurve.SetPointIn(translatedCurve.PointCount-1, new Vector2(-10, 0));

        var curveBottomPos = translatedCurve.GetPointPosition(0) + new Vector2(10, -10);
        translatedCurve.AddPoint(curveBottomPos, null, null, 0);
        translatedCurve.SetPointOut(0, new Vector2(-10, 0));
        translatedCurve.SetPointIn(0, new Vector2(10, 0));


        
        var rotatedCurveRight = gu.RotateCurve(uprightCurve, (float)Math.Atan2(curveRightVector.Y, curveRightVector.X) - (float)Math.PI/2);
        var translatedCurveRight = gu.TranslateCurve(rotatedCurveRight, curveStartRight);

        var polygon = new List<Vector2>();
        polygon.AddRange(translatedCurve.Tessellate());
        polygon.AddRange(translatedCurveRight.Tessellate().Reverse());

        
        // polygon.Add(curveEndRight);
        // polygon.Add(curveStartRight);
        
        // var Line2D = new Line2D(){Points = translatedCurve.Tessellate()};
        // AddChild(Line2D);

        GetNode<Polygon2D>("CurvePolyContainer").Polygon = polygon.ToArray();

    }

    public int FindCurvePointClosestToWidth(Curve2D curve, float width)
    {
        var low = 0;
        var high = curve.PointCount -1;
        
        while (true)
        {
            var mid = (int) ((high + low) / 2);
            if ((high - low) < 2) {
                var highPosDelta = curve.GetPointPosition(high).X - width;
                var lowPosDelta = curve.GetPointPosition(low).X - width;
                if (Math.Abs(highPosDelta) < Math.Abs(lowPosDelta)) return high;
                else return low;
            }   
            if (curve.GetPointPosition(mid).X > width)
            {
                high = mid;
            }
            else
            {
                low = mid;
            }

        }

    }

    public void debugPreviewCurve(Curve2D curve, float heightOffset)
    {

        var polyLine = new Line2D(){Points = curve.Tessellate()};
        polyLine.Width = 1;
        polyLine.Position = new Vector2(0, heightOffset);
        AddChild(polyLine);

    }


   



}
