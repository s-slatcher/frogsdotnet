using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Vector2 = Godot.Vector2;

public partial class TestPlayer : CharacterBody2D
{
    double bodyAirTime = 0;
    double bodyMaxAirTime = 0f;
    double bodyMaxHeight = -30;
    double AccelSmoothing = 15f;
    double spriteTargetHeight = 0;

    Node2D body;
    AnimationPlayer animation;
    double maxSpeed = 300;
    Tween tween;
    Tween tween2;
    Vector2 gravity = new(0, 500);

    public override void _Ready()
    {
       body = GetNode<Node2D>("visuals");
       animation = GetNode<AnimationPlayer>("AnimationPlayer");
       animation.Play("leap");
       animation.SpeedScale = 0;
    }


    public override void _PhysicsProcess(double delta)
    {
        var axis = Input.GetAxis("ui_left", "ui_right");
        if (axis != 0) body.Scale = new Vector2(axis, 1);
        Velocity += gravity * (float)delta;
        var speed = (float)maxSpeed * axis;
        var targetVelocity = new Vector2(speed, Velocity.Y); 
        Velocity = Velocity.Lerp(targetVelocity, (float)(1.0 - Math.Exp(-delta * AccelSmoothing)));
		MoveAndSlide();

        var animationTime = animation.CurrentAnimationLength;

        // update animation state
        
        
        double newBodyTarget = 0;
        
        if (axis != 0) newBodyTarget = bodyMaxHeight;
        else if (body.Position.Y == bodyMaxHeight) newBodyTarget = 0;


        // air time only changes at bottom and top of jump -- is locked in between states
        if (body.Position.Y == bodyMaxHeight){
            bodyAirTime += delta;
        }

        if (body.Position.Y == 0){
            bodyAirTime = 0;
        }

        if (bodyAirTime > bodyMaxAirTime) newBodyTarget = 0; // at peak of jump for too long, stays this way until reset when hitting ground
        var targetPosition = new Vector2(0, (float)newBodyTarget);

        double speedFactor = Math.Sqrt( Math.Abs(Velocity.X / maxSpeed) ); // 
        tween?.SetSpeedScale( 1f - (float)speedFactor/2f );
        
        var heightFactor = Math.Abs(body.Position.Y / bodyMaxHeight);
        SeekAnimation(Math.Pow(heightFactor,2) * animationTime);


        if ((body.Position.Y == 0 && Math.Abs(targetPosition.Y) > 0) || (body.Position.Y == bodyMaxHeight && targetPosition.Y == 0) )
        {
            tween?.Kill();
            tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quad);
            if (targetPosition.Y == 0) {
                tween.SetEase(Tween.EaseType.In);
                tween.TweenProperty(body, "position", targetPosition, 0.2);
            }
            else 
            {
                tween.SetEase(Tween.EaseType.Out);
                tween.TweenProperty(body, "position", targetPosition, 0.1);
            }
        }

    }

    public void SeekAnimation(double time)
    {
        animation.Seek(time, true);
    }

}
