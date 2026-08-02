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

    public Basis Basis = Basis.Identity;

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
            hEdge.Face = this;
        }

        
        
    }

    public List<(PolyVertex, Vector2)> GetPoint2DPair()
    {
        var list2D = new List<(PolyVertex, Vector2)>();
        foreach (var hEdge in EdgeList)
        {
            var vert = hEdge.StartVert;
            list2D.Add((vert, ConvertPointToLocal2D(vert)));
        }
        return list2D;
    }

    public Vector2 ConvertPointToLocal2D(PolyVertex vert)
    {
        var rotatedVert = vert.Position * Basis.Inverse();
        return new Vector2(rotatedVert.X, rotatedVert.Y);
    }
    public Vector3 ConvertToEdgeSpace(Vector2 point)
    {
        var vert3D = new Vector3(point.X, point.Y, 0);
        return vert3D * Basis;
    }

    public void DivideFace(Vector2 clipPoint, Vector2 clipDir)
    {
        // collect intersections
        var hits = new List<(HalfEdge, Vector2)>();
        for (int i = 0; i < EdgeList.Count; i++)
        {
            var hEdge = EdgeList[i];
            var p1 = ConvertPointToLocal2D(hEdge.StartVert);
            var p2 = ConvertPointToLocal2D(hEdge.EndVert);
            var edgeRect = new Rect2(){Position = p1, End = p2}.Abs();

            var intersect = (Vector2)Geometry2D.LineIntersectsLine(clipPoint, clipDir, p1, p2-p1.Normalized());
            
            if (edgeRect.HasPoint(intersect)) hits.Add((hEdge, intersect));
        }

        // sort hits by x or y axis to find new clip edges
        if (hits.Count < 2) return;
        
        if (Math.Abs(clipDir.X) > 0.01) 
        {
            hits = hits.OrderBy(hit => hit.Item2.X).ToList();
        }
        else hits = hits.OrderBy(hit => hit.Item2.Y).ToList();

        // split into groups of two edges and repsective intersect points
        // foreach(var hitChunk in hits.Chunk(2))
        // {
        //     HandleIntersect
        //     (
        //         hitChunk[0].Item2, hitChunk[0].Item1, hitChunk[1].Item2, hitChunk[1].Item1
        //     );
        // }
        // ;

    }

    private void HandleIntersect(Vector2 intersect1, HalfEdge edge1, Vector2 intersect2, HalfEdge edge2)
    {
        // produce vertices, and both half-edges for the clip boundary
        var vert1 = new PolyVertex(){Position = ConvertToEdgeSpace(intersect1)};
        var vert2 = new PolyVertex(){Position = ConvertToEdgeSpace(intersect2)};

        var hEdge1 = new HalfEdge()
        {
            StartVert = vert1,
            EndVert = vert2
        };
        var hEdge2 = new HalfEdge()
        {
            StartVert = vert2,
            EndVert = vert1,
        };
        hEdge1.TwinEdge = hEdge2;
        hEdge2.TwinEdge = hEdge1;

        // use same vertices to split edges of existing lines
        (HalfEdge, HalfEdge) splitEdges1 = edge1.SplitEdge(vert1);



    }

    


    public IEnumerable<PolyVertex[]> TriangulateEdge()
    {
        var vertList = new List<PolyVertex>();
        var triSource = new List<Vector2>();
        var triList = new List<(PolyVertex, PolyVertex, PolyVertex)>();
        
        // convert ordered half-edge list into list of vertices
        // convert to local face plane to elimate Z axis
        // triangulate in 2D, but return triangulated list in original 3D, in form of sets of 3 vertices
        foreach (var hEdge in EdgeList)
        {
            var vert = hEdge.StartVert;
            vertList.Add(vert);
            
            // TODO: correct with full transform and origin later
            var rotatedVert = vert.Position * Basis.Inverse();
            
            // rotated vert should have zeroed Z by default

            Debug.Assert((rotatedVert.Z < 0.01 && rotatedVert.Z > -0.01));
           
            triSource.Add(new Vector2(rotatedVert.X, rotatedVert.Y));

        }

        var triIndices = Geometry2D.TriangulatePolygon(triSource.ToArray());
        return triIndices.Select( i => vertList[i]).Chunk(3);
    }

    public Vector3 GetNormal()
    {
        return Basis.Z;
    }


}




