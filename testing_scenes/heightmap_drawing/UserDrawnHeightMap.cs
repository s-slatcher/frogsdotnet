using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class UserDrawnHeightMap : Node3D
{
    public Button DrawButton;
    private bool QueuedToDraw = false;
    private bool IsDrawing = false;
    private Line2D previewLine = new();
    private float DrawPointMinDistance = 30;

    private List<Vector2> drawPoints = new();

    public override void _Ready()
    {
        DrawButton = GetNode<Button>("CanvasLayer/NewDraw");
        DrawButton.Pressed += OnDrawButtonPressed;
        AddChild(previewLine);

    }

    public override void _Process(double delta)
    {
        var isMousedDown = Input.IsMouseButtonPressed(MouseButton.Left);

        if (QueuedToDraw && isMousedDown)
        {
            IsDrawing = true;
            QueuedToDraw = false;
        }

        if (IsDrawing && !isMousedDown)
        {
            EndDrawing();
        }


    }

    public override void _Input(InputEvent @event)
    {
        if (IsDrawing && @event is InputEventMouseMotion eventMouseMotion)
        {
            var mousePos = eventMouseMotion.Position;
            var lastPos = drawPoints.Count > 0 ? drawPoints[^1] : new Vector2(-10000, -10000);
            LiveUpdatePreviewLine(mousePos);
            if ((mousePos - lastPos).Length() > DrawPointMinDistance)
            {
                drawPoints.Add(mousePos);
                previewLine.AddPoint(mousePos);
                GD.Print(mousePos);
            }
        }
    }

    public void LiveUpdatePreviewLine(Vector2 mousePos)
    {
        if (previewLine.Points.Length == 0) previewLine.AddPoint(new Vector2(0, 0));

        previewLine.SetPointPosition(previewLine.Points.Length - 1, mousePos);
    }
    private void AddPreviewPoint(Vector2 mousePos)
    {
        previewLine.RemovePoint(previewLine.Points.Length - 1);
        previewLine.AddPoint(mousePos);
    }


    // private void DrawPoint()
    // {
    //     var mousePos = MouseEve
    // }


    private void EndDrawing()
    {

        IsDrawing = false;
        previewLine.Points = [];
        var cleanedPoints = CleanDrawPoints();
        TerrainFromPointList(cleanedPoints);
        drawPoints = new();

    }

    private List<Vector2> CleanDrawPoints()
    {
        var cleanPoints = new List<Vector2>();
        for (int i = 0; i < drawPoints.Count; i++)
        {
            cleanPoints.Add(drawPoints[i] * new Vector2(1, 1));
        }
        return cleanPoints;
    }


    private void OnDrawButtonPressed()
    {
        if (QueuedToDraw || IsDrawing) return;
        QueuedToDraw = true;

    }

    private void TerrainFromPointList(List<Vector2> points)
    {
        var terrain = new TerrainMap();
        // List<Vector2> pointList = new()
        // {
        //     new Vector2(0, 20), new Vector2(10,40), new Vector2(20, 20), new Vector2(30, 25),
        //     new Vector2(40, 20), new Vector2(50,40), new Vector2(60, 100), new Vector2(80, 0)
        // };
        var polys = terrain.GenerateFromPointList(points);

        foreach (var poly in polys)
        {
            poly.Scale = new Vector2(-1, 1);
            AddChild(poly);
        }
    }

    // void Remap(List<Vector2> points, float sampleMin, float sampleMax)
    // {
        

    //     for (int i = 0; i < points.Count; i++)
    //     {
    //         var pointHeight = points[i].Y;
    //         var remapHeight = (pointHeight - sampleMin) / (sampleMax - sampleMin) * (MaxHeight - MinHeight) + MinHeight;
    //         points[i] = new Vector2(points[i].X, remapHeight);
    //     } 

    // }
    

}
