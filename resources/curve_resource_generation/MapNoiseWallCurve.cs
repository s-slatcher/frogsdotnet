using Godot;
using System;
using System.Collections.Generic;

public partial class MapNoiseWallCurve : Node2D
{
    [Export] bool GenerateNewCurveResource = false;
    [Export] NoiseTexture2D NoiseTex;

    


    public override void _Ready()
    {
        var path2d = GetNode<Path2D>("Path2D");
        if (GenerateNewCurveResource)
        {
            var noise = NoiseTex.Noise;
            var pointCount = 60;
            var pointDistance = 50;
            var curveHeightMuiltiplier = 50;
            
            path2d.Curve = new Curve2D();
            List<float> heights = [];
            for (int i = 0; i < pointCount; i++)
            {
                var height = noise.GetNoise1D(i * pointDistance);
                var pathPoint = new Vector2(i * pointDistance, height * curveHeightMuiltiplier);    
                path2d.Curve.AddPoint(pathPoint);
            }

            
            DirAccess.MakeDirAbsolute("user://resource_save");
            Error error = ResourceSaver.Save(path2d.Curve, "user://resource_save/" + "curve.tres");
            GD.Print(error);
        }

        debugPreviewCurve(path2d.Curve, 0);
        var scaledCurve = ScaleCurveIntensity(path2d.Curve, 0.5f);
        debugPreviewCurve(scaledCurve, 100);
        
        var slicedCurve = SliceCurve(path2d.Curve, 10, 30);
        debugPreviewCurve(slicedCurve, 150);

    }

    public void debugPreviewCurve(Curve2D curve, float heightOffset)
    {

        var polyLine = new Line2D(){Points = curve.Tessellate()};
        polyLine.Width = 2;
        polyLine.Position = new Vector2(0, heightOffset);
        AddChild(polyLine);

    }

    public Curve2D ScaleCurveIntensity(Curve2D curve, float intensity)
    {
        var newCurve = new Curve2D();

        for (int i = 0; i < curve.PointCount; i++)
        {
            var pointPos = curve.GetPointPosition(i);
            var pointIn = curve.GetPointIn(i);
            var pointOut = curve.GetPointOut(i);
            
            newCurve.AddPoint(pointPos * new Vector2(1, intensity)); //scales down just the y value
            newCurve.SetPointIn(i, pointIn * intensity);
            newCurve.SetPointOut(i, pointOut * intensity);


        }

        return newCurve;
    }
    // from inclusive, to exclusive
    public Curve2D SliceCurve(Curve2D curve, int from, int to)
    {
        var slicedCurve = curve.Duplicate() as Curve2D;
        var translate = slicedCurve.GetPointPosition(0) - slicedCurve.GetPointPosition(from);
        for (int i = curve.PointCount-1; i > -1; i--)
        {
            if (i > to || i < from) slicedCurve.RemovePoint(i);
            else slicedCurve.SetPointPosition(i, slicedCurve.GetPointPosition(i) + translate );
                
        }
        return slicedCurve;
        
    }



}
