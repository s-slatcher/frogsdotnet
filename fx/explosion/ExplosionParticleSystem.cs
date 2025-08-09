using Godot;
using System;

public partial class ExplosionParticleSystem : Node3D
{
    public override void _Ready()
    {
        var explosion = (GpuParticles3D)GetChild(0);
        
        var mesh = (SphereMesh)explosion.DrawPass1;
        mesh.Radius = Scale.X * 1.5f * 0.5f;
        mesh.Height = Scale.X * 1.5f;
        explosion.Emitting = true;
    }

}
