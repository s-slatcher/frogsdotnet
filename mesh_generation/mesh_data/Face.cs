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

    public Transform3D Transform = Transform3D.Identity;

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

    public List<(PolyVertex, Vector2)> GetPoint2DPair()
    {
        var list2D = new List<(PolyVertex, Vector2)>();
        foreach (var hEdge in EdgeList)
        {
            var vert = hEdge.StartVert;
            list2D.Add((vert, VertToLocalSpace(vert)));
        }
        return list2D;
    }

    public Vector2 VertToLocalSpace(PolyVertex vert)
    {
        var rotatedVert = vert.Position * Transform.Inverse();
        return new Vector2(rotatedVert.X, rotatedVert.Y);
    }
    public Vector3 PointToMeshSpace(Vector2 point)
    {
        var vert3D = new Vector3(point.X, point.Y, 0);
        return vert3D * Transform;
    }


    public List<Face> DivideFace(Vector2 clipPoint, Vector2 clipDir)
    {

        // collect intersections in dictionary mapping vert to edge it is the END vert
        // prevent corner intersections from double hitting
        var hitsMap = new Dictionary<PolyVertex, HalfEdge>();

        var Epsilon = 0.0001;
        for (int i = 0; i < EdgeList.Count; i++)
        {
            var hEdge = EdgeList[i];
            var p1 = VertToLocalSpace(hEdge.StartVert);
            var p2 = VertToLocalSpace(hEdge.EndVert);
            var edgeDist = p1.DistanceSquaredTo(p2);
            
            float dotProd = (p2-p1).Normalized().Dot(clipDir.Normalized());
            bool isParallel = Math.Abs(dotProd) > 0.99f;
            if (isParallel)
            {
                GD.Print("skipping parallel line");
                continue;
            }
            
            var inter = (Vector2)Geometry2D.LineIntersectsLine(clipPoint, clipDir, p1, p2-p1);

            var dist1 = inter.DistanceSquaredTo(p1);
            var dist2 = inter.DistanceSquaredTo(p2);
            
            if (dist1 > edgeDist || dist2 > edgeDist ) continue;
            
            if (dist1 < Epsilon) 
            {
                if (hitsMap.ContainsKey(hEdge.StartVert)) continue;
                GD.Print("snapped point");
                hitsMap.Add(hEdge.StartVert, hEdge.PrevEdge);
            }
            else if (dist2 < Epsilon)
            {
                if (hitsMap.ContainsKey(hEdge.EndVert)) continue;
                GD.Print("snapped point");
               hitsMap.Add(hEdge.EndVert, hEdge); 
            } 
            else
            {
                var vert = new PolyVertex(){Position = PointToMeshSpace(inter)};
                hEdge.SplitEdge(vert, true);
                hitsMap.Add(vert, hEdge.childEdge1);
                
            }

            
            
        }
             if (hitsMap.Count % 2 != 0) GD.Print("odd numbered interescts");

        

        if (hitsMap.Count < 2) return [this];
        // sort intersect vertices by projected x or y axis to find new clip edges


        bool isXSortAble = Math.Abs(clipDir.X) > 0.01;
        var orderedIntersects = hitsMap.Keys.OrderBy(VertOrderBy);
        float VertOrderBy(PolyVertex vert)
        {
            var pos2D = VertToLocalSpace(vert);
            return isXSortAble ? pos2D.X : pos2D.Y;
        }
       
    
        // split into groups of two edges and reroute respective edges to new clipped edges
        HashSet<HalfEdge> unhandledEdges = new();
        foreach(var vertSet in orderedIntersects.Chunk(2))
        {
        
            
            var vert1 = vertSet[0];
            var vert2 = vertSet[1];
            
            var hEdge = new HalfEdge(vert1, vert2);
            hEdge.SetTwinEdge();

            unhandledEdges.Add(hEdge);
            unhandledEdges.Add(hEdge.TwinEdge);

            // for edge to intersect its END vert (2nd arg) is on that other edge
            
            hitsMap[vert1].ReRoute(hEdge);
            hitsMap[vert2].ReRoute(hEdge.TwinEdge);

        }

        // generate new faces by looping each new clip edge
        List<Face> newFaces = new();

        foreach(var clipEdge in unhandledEdges.ToList()) 
        {
              
            if (clipEdge.Face != null) continue;
            
            var face = new Face(){Transform = Transform};
            var curEdge = clipEdge;
            do
            {
                face.AddEdge(curEdge);
                curEdge.Face = face;
                curEdge = curEdge.NextEdge;
            } while (curEdge != clipEdge);
        
            newFaces.Add(face);
        }
 
        return newFaces;

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
            var rotatedVert = vert.Position * Transform.Inverse();
            
            // rotated vert should have zeroed Z by default

            Debug.Assert((rotatedVert.Z < 0.01 && rotatedVert.Z > -0.01));
           
            triSource.Add(new Vector2(rotatedVert.X, rotatedVert.Y));

        }

        var triIndices = Geometry2D.TriangulatePolygon(triSource.ToArray());
        return triIndices.Select( i => vertList[i]).Chunk(3);
    }

    public Vector3 GetNormal()
    {
        return Transform.Basis.Z;
    }


}





