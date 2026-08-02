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


    public (HalfEdge, HalfEdge) SplitEdge(PolyVertex splittingVert)
    {
        bool doSplitTwinEdge = TwinEdge != null && TwinEdge.childEdge1 == null;

        childEdge1 = new HalfEdge(){StartVert = StartVert, EndVert = splittingVert };

       
        return new (); 
     

        
    }



}
