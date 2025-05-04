using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class PolygonQuadMesh : GodotObject
{

    public PolygonQuad RootQuad;
    public Dictionary<PolygonQuad, PolygonQuad> AlteredQuadBranchMap; // altered branches keyed to the branch it will replace when consolidated
    
    public Dictionary<Vector2, IndexedVertex> Vector2ToVertexMap;
    public List<IndexedVertex> VertexList;



    public PolygonQuadMesh(Vector2[] polygon, float minimumQuadWidth = 0.25f)
    {
        RootQuad = PolygonQuad.CreateRootQuad(polygon, minimumQuadWidth);
        AlteredQuadBranchMap = new();
        Vector2ToVertexMap = new();
        VertexList = new();
    }

    public PolygonQuadMesh(PolygonQuadMesh polyMesh)
    {
        RootQuad = polyMesh.RootQuad;
        AlteredQuadBranchMap = new(polyMesh.AlteredQuadBranchMap);
        Vector2ToVertexMap = new(polyMesh.Vector2ToVertexMap);
        VertexList = new(polyMesh.VertexList);
    }

    public void Subdivide(PolygonQuad quad)
    {
        bool isBranch = AlteredQuadBranchMap.ContainsKey(quad);
        if (!isBranch && quad.Root != RootQuad) return;

        if (isBranch)
        {
            quad.Subdivide();
        }
        else
        {
            // var branchDuplicate = new PolygonQuad()
        }


    }


}
