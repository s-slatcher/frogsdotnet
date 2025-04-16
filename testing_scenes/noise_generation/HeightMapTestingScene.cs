using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class HeightMapTestingScene : Node2D
{
    int seed = 0;
    float MaxHeight = 80;
    int Width = 300; 
    GeometryUtils gu = new();

    public override void _Ready()
    {   
        seed = (int)GD.Randi();
        var heightMap = new HeightMap(Width, seed, 0.025f, 3, 2, 0.5f);
        heightMap.MaxHeight = MaxHeight;

        var points = heightMap.GetHeights();
        var pointsOfInterest = heightMap.GetPointsOfInterest();
        
        var polyline = new Line2D();
        for (int i = 0; i < points.Count; i++)
        {
            polyline.AddPoint(points[i]);
        }

        var polylinePOIs = new Line2D();
        var smoothCurveLine = new Line2D();
        var curve = new GeometryUtils().PointsToCurve(pointsOfInterest.ToArray(), 0.5f, false);
        
        smoothCurveLine.Points = curve.Tessellate();

        for (int i = 0; i < pointsOfInterest.Count; i++)
        {
            
            var pointPos = pointsOfInterest[i];
    
            polylinePOIs.AddPoint( new Vector2(pointPos.X, pointPos.Y)); 
            polylinePOIs.AddPoint( new Vector2(pointPos.X, pointPos.Y - 8)); 
            polylinePOIs.AddPoint( new Vector2(pointPos.X, pointPos.Y)); 
           
        }

        


        polylinePOIs.Width = 1f;
        polylinePOIs.Position = new Vector2(0, 100);
        polylinePOIs.Modulate = Colors.Orange;
        
        polyline.Width = 1f;
        smoothCurveLine.Width = 1f;
        smoothCurveLine.Position = new Vector2(0, 50);
        smoothCurveLine.Modulate = Colors.Aquamarine;
        
        AddChild(polyline);
        AddChild(polylinePOIs);
        AddChild(smoothCurveLine);

        GenerateHeightMapPolygons_v2(polylinePOIs.Points.ToList());
        // GenerateHeightMapPolygons(polylinePOIs.Points);
    }

    void GenerateHeightMapPolygons_v2(List<Vector2> points)
    {
        

        var groups = new List<List<Vector2>>();
        var currentGroup = new List<Vector2>();
        var groupMin = float.MaxValue;
        var groupMax = float.MinValue;
        // step 2; group points together with a running average of slope
        float tolerance = 20; 
        for (int i = 1; i < points.Count; i++)
        {
            var pointHeight = points[i].Y;
            groupMin = Math.Min(pointHeight, groupMin);
            groupMax = Math.Max(pointHeight, groupMax);
            
            currentGroup.Add(points[i]);

            if (groupMax - groupMin > tolerance)
            {
                groups.Add(currentGroup);
                currentGroup = new(){points[i]};
                groupMin = groupMax = points[i].Y;
            }
        }

        for (int i = 0; i < groups.Count; i++)
        {
            // take only the first and last points of the group as relevant (may be same point)
            var p1 = groups[i][0];
            var p2 = groups[i][^1];


            var avgY = 0f;
            foreach (var point in groups[i]) avgY += point.Y;
            avgY /= groups[i].Count;
    
            p1.Y = p2.Y = avgY;
            
            var groupGapOffset = new Vector2(i * 7, 0); // add only for debugging purposes
            

            var polygon = new Vector2[]
            {
                p1 + groupGapOffset ,
                p2 + groupGapOffset ,
                new Vector2(p2.X, 0) + groupGapOffset ,
                new Vector2(p1.X, 0) + groupGapOffset ,
            };
            UnitFromRect(gu.RectFromPolygon(polygon));

            // add group polygon to scene

            var poly2d = new Polygon2D(){Polygon = polygon};
            poly2d.Position = new Vector2(0, -90);
            poly2d.Modulate = new Godot.Color(GD.Randf(), GD.Randf(), GD.Randf(), 1);
            AddChild(poly2d);

        }


    }
    
    void UnitFromRect(Rect2 rect)
    {
        // generate two different curve stretches

        var hm = new HeightMap(rect.Size.Y, (int)GD.Randi(), 0.06f, 5, 2, 0.5f);
        hm.MaxHeight = float.Clamp(5, 0, rect.Size.X/2);
        hm.MinHeight = -hm.MaxHeight;
        
        var leftSide = hm.GetPointsOfInterest();
        hm.domainOffset = rect.Size.Y;
        var rightSide = hm.GetPointsOfInterest();

        //rotate to vertical
        leftSide = gu.RotatePolygon(leftSide.ToArray(), (float)Math.PI/2).ToList();
        rightSide = gu.RotatePolygon(rightSide.ToArray(), (float)Math.PI/2).ToList();

        // add ending points
        foreach (var side in new[]{leftSide, rightSide})
        {
            var edgeVector = new Vector2(0, 1);
            var endPoint = side[^1];
            // angle transition split into two points to help curve smoother
            var angleFlip = side == rightSide? -1 : 1;
            var edgeAngle = -Math.PI/2.1;  // smaller angles than 90 produce better results in curve smoothing
            var edgePoint1 = endPoint + edgeVector.Rotated((float)edgeAngle/2 * angleFlip);
            var edgePoint2 = edgePoint1 + edgeVector.Rotated((float)edgeAngle * angleFlip);
            side.AddRange([edgePoint1, edgePoint2]);
        }
       
        // translate lines, reverse right side and combine as poly
        var translatedRightSide = gu.TranslatePolygon(rightSide.ToArray(), new Vector2(rect.Size.X, 0));
        var combinedUnitPoly = leftSide.Concat(translatedRightSide.Reverse().ToArray()).ToArray();


        // add to curve and smooth, then tesselate back into polygon
        var smoothCurve = gu.PointsToCurve(combinedUnitPoly, 0.5f, false);
        var tesselatePoly = smoothCurve.Tessellate();
       
        var translatedPoly = gu.TranslatePolygon(tesselatePoly, rect.Position);

        // var polyLine = new Line2D(){Points = tesselatePoly};
        // polyLine.Position = new Vector2(0, -180);
        // polyLine.Width = 0.5f;
        
        // AddChild(polyLine);


        
        // GD.Print("total points after tes: " + tesselatedPoly.Length);

        var polyInst = new Polygon2D(){Polygon = translatedPoly};
        polyInst.Position = new Vector2(0, -180);
        AddChild(polyInst);





    }


}
