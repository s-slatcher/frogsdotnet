using Godot;
using System;

public partial class CharacterBody3d : CharacterBody3D
{
    public PlayerAnimations3d bodyAnimations;
    float gravity = -9.5f;
    float maxSpeed = 5f;
    float AccelSmoothing = 15f;

    public override void _Ready()
    {
        bodyAnimations = GetNode<PlayerAnimations3d>("PlayerAnimations3d");
    }



    public override void _Process(double delta)
    {
        var axis = Input.GetAxis("ui_left", "ui_right");
        if (axis != 0) bodyAnimations.Scale = new Vector3(axis, 1, 1);
        var velocityY = Velocity.Y + (gravity * (float)delta);
        var targetSpeed = (float)maxSpeed * axis;   
        var currentSpeed = float.Lerp(Velocity.X, targetSpeed, (float)(1.0 - Math.Exp(-delta * AccelSmoothing)));
        
        Velocity = new Vector3(currentSpeed, velocityY, 0);
		MoveAndSlide();
        

    }

}
