using Godot;
using System;
using System.Collections.Generic;

public partial class WasdCamera : Camera3D
{

    Vector3 targetPosition = Vector3.Zero;

    Vector3 startPosition;
    public float AccelSmoothing = 20f;

    List<float> zoomLevels = [0, -60, -80];
    List<float> speeds = [50, 30, 20];
    int currentZoom = 0;

    float currentSpeed;

    public override void _Ready()
    {
        startPosition = GlobalPosition;
        targetPosition = startPosition;
        currentSpeed = speeds[currentZoom];
    }


    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var speed = currentSpeed * dt;
        if (Input.IsActionPressed("shift")) speed *= 3;
        // movement on xy plane
        var yAxis = Input.GetAxis("down", "forward");
        var xAxis = Input.GetAxis("left", "right");

        targetPosition += new Vector3(xAxis * speed, yAxis * speed, 0);
        Position = Position.Lerp(targetPosition, (float)(1.0 - Math.Exp(-dt * AccelSmoothing)));

        if (Input.IsActionJustPressed("ui_accept")) CycleZoom();


    }

    private void CycleZoom()
    {
        currentZoom += 1;
        if (currentZoom >= zoomLevels.Count) currentZoom = 0;
        targetPosition.Z = startPosition.Z + zoomLevels[currentZoom];
        // Position = new Vector3(Position.X, Position.Y, startPosition.Z + zoomLevels[currentZoom]);
        currentSpeed = speeds[currentZoom];
    }

}
