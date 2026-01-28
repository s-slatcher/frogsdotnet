using Godot;
using System;

public partial class TestCharacter : CharacterBody2D
{

    Node2D spriteLayer;

    // constants
    const float GRAVITY = 1000;
    const float BASE_MAX_SPEED = 300f;
    const float VELOCITY_ACCEL_SMOOTH = 5;
    const float JUMP_SPEED = -120f;
    
    // character state
    

    // input state
    float xAxis = 0f;
    float yAxis = 0f;
    float xHeldCount = 0f;
    float coyoteTimeCount = 0f;
    float jumpBufferCount = 0f;
    float hang_time = 0;

    
    // input constants
    const float X_HELD_MIN = 0.02f;
    const float X_HELD_MAX = 0.3f;
    const float COYOTE_TIME_MAX = 0.5f;
    const float JUMP_BUFFER_MAX = 0.5f;

    const float HANG_TIME_MAX = 0.2f;

    

    // debug ui
    ProgressBar MoveHoldIndicator;


    public override void _Ready()
    {
        MoveHoldIndicator = GetNode<ProgressBar>("CanvasLayer/MovementHoldIndicator");
        spriteLayer =   GetNode<Node2D>("sprite");
    }

    public override void _PhysicsProcess(double delta)
    {
        DrawDebug();

        var velX = Velocity.X;
        var velY = Velocity.Y;

        // run collision first
        var collision = MoveAndSlide();
        bool grounded = IsOnFloor();

        // flip sprite
        if (xAxis != 0) spriteLayer.Scale = new Vector2(Math.Sign(xAxis), 1);
        

        // handle hang time and gravity
        // 
        var grav = GRAVITY * (float)delta;
        var max_hang = HANG_TIME_MAX * (xHeldCount/X_HELD_MAX);
        if (velY < 0 && velY + grav > 0)
        {
            velY = 0;
            // relative max hang time depends on move speed 
            
                        
            hang_time = max_hang;        

        }
        
        if (hang_time > 0) hang_time = Math.Clamp(hang_time - (float)delta, 0, max_hang);
        if (hang_time < 0) hang_time = 0;
        if (hang_time == 0) velY += grav;
    
        
        

        // jump
        if (grounded && xHeldCount > 0) velY = JUMP_SPEED;
        
        

       

        // x axis input
        var inputAxis = Input.GetAxis("left", "right");

        if (Math.Abs(inputAxis) < 0.05 || Mathf.Sign(inputAxis) != Mathf.Sign(xAxis)) xHeldCount = 0;
        else xHeldCount = Math.Clamp( xHeldCount + (float)delta, X_HELD_MIN, X_HELD_MAX);
           
        

        xAxis = inputAxis;

        // issues --- not enough impulse when feathering movement, too much slipping when changing direction or stopping after fast movement
        var targetVelocity = (xHeldCount/X_HELD_MAX) * BASE_MAX_SPEED * Math.Sign(xAxis);
        velX = float.Lerp(velX, targetVelocity, (float)(1.0 - Math.Exp(-delta * VELOCITY_ACCEL_SMOOTH)));
        

        // apply velocity changes
        Velocity = new Vector2(velX, velY);


        // progress to max hold time decides of percent of max speed to set velocity too (makes accel not linear by exponent on hold time percentage)

        
        


    }


    public void DrawDebug()
    {
       MoveHoldIndicator.Value = xHeldCount / X_HELD_MAX; 
       GetNode<RichTextLabel>("CanvasLayer/HangTime").Text = "Hang Time: " + hang_time;
       
       GetNode<RichTextLabel>("CanvasLayer/Velocity").Text = "velocity: " + Math.Round(Velocity.X,2) + "," + Math.Round(Velocity.Y,2); 
    }

    



    
 


}
