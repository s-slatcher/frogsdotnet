using Godot;
using System;

public partial class DebugControls : Node
{


    // (possible) todo: 
	// scene loader - left dpad to save scene, right to load it
	// down dpad to pause time 
	[Export] float SlowMotionSpeed = 0.25f;
	bool slowMotion = false;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("DebugSlowMotionToggle"))
		{
			if (!slowMotion) Engine.TimeScale = SlowMotionSpeed;
			else Engine.TimeScale = 1;
			slowMotion = !slowMotion;
		}
		
		// if event is stop time
		//   if time stopped: start
		//	 else start time
    }

}
