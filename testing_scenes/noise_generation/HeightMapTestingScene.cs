using Godot;
using Godot.NativeInterop;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Threading;
using Vector2 = Godot.Vector2;

public partial class HeightMapTestingScene : Node2D
{
    int seed = 0;
    float MaxHeight = 80;
    float MinHeight = 5;
    int Width = 300; 
    float minUnitWidth = 5;

    int islandCount = 3;
    
    
    GeometryUtils gu = new();

    public override void _Ready()
    {   
        
        seed = (int)GD.Randi();
        
        var heightMap = new HeightMap(Width, seed, 0.025f, 3, 2, 0.5f);
        
        heightMap.MaxHeight = MaxHeight;
        heightMap.MinHeight = MinHeight;
        heightMap.RemapToHeightRange = true;

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

        GenerateHeightMapPolygons_v2(pointsOfInterest);
        // GenerateHeightMapPolygons(polylinePOIs.Points);
    }

    void GenerateHeightMapPolygons_v2(List<Vector2> points)
    {
        var groupPolyList = new List<Vector2[]>();
        var groups = new List<List<Vector2>>();
        var currentGroup = new List<Vector2>();
        var groupMin = float.MaxValue;
        var groupMax = float.MinValue;
        // step 2; group points together with a running average of slope
        float tolerance = 12;
        for (int i = 1; i < points.Count; i++)
        {
            var pointHeight = points[i].Y;
            groupMin = Math.Min(pointHeight, groupMin);
            groupMax = Math.Max(pointHeight, groupMax);
            
            currentGroup.Add(points[i]);

            if (groupMax - groupMin > tolerance)
            {
                var groupWidth = currentGroup[0].X - currentGroup[^1].X;
                if (Math.Abs(groupWidth) > minUnitWidth)
                {
                    groups.Add(currentGroup);
                    currentGroup = new(){points[i]};
                    groupMin = groupMax = points[i].Y;
                }
            }

        }

        for (int i = 0; i < groups.Count; i++)
        {
            // take only the first and last points of the group as relevant (may be same point)
            var averageY = groups[i].Aggregate(0f, (acc, vec) => acc += vec.Y);
            averageY /= groups[i].Count;

            var p1 = groups[i][0];
            var p2 = groups[i][^1];

            p1.Y = p2.Y = averageY;
            
            var groupGapOffset = new Vector2(i * 15, 0); // add only for debugging purposes
            

            var polygon = new Vector2[]
            {
                p1 + groupGapOffset ,
                p2 + groupGapOffset ,
                new Vector2(p2.X, 0) + groupGapOffset ,
                new Vector2(p1.X, 0) + groupGapOffset ,
            };

            
            groupPolyList.Add(GenerateUnit(gu.RectFromPolygon(polygon)));
            // add group polygon to scene

            var poly2d = new Polygon2D(){Polygon = polygon};
            poly2d.Position = new Vector2(0, -60);
            poly2d.Modulate = new Godot.Color(GD.Randf(), GD.Randf(), GD.Randf(), 1);
            AddChild(poly2d);
            
        }

        var alignedUnits = AlignUnits(groupPolyList);


    }
    
    Vector2[] CurlEndsOfNoiseLine(Vector2[] noiseLine, float curlRadius, Vector2 noiseOrientation, int curlDirection)
    {
        if (curlDirection != -1 && curlDirection != 1) return noiseLine;
        List<Vector2> newNoiseList = [..noiseLine];
        var edgeVector = noiseOrientation.Normalized() * curlRadius;
        var bottomEdgeVector = edgeVector * -1;

        var topEdgeCenter = edgeVector.Rotated( float.Pi/2 * curlDirection) + noiseLine[^1];
        var bottomEdgeCenter = edgeVector.Rotated(float.Pi/2 * curlDirection) + noiseLine[0];
        
        Vector2 topEdge1 = topEdgeCenter + edgeVector.Rotated( -curlDirection * float.Pi/4);
        Vector2 topEdge2 = topEdgeCenter + edgeVector;
        
        Vector2 bottomEdge1 = bottomEdgeCenter + bottomEdgeVector;
        Vector2 bottomEdge2 = bottomEdgeCenter + bottomEdgeVector.Rotated(curlDirection * float.Pi/4);

        newNoiseList.AddRange([topEdge1, topEdge2]);
        newNoiseList.InsertRange(0, [bottomEdge1, bottomEdge2]);

        return newNoiseList.ToArray();
    }

