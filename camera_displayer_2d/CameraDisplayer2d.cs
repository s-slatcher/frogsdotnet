using Godot;
using System;

public partial class CameraDisplayer2d : Node3D
{
    [Export] Camera2D camera2d;
    [Export] MeshInstance3D displayMesh;
    
    Rect2 lastRect = new Rect2();

    public override void _PhysicsProcess(double delta)
    {
        if (camera2d == null) return;

        var viewportRect = GetScreenRect();

        if (viewportRect.Size != lastRect.Size || viewportRect.Position != lastRect.Position)
        {
            lastRect = viewportRect;
            UpdateMesh();
        }

    }

    private void UpdateMesh()
    {
        displayMesh.Position = new Vector3(lastRect.GetCenter().X, lastRect.GetCenter().Y, 0);
        var mesh = (PlaneMesh)displayMesh.Mesh;
        mesh.Size = lastRect.Size;
        
        GD.Print(mesh.Size);
    }


    private Rect2 GetScreenRect()
    {
        
        var cameraCenter = camera2d.GetScreenCenterPosition();
        var zoom = camera2d.Zoom;
        var screenSize = GetViewport().GetVisibleRect().Size;
        screenSize /= zoom;

        var rectPos = cameraCenter - (screenSize / 2);
        var rectEnd = cameraCenter + (screenSize / 2);

        var trueScreenRect = new Rect2();
        trueScreenRect.Position = rectPos;
        trueScreenRect.End = rectEnd;
        return trueScreenRect;
    }

}
