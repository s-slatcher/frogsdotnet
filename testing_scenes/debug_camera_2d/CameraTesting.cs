using Godot;
using System;
using System.Diagnostics;

public partial class CameraTesting : Node2D
{

    public override void _PhysicsProcess(double delta)
    {
        var camera = GetNode<DebugCamera2d>("DebugCamera2d");

        var rect = camera.trueScreenRect;
        
        // var line = GetNode<Line2D>("Line2D");
        // line.Points = new Vector2[] { rect.Position,rect.End };
            
        


       

    }

}
