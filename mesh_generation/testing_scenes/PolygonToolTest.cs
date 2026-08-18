using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.WebSockets;

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

        polyMeshTool.SetPolygon(polygon, 5);


        // VisualizeMesh();

        polyMeshTool.SubdivideAll(2);

        InitializeMeshVisual();


        // cuts.Add((new Vector2(0,25), new Vector2(0.5f,-1f)));

        // cuts.Add( (new Vector2(0,20), Vector2.Right) );
        // cuts.Add((new Vector2(0,0), new Vector2(1,1)));
        // cuts.Add((new Vector2(15,0), new Vector2(-0.2f,1)));
        // cuts.Add((new Vector2(15,0), new Vector2(+0.2f,1)));

    }

    private void OnNextCutButton()
    {
        if (queuePos >= queuedCutsList.Count)
        {
            var cutsCount = cuts.Count;
            if (cutsCount == 0)
            {
                GD.Print("end of cuts reached");
                ReconstructOriginalEdge();

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
        foreach (var face in polyMeshTool.GetFaces())
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
    private void UpdateVisualization(List<Face> newFaces)
    {
        var container = GetNode<Node3D>("MeshContainer");
        foreach (var child in container.GetChildren()) child.QueueFree();
        foreach (var face in newFaces) faceVisualizerMap.Add(face, null);
        foreach (var face in faceVisualizerMap.Keys) VisualizeFace(face);
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

        UpdateVisualization( newFaces);

        // foreach (var newFace in newFaces) VisualizeFace(newFace);

        // cutVisualizer.Curve.RemovePoint(1);
        // cutVisualizer.Curve.RemovePoint(0);
        // cutVisualizer.Curve.AddPoint()
    }

    


    public List<HalfEdge> ReconstructOriginalEdge()
    {
        var edge = polyMeshTool.OriginalEdgeSample;

        var outerEdgeList = new List<HalfEdge>();

        // loop edge until edge with children found
        var cur = edge;
        var isDescending = true;
        do
        {
            if (cur.TwinEdge != null)
            {
                cur = cur.TwinEdge.NextEdge;
                continue;
            }

            if (cur.childEdge1 != null)
            {
                cur = cur.childEdge1;
                continue;
            }
            
            outerEdgeList.Add(cur);
            cur = cur.NextEdge;
            
                
        } while (outerEdgeList.Count == 0 || outerEdgeList[0] != cur);

        return outerEdgeList;
       
    }


}
