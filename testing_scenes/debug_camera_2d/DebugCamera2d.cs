using Godot;
using System;

public partial class DebugCamera2d : Camera2D
{

    public Rect2 trueScreenRect;
    Vector2 dragStartMousePos;
    Vector2 dragStartCameraPos;
    
    bool isDragging = false;


    public override void _Ready()
    {
        SetScreenRect();
    }

    public override void _PhysicsProcess(double delta)
    {
        SetScreenRect();
        UpdateLabels();


        var xAxis = Input.GetAxis("left", "right");
        var yAxis = Input.GetAxis("forward", "down");

        var speed = (float)delta * trueScreenRect.Size.X * 0.75f;
        if (Input.IsActionPressed("shift")) speed *= 2;

        Position += new Vector2(xAxis, yAxis) * speed;


        if (Input.IsActionJustPressed("scroll_down")) Zoom *= 0.85f;
        if (Input.IsActionJustPressed("scroll_up")) Zoom *= 1 / 0.85f;


        var mousePosLocal = GetNode<Control>("CanvasLayer/Control").GetLocalMousePosition();
        var mousePosGlobal = GetGlobalMousePosition();
        var label = GetNode<Label>("CanvasLayer/Control/MouseLabel");
        label.Position = mousePosLocal + new Vector2(0, 50);
        label.Text = mousePosGlobal.ToString();
        
    }

    // public override void _UnhandledInput(InputEvent @event)
    // {
    //     if (@event is )
    // }


    private void UpdateLabels()
    {
        GetNode<Label>("CanvasLayer/Control/PositionLabel").Text = trueScreenRect.Position.ToString();
        GetNode<Label>("CanvasLayer/Control/EndLabel").Text = trueScreenRect.End.ToString();
    }

    private void SetScreenRect()
    {
        
        var cameraCenter = GetScreenCenterPosition();
        var zoom = Zoom;
        var screenSize = GetViewport().GetVisibleRect().Size;
        screenSize /= zoom;

        var rectPos = cameraCenter - (screenSize / 2);
        var rectEnd = cameraCenter + (screenSize / 2);

        trueScreenRect = new Rect2();
        trueScreenRect.Position = rectPos;
        trueScreenRect.End = rectEnd;

    }



}
