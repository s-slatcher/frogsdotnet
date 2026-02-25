using Godot;
using System;

public partial class TestCharacter : CharacterBody2D
{

    Node2D spriteLayer;

    // constants
    const float GRAVITY = 800;
    const float FALLING_GRAV_MULT = 2f;
    const float BASE_MAX_SPEED = 120f;
    
    const float VELOCITY_ACCEL_SMOOTH = 100;
    const float JUMP_SPEED = -130f;
    const float SLIDE_TIME_MAX = 0.08f;
    
    // character state
    

    // input state
    float xAxis = 0f;
    float yAxis = 0f;
    float xHeldCount = 0f;
    float coyoteTimeCount = 0f;
    float jumpBufferCount = 0f;
    float slideTimeCount = 0f;
    float hang_time = 0;

    
    // input constants
    const float X_HELD_MIN = 0.0f;
    const float X_HELD_MAX = 0.3f;
    const float COYOTE_TIME_MAX = 0.5f;
    const float JUMP_BUFFER_MAX = 0.4f;

    const float HANG_TIME_MAX = 0.15f;

    

    // debug ui
    ProgressBar MoveHoldIndicator;


    public override void _Ready()
    {
        MoveHoldIndicator = GetNode<ProgressBar>("CanvasLayer/MovementHoldIndicator");
        spriteLayer =   GetNode<Node2D>("sprite");
    }

    public override void _PhysicsProcess(double delta)
    {


        // notes to self:
        // - strip held-count features one by one to see what matters for feel
        // - swap in a two sprites of frog standing and jumping
        // - add a grounded slide phase based on speed (faster, shorter jumps at slow speed)
        // - read controller strentgh instead of held-count, add a wall button for keyboard (shift)
        // - switch to "true" velocity reads at start and see if its buggy
        // - try this: no accelration (or very high base speed), but longer held time for max jump

        // next steps for control:
        // - add in some small delay for turning left/right
        // - add in a sprite swapping tween that can be reversed on a spam-protected mid-air input 
        // jump: 
        // - add simple jump back in (charging added later)
        // - up / down to rotate, see how it feels in combination with delayed left-right rotate
        // - make rotation an accelartion force so its harder to preciesly control 

        // adding in tongue grapple:
        // - get tongue grapple script and 


        DrawDebug();

        var velX = Velocity.X;
        var velY = Velocity.Y;
       

        // run collision first
        var collision = MoveAndSlide();
        bool grounded = IsOnFloor();

        
        // x axis input
        var inputAxis = Input.GetAxis("left", "right");
        if (Math.Abs(inputAxis) < 0.05 || Mathf.Sign(inputAxis) != Mathf.Sign(xAxis)) xHeldCount = 0;
        else xHeldCount = Math.Clamp( xHeldCount + (float)delta * Math.Abs(xAxis), X_HELD_MIN, X_HELD_MAX);
        xAxis = inputAxis;
        var heldRatio = (xHeldCount/X_HELD_MAX);
        // flip sprite
        if (xAxis != 0) spriteLayer.Scale = new Vector2(Math.Sign(xAxis), 1);
        

        // handle hang time and gravity
        // 
        var grav = GRAVITY * (float)delta * (velY < 0 ? 1 : FALLING_GRAV_MULT);
        var max_hang = HANG_TIME_MAX * heldRatio;
        if (velY < 0 && velY + grav > 0)
        {
            velY = 0;
            hang_time = max_hang;        
        }
        
        if (hang_time > 0) hang_time = Math.Clamp(hang_time - (float)delta, 0, max_hang);
        if (hang_time <= 0) velY += grav ;
    

        // short jump -- set to a minimum of half max, then scaled over x-held time
        var halfJump = 0.5f * JUMP_SPEED;
        if (grounded && xHeldCount > 0) 
        {
            slideTimeCount += (float)delta;
            if (slideTimeCount > SLIDE_TIME_MAX * heldRatio)
            {
                velY = halfJump + halfJump * heldRatio;
                slideTimeCount = 0;
            }
            
        }

        // jump buffering should check for closeness to ground, force a quick landing for faster jumping
        // combine with coyote time calc: if they just left the ground, 
        // possibly need overall shorter air times for more resposiveness (too high buffers feels bad) 

        // if (Input.IsActionJustPressed("jump")) 
        // {
        //     jumpBufferCount = JUMP_BUFFER_MAX;
        //     if (hang_time > 0) hang_time = 0;
        // }
        // jumpBufferCount -= (float)delta;
        // bool jumpBuffered = jumpBufferCount > 0;
        //  if (grounded && jumpBuffered) velY = JUMP_SPEED * 3;
        

        var targetVelocity = BASE_MAX_SPEED * xAxis;
        velX = float.Lerp(velX, targetVelocity, (float)(1.0 - Math.Exp(-delta * VELOCITY_ACCEL_SMOOTH)));
        

        // apply velocity changes
        Velocity = new Vector2(velX, velY);


    }


    public void DrawDebug()
    {
       MoveHoldIndicator.Value = xHeldCount / X_HELD_MAX; 
       GetNode<RichTextLabel>("CanvasLayer/HangTime").Text = "Hang Time: " + Math.Round(hang_time,2);
       
       GetNode<RichTextLabel>("CanvasLayer/Velocity").Text = "velocity: " + Math.Round(Velocity.X,2) + "," + Math.Round(Velocity.Y,2); 
    }

    



    
 


}
