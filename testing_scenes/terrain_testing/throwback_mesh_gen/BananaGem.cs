using Godot;
using System;

public partial class BananaGem : Sprite3D
{
    public override void _PhysicsProcess(double delta)
    {
        Rotate(Vector3.Back, (float)(0.5 * delta));        
    }

    
        
    

}
