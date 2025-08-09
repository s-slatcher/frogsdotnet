using Godot;
using System;

public partial class PlaneMouseCapture : Node
{
    [Export] Camera3D camera;

    public Action<Vector3> PlaneClicked;

    public Plane dropPlane = new(Vector3.Back, 0);

    public Vector3 LastMousePos = new Vector3();



    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseMotion eventMouseMotion)
        {
            var mousePos = eventMouseMotion.Position;
            var pos3D = dropPlane.IntersectsRay(camera.ProjectRayOrigin(mousePos), camera.ProjectLocalRayNormal(mousePos));

            LastMousePos = pos3D ?? Vector3.Zero;
        }
        if (Input.IsActionJustPressed("click"))
        {
            if (LastMousePos == Vector3.Zero) return;
            PlaneClicked.Invoke(LastMousePos);
        }
    }

}
