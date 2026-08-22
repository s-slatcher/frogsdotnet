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
    
    
    // subdivided faces are passed this hashset to properly map faces together by edge connection
    public List<HalfEdge> EdgeList = new();
    public bool IsValid = false;


    public Rect2 BoundingRect; 

    public void AddEdge(HalfEdge hEdge)
    {
        if (IsValid)
        {
           GD.Print("Tried adding edge to already enclosed face");
           return;
        }
        if (EdgeList.Count == 0) 
        {
            EdgeList.Add(hEdge);
        }
        else
        {
            var last = EdgeList[^1];
            var first = EdgeList[0];
            if (hEdge.StartVert != last.EndVert)    
            {
                GD.Print("tried adding unlinked edge to poly");
                return;
            }
            else
            {
                EdgeList.Add(hEdge);
                hEdge.PrevEdge = last;
                last.NextEdge = hEdge;
            }

            if (hEdge.EndVert == first.StartVert)
            {
                IsValid = true;
                first.PrevEdge = hEdge;
                hEdge.NextEdge = first;
            }
        }
        hEdge.Face = this;


        
        
    }

    // public List<(PolyVertex, Vector2)> GetPoint2DPair()
    // {
    //     var list2D = new List<(PolyVertex, Vector2)>();
    //     foreach (var hEdge in EdgeList)
    //     {
    //         var vert = hEdge.StartVert;
    //         list2D.Add((vert, VertToLocalSpace(vert)));
    //     }
    //     return list2D;
    // }

    // public Vector2 VertToLocalSpace(PolyVertex vert)
    // {
    //     var rotatedVert = vert.Position * Transform.Inverse();
    //     return new Vector2(rotatedVert.X, rotatedVert.Y);
    // }
    // public Vector3 PointToMeshSpace(Vector2 point)
    // {
    //     var vert3D = new Vector3(point.X, point.Y, 0);
    //     return vert3D * Transform;
    // }

    public List<PolyVertex> GetVerts()
    {
        return EdgeList.Select(edge => edge.StartVert).ToList();
    }




}





