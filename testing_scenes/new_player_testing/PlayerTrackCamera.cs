using Godot;
using System;

public partial class PlayerTrackCamera : Camera2D
{
    
    [Export] CharacterBody2D player;

    Vector2 targetPos;
    [Export] bool TrackPlayer = true;
    float AccelSmoothing = 20;

    public override void _Ready()
    {
        targetPos = Position;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        targetPos = new Vector2(player.Position.X, Position.Y);

        if (TrackPlayer) Position = Position.Lerp(targetPos, (float)(1.0 - Math.Exp(-dt * AccelSmoothing)));
    }


}
