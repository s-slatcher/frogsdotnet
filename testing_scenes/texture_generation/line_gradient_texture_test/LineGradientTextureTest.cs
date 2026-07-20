using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

public partial class LineGradientTextureTest : Node2D
{

    [Export] Line2D line; 
    [Export] SubViewport subViewport;
    [Export] ColorRect colorRect;


    Vector2 circlePosition = new Vector2(500,500);

    float circleRad = 300;

    NormalPoly poly;
    Rect2I polyRect;

    public override void _Ready()
    {

       
        var points = GeneratePointSet();
        // var points = new List<Vector2>()
        // {
        //   new Vector2(),
        //   new Vector2(1000,0),
        //   new Vector2(1000,1000),
        //   new Vector2(0,1000),
        // };
        
        poly = new NormalPoly(points.ToArray(), (int)(line.Width/2));
        polyRect = poly.Rect;
        line.Points = poly.Polygon;

        SetShaderParameters();
        SetLineGradient();

    }




    private void SetLineGradient()
    {   
        var time = Time.GetTicksMsec();

        var gradientTex = new GradientTexture1D();
        var grad = new Gradient();
        grad.InterpolationColorSpace = Gradient.ColorSpace.LinearSrgb; 
        
        var offsets = GenerateOffsetsList(poly);
        var colors = new List<Godot.Color>();
        for (int i = 0; i < offsets.Count; i++)
        {
            colors.Add(ConvertPointToColor(poly, i));
        }

        // repeat first color as the final gradient offset (at offset == 1)
        offsets.Add(1);
        colors.Add(ConvertPointToColor(poly, 0)); 
        
        // apply to gradient
        grad.Colors = colors.ToArray();
        grad.Offsets = offsets.ToArray();
        

        gradientTex.UseHdr = true;
        gradientTex.Gradient = grad;

        line.Texture = gradientTex;
        
    }


    public Godot.Color ConvertPointToColor(NormalPoly poly, int index)
    {
        var uv = poly.GetPointAsUV(index);
        return new Godot.Color(uv.X, uv.Y, 1,1);
    }

    
    public void SetShaderParameters()
    {
        // uniform sampler2D line_texture: filter_nearest;
        // uniform vec2 rect_size;
        // uniform float line_width;

        
        subViewport.Size = polyRect.Size;
        colorRect.Size = polyRect.Size; 

        var mat = (ShaderMaterial)colorRect.Material;
        mat.SetShaderParameter("line_texture", subViewport.GetTexture());
        mat.SetShaderParameter("rect_size", (Vector2)polyRect.Size);
        mat.SetShaderParameter("line_width", line.Width);




    }



    public List<Vector2> GeneratePointSet()
    {

        var gu = new GeometryUtils();
        var pointSet = gu.PolygonFromCircle(circlePosition, circleRad , 0.5f);
        

        return pointSet;

    }

    public List<float> GenerateOffsetsList(NormalPoly poly)
    {
        var lengths = new List<float>();

        var offsets = new List<float>();        

        var lengthTotal = 0f;
        var polyIterator = poly.GetIterator();

        lengths = polyIterator.Select(p => (p.current - p.next).Length()).ToList();
        lengthTotal = lengths.Aggregate(0f, (sum, num) => sum+num );

        var curLen = 0f;
        foreach (var length in lengths)
        {
            offsets.Add(curLen / lengthTotal);
            curLen += length;
        }

        return offsets;
    }


}
