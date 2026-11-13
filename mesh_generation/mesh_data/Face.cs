using Godot;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;


public partial class Face : GodotObject 
{
    
    
    public List<HalfEdge> EdgeList = new();
    public bool IsValid = false;
    
    public Vector3 WeightedCenter = new Vector3();
    public float Area = -1f;
    public Vector3 Normal = Vector3.Zero;

    public Rect2 BoundingRect; 

    public static Face FaceFromInitialEdge(HalfEdge edge)
    {
        var face = new Face();
        face.EdgeList.Add(edge);

        return face;
    }
    // public void AddEdge(HalfEdge hEdge)
    // {
    //     if (IsValid)
    //     {
    //        GD.Print("Tried adding edge to already enclosed face");
    //        return;
    //     }
    //     if (EdgeList.Count == 0) 
    //     {
    //         EdgeList.Add(hEdge);
    //     }
    //     else
    //     {
    //         var last = EdgeList[^1];
    //         var first = EdgeList[0];
    //         if (hEdge.StartVert != last.EndVert)    
    //         {
    //             GD.Print("tried adding unlinked edge to poly");
    //             return;
    //         }
    //         else
    //         {
    //             EdgeList.Add(hEdge);
    //             hEdge.PrevEdge = last;
    //             last.NextEdge = hEdge;
    //         }

    //         if (hEdge.EndVert == first.StartVert)
    //         {
    //             IsValid = true;
    //             first.PrevEdge = hEdge;
    //             hEdge.NextEdge = first;
    //         }
    //     }
    //     hEdge.Face = this;
    // }

    public void AddEdgeToEnd(HalfEdge edge)
    {
        EdgeList.Add(edge);
        edge.Face = this;
        

        if (EdgeList.Count > 1) 
        {
            var last = EdgeList[^2];
            Debug.Assert(last.NextEdge == edge);
            UpdateValidity();
        }

    }
    public void AddEdgeToStart(HalfEdge edge)
    {
        EdgeList.Insert(0, edge);
        edge.Face = this;

        if (EdgeList.Count > 1) 
        {
            var second = EdgeList[1];
            Debug.Assert(second.PrevEdge == edge);
            UpdateValidity();
        }
    }

    private void UpdateValidity()
    {
        Debug.Assert(!IsValid, "tried adding edges to already valid face");
        var startVert = EdgeList[0].StartVert;
        var endVert = EdgeList[^1].EndVert;

        if (startVert == endVert)
        {
            Debug.Assert(EdgeList.Count > 2, "tried completing face loop with just 2 edges");
            IsValid = true;
            SetAreaAndCenter();
            Normal = GeometryUtils.GetTriNormal(
                [
                    EdgeList[0].StartVert.Position,
                    EdgeList[0].EndVert.Position,
                    EdgeList[1].EndVert.Position
                ]
            );
        }

    }

    

    private void SetAreaAndCenter()
    {
        var areaAndCenter = GeometryUtils.AreaAndCenterOf3DPolyFace(GetVertPositions());
        Area = areaAndCenter.Item1;
        WeightedCenter = areaAndCenter.Item2;
    }


    public List<HalfEdge> GetEdges()
    {
        return EdgeList.ToList();
    }
    public List<PolyVertex> GetVerts()
    {
        return EdgeList.Select(edge => edge.StartVert).ToList();
        
    }

    public List<Vector3> GetVertPositions()
    {
        return EdgeList.Select(edge => edge.StartVert.Position).ToList();

    }
    



    
    




}





