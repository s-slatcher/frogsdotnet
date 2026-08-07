using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PolygonToolTest : Node3D
{
    // [Export] MeshInstance3D meshNode;
    [Export] Polygon2D polyNode;
    [Export] PackedScene polyVisualizer; 
    [Export] Button cutButton;
    [Export] Path3D cutVisualizer;


    
    public struct QueuedCut()
    {
        public Face face;
        public Vector2 cutDirection;
        public Vector2 cutPoint;

    }

    public List<Face> faces;

    public Dictionary<Face, PolyFaceVisualized> faceVisualizerMap = new();
    public List<QueuedCut> queuedCutsList = new();
    public List<(Vector2,Vector2)> cuts = new();
    public int queuePos = 0;

    public PolygonMeshTool polyMeshTool;
    public override void _Ready()
    {
        cutButton.Pressed += OnNextCutButton;
        polyMeshTool = new PolygonMeshTool();
        
        var polygon = new NormalPoly(polyNode.Polygon, 0);

        polyMeshTool.SetPolygon(polygon);

        faces = polyMeshTool.GetFaces();

        // VisualizeMesh();
        InitializeMeshVisual();

        cuts.Add((new Vector2(0,25), new Vector2(0.5f,-1f)));

        cuts.Add( (new Vector2(0,20), Vector2.Right) );
        cuts.Add((new Vector2(0,0), new Vector2(1,1)));

        
        // var mesh = polyMeshTool.GetMesh();

        // meshNode.Mesh = mesh;


    }

    private void OnNextCutButton()
    {
        if (queuePos >= queuedCutsList.Count)
        {
            var cutsCount = cuts.Count;
            if (cutsCount == 0)
            {
                GD.Print("end of cuts reached");
                PolyEdgeVisualized edgeVisualizer = new();
                edgeVisualizer.SetEdges(faceVisualizerMap.Keys.AsEnumerable().ElementAt(0));
                return;
            }

            else
            {
                var nextCut = cuts[^1];
                cuts.RemoveAt(cuts.Count-1);
                AddCutToQueue(nextCut.Item1, nextCut.Item2);
                queuePos = 0;
            
            }
        }

        
        
        var cut = queuedCutsList[queuePos];
        queuePos++;
        DivideFaceAndUpdateVisuals(cut.face, cut.cutDirection, cut.cutPoint);
        
    }


    private void AddCutToQueue(Vector2 clipPoint, Vector2 clipDirection)
    {
        queuedCutsList = new();
        foreach (var face in faceVisualizerMap.Keys)
        {
            var cut = new QueuedCut()
            {
                cutPoint = clipPoint,
                cutDirection = clipDirection,
                face = face

            };
            queuedCutsList.Add(cut);
        }
        
    }


    public void InitializeMeshVisual()
    {
        foreach (var face in faces)
        {
            VisualizeFace(face);
        }
    
    }

    public void VisualizeFace(Face face)
    {
        var container = GetNode<Node3D>("MeshContainer");
        var vis = (PolyFaceVisualized)polyVisualizer.Instantiate();
        container.AddChild(vis);
        vis.SetFace(face); 
        faceVisualizerMap[face] = vis;

    }


    public void DivideFaceAndUpdateVisuals(Face face, Vector2 clipDir, Vector2 clipPoint)
    {
        var newFaces = polyMeshTool.DivideFace(face, clipDir, clipPoint);
        if (newFaces.Count == 1)
        {
            OnNextCutButton();
            return;
        }
        var oldVis = faceVisualizerMap[face];
        
        faceVisualizerMap.Remove(face);
        oldVis.QueueFree();

        foreach (var newFace in newFaces) VisualizeFace(newFace);

        // cutVisualizer.Curve.RemovePoint(1);
        // cutVisualizer.Curve.RemovePoint(0);
        // cutVisualizer.Curve.AddPoint()
    }

}
