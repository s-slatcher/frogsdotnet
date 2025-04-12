using Godot;
using System;

public partial class PlayerAnimations3d : Node3D
{   
    
    double TimeHeld = 0;
    double TimeHeldMax = 0.1;

    public override void _Process(double delta)
    {

        TimeHeld = double.Clamp(TimeHeld ,0, TimeHeldMax);

        var anim = GetNode<AnimationPlayer>("AnimationPlayer");

        if (Input.IsActionPressed("ui_right") || Input.IsActionPressed("ui_left")){

            TimeHeld += delta;


            if (anim.CurrentAnimation != "leap")
            {
                anim.Play("leap", 0.15);
                
            }
        }   

        else{

            TimeHeld = 0;
            
            anim.Queue("sit");
        }
        SetAnimSpeed();
        
    }

    private void SetAnimSpeed()
    {
        var anim = GetNode<AnimationPlayer>("AnimationPlayer"); 

        if (anim.CurrentAnimation != "leap") return;
        var animProgress = anim.CurrentAnimationPosition / anim.CurrentAnimationLength;

       var animMiddleness = Math.Pow(1f - Math.Abs(animProgress - 0.5) * 2, 2);
       var speedFactor = 1 - (TimeHeld * 4 * animMiddleness) ;
       // how close animation is too middle, scale of 1-0

       GD.Print(speedFactor);
       anim.SpeedScale = (float)speedFactor;

    }

}
