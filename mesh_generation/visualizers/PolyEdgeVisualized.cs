using Godot;
using System;
using System.Collections.Generic;

public partial class PolyEdgeVisualized : Node2D
{
    /// <summary>
    /// should be able to use one edge to traverse all faces
    /// </summary>
    public void SetEdges(Face initFace)
    {
        HashSet<Face> DiscoveredFaces = new();
        
        HashSet<HalfEdge> DiscoveredEdges = new();

        var faceQueue = new List<Face>(){initFace};
        int queuePos = 0;
        while (queuePos < faceQueue.Count)
        {
            
            var face = faceQueue[queuePos];
            if (face == null) GD.Print("null face at queue: ",queuePos);    
            queuePos++;
            foreach (var edge in face.EdgeList)
            {   
                DiscoveredEdges.Add(edge);
                if (edge.TwinEdge != null)
                {
                    var adjFace = edge.TwinEdge.Face; // a twin edge should always have a face assigned;
                    bool isAdded = DiscoveredFaces.Add(adjFace);
                    if (isAdded) faceQueue.Add(adjFace);
                }
            }
        }

        GD.PrintS("Discovered faces:",DiscoveredFaces.Count,"Discovered edges:",DiscoveredEdges.Count);
        

        
    }
}
