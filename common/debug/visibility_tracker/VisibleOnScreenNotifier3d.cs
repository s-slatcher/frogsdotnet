using Godot;
using System;

public partial class VisibleOnScreenNotifier3d : VisibleOnScreenNotifier3D
{
    public bool isVisible = false;

    [Export] VisualInstance3D AABBSource; 

    public override void _Ready()
    {
        CallDeferred("SetAABB");
        ScreenEntered += OnEnterScreen;
        ScreenExited += OnExitScreen;
    }

    private void SetAABB()
    {
        Aabb = AABBSource.GetAabb();
    }


    private void OnExitScreen()
    {
        isVisible = false;
    }


    private void OnEnterScreen()
    {
        isVisible = true;
    }


 
}
