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
        // seed = (int)GD.Randi();
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
        //step 1; turn points to sets of points forming plateaus
        // var flatPointSet = new List<Vector2>();
        // for (int i = 1; i < points.Count - 1; i++)   // range excludes first and last points for simplicity
        // {
        //     var p1 = points[i];
        //     var pLast = points[i-1];
        //     var pNext = points[i+1];

        //     var flatPoints = new List<Vector2>(){ 
        //         p1 + new Vector2((pLast.X - p1.X)/2, 0),
        //         p1 + new Vector2((pNext.X - p1.X)/2, 0),

        //     };
        //     flatPointSet.AddRange(flatPoints);
        // }

        var groups = new List<List<Vector2>>();
        var currentGroup = new List<Vector2>();
        var groupMin = float.MaxValue;
        var groupMax = float.MinValue;
        // step 2; group points together with a running average of slope
        float tolerance = 16; 
        for (int i = 1; i < points.Count; i++)
        {
            var pointHeight = points[i].Y;
            var newMin = Math.Min(pointHeight, groupMin);
            var newMax = Math.Max(pointHeight, groupMax);
            
            
            groupMin = newMin;
            groupMax = newMax;
            currentGroup.Add(points[i]);

            if (newMax - newMin > tolerance)
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
            
            var groupGapOffset = new Vector2(i * 5, 0); // add only for debugging purposes
            
            // extend points out to meet halfway to their neighboring group edges
            // if (i > 0)
            // {
            //     var VecToLeftNeighbor = groups[i-1][^1] - p1;
            //     p1 += new Vector2(VecToLeftNeighbor.X / 2, 0);
            // }
            // if (i < groups.Count - 1)
            // {
            //     var VecToRightNeighbor = groups[i+1][0] - p2;
            //     p2 += new Vector2(VecToRightNeighbor.X / 2, 0);
            // }
            
            
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
        
        var hm = new HeightMap(rect.Size.Y * 2, (int)GD.Randi(), 0.06f, 5, 2, 0.5f);
        hm.MaxHeight = float.Clamp(5, 0, rect.Size.X/2);
        hm.MinHeight = -hm.MaxHeight;
        var noisePoints = hm.GetPointsOfInterest();
        
        if (!int.IsEvenInteger(noisePoints.Count)) noisePoints.RemoveAt(noisePoints.Count-1);
        var midPointIndex = 0;
        var lowDelta = float.MaxValue;
        for (int i = 0; i < noisePoints.Count; i++)
        {
            var delta = Math.Abs( noisePoints[i].X - rect.Size.Y );
            if (delta < lowDelta) 
            {  
                midPointIndex = i; 
                lowDelta = delta;
            }
            else break;  // assumes in sorted list that value will only get closer until correct index is passed
        }
        GD.Print(noisePoints.Count + " " + midPointIndex);

        var leftSide = noisePoints.Slice(0, midPointIndex).ToArray();
        var rightSide = noisePoints.Slice(midPointIndex, noisePoints.Count - midPointIndex -1).ToArray();
        // need the slice to be actually length approriate 
        
        
        // normalize point positions
        leftSide = gu.TranslatePolygon(leftSide, -leftSide[0]);
        rightSide = gu.TranslatePolygon(rightSide, -rightSide[0]);

        //rotate to vertical
        leftSide = gu.RotatePolygon(leftSide, (float)Math.PI/2);
        rightSide = gu.RotatePolygon(rightSide, (float)Math.PI/2);

        // move right side over and invert
        rightSide = gu.TranslatePolygon(rightSide, new Vector2(rect.Size.X, 0) );
        var _ = leftSide.ToList();
        _.Reverse();
        leftSide = _.ToArray();

        //smooth and tesselate each side separately; 
        var leftCurve = gu.PointsToCurve(leftSide, 0.5f, false);
        var rightCurve = gu.PointsToCurve(rightSide, 0.5f, false);
        
        var rightLastIdx = rightCurve.PointCount - 1;
        // smooth curves into flat tops
        var leftTopPoint = leftCurve.GetPointPosition(0);
        var rightTopPoint = rightCurve.GetPointPosition(rightLastIdx);
        var topVector = (rightTopPoint - leftTopPoint).Normalized();

        var topEdgeRadius = 2;
        var topNormal =  topVector.Rotated((float)Math.PI/2) * topEdgeRadius;
        
        var handleLeft = -1 * topVector * topEdgeRadius / 3;
        var handleRight = topVector * topEdgeRadius / 3;

        var leftNewPoint = leftTopPoint + topVector.Rotated((float)Math.PI/4);
        var rightNewPoint = rightTopPoint + (-1 * topVector).Rotated(-(float)Math.PI/4);

        
        leftCurve.SetPointIn(0, Vector2.Zero);
        rightCurve.SetPointOut(rightLastIdx, Vector2.Zero);
        leftCurve.AddPoint(leftNewPoint, Vector2.Zero, handleLeft, 0);
        rightCurve.AddPoint(rightNewPoint, handleRight , Vector2.Zero);
        // var topRotate = topVector.Rotated(0.2f);
        // var topRotateMinus = topVector.Rotated(-0.2f);
       
        // var topVector = rightCurve.GetPointPosition(rightLastIdx) - leftCurve.GetPointPosition(0);
        // rightCurve.SetPointIn( rightLastIdx, topVector.Normalized() * 2);
        // rightCurve.SetPointOut( rightLastIdx, topVector.Normalized() * -2);
        

       
        


       
        var smoothLeftPoly = leftCurve.Tessellate();
        var smoothRightPoly = rightCurve.Tessellate();
       

        // combine into one polygon, then into a smoothed out curve
        var unitPolygon = new List<Vector2>();
        unitPolygon.AddRange(smoothLeftPoly);
        unitPolygon.AddRange(smoothRightPoly);
        var unitPolyTranslated = gu.TranslatePolygon(unitPolygon.ToArray(), rect.Position);

        


        
        // GD.Print("total points after tes: " + tesselatedPoly.Length);

        var polyInst = new Polygon2D(){Polygon = unitPolyTranslated};
        polyInst.Position = new Vector2(0, -180);
        AddChild(polyInst);





    }


}