    Vector2[] GenerateUnit(Rect2 rect)
    {

        var sideAngle = float.Pi/18;
        var sideLength =  rect.Size.Y / float.Cos(sideAngle);
        var baseWidth = Math.Sqrt(sideLength*sideLength - rect.Size.Y * rect.Size.Y);
        var noiseWidth = 8;
        var noiseDirectionRatio = 0.33f; 
        

        // generate two different curve stretches
        var hm = new HeightMap(sideLength, (int)GD.Randi(), 0.06f, 5, 2.25f, 0.45f);
        // hm.RemapToHeightRange = true
        
        
        hm.MaxHeight = noiseWidth * ( 1 - noiseDirectionRatio);
        hm.MinHeight = hm.MaxHeight - noiseWidth;
        
        var leftSide = hm.GetPointsOfInterest().ToArray();
        
        hm.domainOffset = rect.Size.X * 2;
        var rightSide = hm.GetPointsOfInterest().ToArray();

        // normalize positions
        leftSide = gu.TranslatePolygon(leftSide, new Vector2(-leftSide[0].X, 0));
        rightSide = gu.TranslatePolygon(rightSide, new Vector2(-rightSide[0].X, 0));
        
        // add ending points to transition into flat surface
        leftSide = CurlEndsOfNoiseLine(leftSide, 1, new Vector2(1,0), -1);
        rightSide = CurlEndsOfNoiseLine(rightSide, 1, new Vector2(1,0), -1);

        //rotate to match side angle
        leftSide = gu.RotatePolygon(leftSide, float.Pi/2 - sideAngle);
        rightSide = gu.RotatePolygon(rightSide, float.Pi/2 - sideAngle);

        rightSide = gu.ScalePolygon(rightSide, new Vector2(-1, 1));

        
    
        var translateX = rect.Size.X + (baseWidth * 2);
        var translation = new Vector2( (float)translateX, 0); 
        var translatedRightSide = gu.TranslatePolygon(rightSide.ToArray(), translation);
        var combinedUnitPoly = leftSide.Reverse().Concat(translatedRightSide).ToArray();

        // convert to curve and smooth, then tesselate back into polygon
        var smoothCurve = gu.PointsToCurve(combinedUnitPoly, 0.5f, false);
        var tesselatePoly = smoothCurve.Tessellate();
       
        var translatedPoly = gu.TranslatePolygon(tesselatePoly, rect.Position);

        var polyInst = new Polygon2D(){Polygon = translatedPoly};
        polyInst.Position = new Vector2(0, -120);
        AddChild(polyInst);

        return translatedPoly;

    }

    List<Vector2[]> AlignUnits(List<Vector2[]> unitPolygons)
    {
        // squish each unit up against each other to leave minimal gaps without overlapping the top surface of each unit
        var alignedUnits = new List<Vector2[]>();
        
        var smallGapBaseChance = 0.25;
        var smallGapSizeMean = 4;

        var largeGapFrequency = (float)unitPolygons.Count / (float)(islandCount);
        var largeGapMean = 50;
        var gapIndices = new int[islandCount-1];
        
        for (int i = 0; i < gapIndices.Length; i++) gapIndices[i] = (int) (largeGapFrequency * (i + 1) );

        for (int i = 0; i < unitPolygons.Count - 1; i++)
        {
            var heightFactor = unitPolygons[i][0].Y / MaxHeight;
            var splitChance = smallGapBaseChance * (1 - heightFactor); //lower height = higher chance to split
            var splitGap = GD.Randf() < splitChance ? GD.Randfn(smallGapSizeMean, 1) : 0;
            if (gapIndices.Contains(i)) splitGap += GD.Randfn(largeGapMean, 10); 

            if (splitGap > 0)
            {
                var unitRectLeft = gu.RectFromPolygon(unitPolygons[i]);
                var unitRectRight = gu.RectFromPolygon(unitPolygons[i+1]);
                var leftX = unitRectLeft.End.X;
                var rightX = unitRectRight.Position.X;
                unitPolygons[i+1] = gu.TranslatePolygon(unitPolygons[i+1], new Vector2(leftX - rightX + (float)splitGap, 0));
                
                
            }
            else
            {

                var unitSlice = unitPolygons.Slice(i, 2);
                bool leftUnitIsShorter = unitSlice[0][0].Y < unitSlice[1][0].Y; 

                Vector2 topEdge;
                Vector2 mergePoint;
                Vector2 rightUnitTranslation;

                if (leftUnitIsShorter)
                {
                    topEdge = unitSlice[0][^1];
                    mergePoint = HeightMatchPointToUnitEdge(unitSlice[1], topEdge, false);
                    rightUnitTranslation = new Vector2( (topEdge - mergePoint).X, 0);

                }
                else
                {
                    topEdge = unitSlice[1][0];
                    mergePoint = HeightMatchPointToUnitEdge(unitSlice[0], topEdge, true );
                    rightUnitTranslation = new Vector2( (mergePoint - topEdge).X, 0);
                }

                unitPolygons[i+1] = gu.TranslatePolygon(unitPolygons[i+1], rightUnitTranslation + new Vector2((float)splitGap, 0 ));

            }
            
            var polyInst = new Polygon2D(){Polygon = unitPolygons[i]};
            polyInst.Position = new Vector2(0, -200);
            if (i == 0) polyInst.Modulate = Colors.Green;
            AddChild(polyInst);
        }
        var polyInstLast = new Polygon2D(){Polygon = unitPolygons[^1]};
        polyInstLast.Position = new Vector2(0, -200);
        AddChild(polyInstLast);

        return alignedUnits;
    }

    private Vector2 HeightMatchPointToUnitEdge(Vector2[] unitPolygon, Vector2 point, bool rightEdge)
    {
        // assumes units have roughly same number of points on left and right sides -- could possibly fail with extreme height differences and bad luck
        Vector2 indexRange;
        if (rightEdge) indexRange = new Vector2(unitPolygon.Length/2 - 1, unitPolygon.Length);
        else indexRange = new Vector2(0, unitPolygon.Length/2 + 1);
        
        var matchingPoint = Vector2.Zero;
        var lowestDelta = float.MaxValue;
       
        for (int i = (int)indexRange.X; i < indexRange.Y; i++)
        {
            var unitP = unitPolygon[i];
            var delta = Math.Abs(point.Y - unitP.Y); 
            if (delta < lowestDelta)
            {
                matchingPoint = unitPolygon[i];
                lowestDelta = delta;
            }        
        }
        return matchingPoint;
    
    }

}
