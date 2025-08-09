using Godot;
using System;
using System.Collections.Generic;

public partial class LineDrawing : Node3D
{
    [Export] PlaneMouseCapture planeMouseScene;
    public Action<Vector3[]> lineDrawn;

    public bool doPreview = true;
    public float minTimeInterval = 0.1f; // min time between new lines
    public float minDistanceInterval = 2f; // min distance covered by each line

    bool isDrawing = false;
    bool isMouseDown = false;

    float timeSinceLine = 0;


    List<Vector3[]> lineSegments = new();
    Vector3? lastLineEnd;
    

    public override void _Ready()
    {

    }


    public override void _Process(double delta)
    {
        isMouseDown = Input.IsMouseButtonPressed(MouseButton.Left);


        if (isMouseDown) timeSinceLine += (float)delta;
        else lastLineEnd = null;

    }

    public override void _Input(InputEvent @event)
    {
        
        if (isMouseDown && @event is InputEventMouseMotion eventMouseMotion)
        {

            var mousePos = planeMouseScene.LastMousePos;
            // GD.Print("mousePos");

            if (mousePos == Vector3.Zero) return;
            ProcessMousePos(mousePos);


        }
    }

    private void ProcessMousePos(Vector3 mousePos)
    {
        if (timeSinceLine < minTimeInterval) return;
        if (lastLineEnd == null) lastLineEnd = mousePos;
        Vector3 start = (Vector3)lastLineEnd;
        var dist = start.DistanceTo(mousePos);

        if (dist > minDistanceInterval)
        {
            lineSegments.Add([start, mousePos]);
            lastLineEnd = mousePos;
            lineDrawn.Invoke(lineSegments[^1]);
            timeSinceLine = 0;
        }

    }

    

}
