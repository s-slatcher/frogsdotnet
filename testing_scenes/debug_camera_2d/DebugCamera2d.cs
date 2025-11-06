using Godot;
using Microsoft.VisualBasic;
using System;

public partial class DebugCamera2d : Camera2D
{

    public Rect2 trueScreenRect;
    Vector2 dragStartMousePos;
    Vector2 dragStartCameraPos;
    
    bool isDragging = false;

    string savePath = "user://resource_save/";
    string saveName = "debugCameraData.tres";

    bool isFlipped = false;

    

    public override void _Ready()
    {
        
        Error error = DirAccess.MakeDirAbsolute(savePath);
        GD.Print(error);
        GetTree().CreateTimer(0.5).Timeout += OnSaveTimer;
        LoadData();
        SetScreenRect();
        var button = GetNode<CheckButton>("CanvasLayer/Control/CheckButton");
        button.Toggled += OnCheckButtonPress;
    }

    private void OnCheckButtonPress(bool state)
    {
        if (state) RotationDegrees = 180;
        else RotationDegrees = 0;
        isFlipped = state;
    }


    private void OnSaveTimer()
    {
        WriteData();
        GetTree().CreateTimer(0.5).Timeout += OnSaveTimer;
    }


    private void LoadData()
    {

        var exists = ResourceLoader.Exists(savePath + saveName);

        if (exists)
        {
            var data = ResourceLoader.Load<DebugCameraData>(savePath + saveName);
            Position = data.Position;
            Zoom = data.Zoom;
        }
        else GD.Print("no camera data found");
    }

    private void WriteData()
    {
        var data = new DebugCameraData(){Position = this.Position, Zoom = this.Zoom};
        _ = ResourceSaver.Save(data, savePath + saveName);
    }


    public override void _PhysicsProcess(double delta)
    {
        SetScreenRect();
        UpdateLabels();


        var xAxis = Input.GetAxis("left", "right");
        var yAxis = Input.GetAxis("forward", "down");
        if (isFlipped) { xAxis *= -1; yAxis *= -1; };

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
