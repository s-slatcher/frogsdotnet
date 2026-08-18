using Godot;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;

public partial class HalfEdge : GodotObject 
{
    
    public PolyVertex StartVert = new();
    public PolyVertex EndVert = new();
    public HalfEdge NextEdge; 
    public HalfEdge PrevEdge;
    public Face Face;

    public HalfEdge TwinEdge;  // same edge line in opposite direction; attached to an adjacent face. 
    public HalfEdge ParentEdge;
    public HalfEdge childEdge1;
    public HalfEdge childEdge2;

    public HalfEdge(){}
    public HalfEdge(PolyVertex start, PolyVertex end)
    {
        StartVert = start;
        EndVert = end;
    }

    public HalfEdge SetTwinEdge()
    {
        TwinEdge = new HalfEdge(EndVert, StartVert){TwinEdge = this};
        return TwinEdge;    
    }

    public bool HasChildren() => childEdge1 != null;
    public bool HasTwin() => TwinEdge != null;
    

    public void GenerateTwinEdge()
    {
        TwinEdge = new HalfEdge(EndVert, StartVert){TwinEdge = this};
    }
    
    
    // pass the intersecting half edge, 
    // whose end vert is pointing at the edge, from inside the face.
    // sets up edge connections for both twin edges. 
    // TODO: could let this function check which of the twin edges is primary
    public void ReRoute(HalfEdge hEdge)
    {
        if (hEdge.StartVert != EndVert)
        {
            GD.Print("Reroute edge passed not valid next-edge");
            return;
        }
        hEdge.TwinEdge.SetNextEdge(NextEdge);
        SetNextEdge(hEdge);
        
    }

    public void SplitEdge(PolyVertex splitVert, bool maintainValidFace)
    {
        childEdge1 = new HalfEdge(){StartVert = StartVert, EndVert = splitVert, Face = Face };
        childEdge2 = new HalfEdge(){StartVert = splitVert, EndVert = EndVert, Face = Face};

        PrevEdge.SetNextEdge(childEdge1);
        childEdge2.SetNextEdge(NextEdge);
        childEdge1.SetNextEdge(childEdge2);

        // always maintain face on twin edge call
        bool doSplitTwinEdge = TwinEdge != null && TwinEdge.childEdge1 == null;
        if (doSplitTwinEdge)
        {
            TwinEdge.SplitEdge(splitVert, true); 
            childEdge1.TwinEdge = TwinEdge.childEdge2;
            childEdge2.TwinEdge = TwinEdge.childEdge1;

            TwinEdge.childEdge1.TwinEdge = childEdge2;
            TwinEdge.childEdge2.TwinEdge = childEdge1;

        }

        if (maintainValidFace) 
        {
            int idx = Face.EdgeList.IndexOf(this);
            Face.EdgeList.RemoveAt(idx);
            Face.EdgeList.InsertRange(idx, [childEdge1, childEdge2]);
        }
    }



    public void SetNextEdge(HalfEdge edge)
    {
        NextEdge = edge;
        edge.PrevEdge = this;
    }

    

}
