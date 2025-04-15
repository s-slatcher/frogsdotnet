using Godot;
using GodotPlugins.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

public partial class UserPolygonDraw : Node3D
{

    [Export] public Camera3D MainCamera;
    [Export] public MeshInstance3D PointPreview;
    [Export] public MeshInstance3D PolyContainer;
    [Export] public Button FinishButton;
    [Export] public Button ClearButton;
    [Export] public PackedScene RigidBodyDrawing;


    private bool activeDrawing = false;
    private List<Vector3> currentPointList = new();
    private List<MeshInstance3D> pointPreviews = new();
    GeometryUtils gu = new();


    public override void _Ready()
    {
        FinishButton.Pressed += FinishDrawing;
        ClearButton.Pressed += ClearDrawing;
    }

    private void FinishDrawing()
    {
        var time = Time.GetTicksMsec();

        if (currentPointList.Count < 3){
            GD.Print("not enough points");
            return;
        }
        var points2DList = new List<Vector2>();
        foreach (var point in currentPointList){
            points2DList.Add(new Vector2(point.X, point.Y));
        }
        var points2DArray = points2DList.ToArray();
        if (Geometry2D.TriangulatePolygon(points2DArray).Length == 0)
        {
            GD.Print("invalid shape");
            return;
        }
        
        var smoothCurve = gu.PointsToCurve(points2DArray, 0.5f, true);
        var smoothPoly = smoothCurve.Tessellate().ToArray();


        var extrudedMesh = new ExtrudedMesh(smoothPoly, 0.125f, 0.5f, 2f);
       
        var mesh = extrudedMesh.GetMesh();
        
        var meshInst = PolyContainer.Duplicate() as MeshInstance3D;
        meshInst.Mesh = mesh;
        meshInst.Visible = true;

        var rigidBody = RigidBodyDrawing.Instantiate() as PhysicsDrawing;
        rigidBody.MeshInstance = meshInst;
        AddChild(rigidBody);


        GD.Print("Time gen mesh: " + (Time.GetTicksMsec() - time));
        ClearDrawing();

    }

    private void ClearDrawing()
    {
        currentPointList = new();
        foreach (var mesh in pointPreviews) mesh.QueueFree();
        pointPreviews = new();

    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed("click"))
        {   
            var pos3D = GetClickPosition(GetViewport().GetMousePosition());
            if (pos3D != null) AddDrawPoint((Vector3) pos3D);
        }
    }

    private void AddDrawPoint(Vector3 pointPos)
    {
        if (currentPointList.Contains(pointPos)) return;
        currentPointList.Add(pointPos); 
        AddPointPreview(pointPos);

    }
    
    

    private void AddPointPreview( Vector3 pointPos )
    {
        var newPoint = (MeshInstance3D)PointPreview.Duplicate();
        newPoint.Visible = true;
        newPoint.Position = pointPos;
        pointPreviews.Add(newPoint);
        AddChild(newPoint);
    }


    public Vector3? GetClickPosition(Vector2 clickPosition)
    {
        var dropPlane = new Plane(Vector3.Back, 0);
        var pos3D = dropPlane.IntersectsRay(MainCamera.ProjectRayOrigin(clickPosition), MainCamera.ProjectLocalRayNormal(clickPosition));
        return pos3D;
    }

}
