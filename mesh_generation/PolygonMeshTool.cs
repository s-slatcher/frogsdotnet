using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;
using Microsoft.VisualBasic;
using System.Security.Cryptography;

public partial class PolygonMeshTool : GodotObject
{
    List<Face> faces = new();
    Dictionary<Vector2, PolyVertex> vertexMap = new();

    public HalfEdge OriginalEdgeSample;

    static Vector2 RoundVector2(Vector2 vec)
    {
        return new Vector2(float.Floor(vec.X * 1000), float.Floor(vec.Y * 1000)) / 1000;
    }

    struct UnhandledIntersection(PolyVertex vert, HalfEdge halfEdge, bool DoSplitEdge)
    {
        public PolyVertex vertex = vert;
        public HalfEdge halfEdge = halfEdge;
        public bool DoSplitEdge = DoSplitEdge;
    }

    public void SetPolygon (NormalPoly normPoly)
    {
        var concavePolygon = normPoly.Polygon;
        var convexPolys = Geometry2D.DecomposePolygonInConvex(concavePolygon);

        Dictionary<Vector2, PolyVertex> vertMap = new();
        Dictionary<(PolyVertex, PolyVertex), HalfEdge> edgeMap = new();

        var transform = Transform3D.Identity;
        // transform.Origin = new Vector3(20,20,0);
        // transform.Basis = new Basis(Vector3.Right, float.Pi/2);

        foreach (var poly in convexPolys)
        {
            var face = new Face(){Transform = transform};

            for (int i = 0; i < poly.Length; i++)
            {
                var curP = poly[i];
                var nextP = i == poly.Length-1 ? poly[0] : poly[i+1];

                PolyVertex curVert = GetPointVertex(curP, transform);
                PolyVertex nextVert = GetPointVertex(nextP, transform);

                // generate edge, looking for twin edge matches
                var hEdge = new HalfEdge(curVert, nextVert);
                
                edgeMap[(curVert, nextVert)] = hEdge;

                if ( edgeMap.TryGetValue((nextVert,curVert), out HalfEdge twinEdge) )
                {
                    PairTwinEdges(hEdge, twinEdge);
                }
                
                face.AddEdge(hEdge);    
            }
            faces.Add(face);
        } 
        OriginalEdgeSample = edgeMap.Values.ToList()[0];
        
    }

    

    private List<UnhandledIntersection> GenerateIntersections(Face face, Vector2 clipLineDir, Vector2 clipLinePoint)
    {
        var ui_list = new List<UnhandledIntersection>();
        // var hitsList = new List<(PolyVertex, HalfEdge)>();
        var f = face;
        var c_dir = clipLineDir;
        var c_p = clipLinePoint;
        
        GD.Print("  ");
        GD.Print("Start of face intersection");

        for (int i = 0; i < f.EdgeList.Count; i++)
        {
            
            var hEdge = f.EdgeList[i];
            LineSegment edge2D = HalfEdgeAs2DLine(hEdge); 
            // GD.PrintS("next Edge: ",edge2D.Start, "->",edge2D.End);
            var snapEpsilon = 0.01f;       
            
            Vector2? maybeIntersect = GetLineIntersectOrNull(c_p, c_dir, edge2D.Start, edge2D.DirectionVector().Normalized());
            if (maybeIntersect == null)
            {
                // GD.PrintS("parallel lines: ", edge2D.DirectionVector(), clipLineDir);
                continue;
            }


            Vector2 intersect = (Vector2)maybeIntersect;

            var dist1 = intersect.DistanceTo(edge2D.Start);
            var dist2 = intersect.DistanceTo(edge2D.End);
            var edgeDist = edge2D.Length();
            
            if (dist2 < snapEpsilon) 
            {
                ui_list.Add(new UnhandledIntersection(hEdge.EndVert, hEdge, false));
                // GD.Print("  snap intersect at: ",intersect);
            }
            else if ( dist1 > snapEpsilon && dist2 > snapEpsilon)
            {
                if (dist1 < edgeDist && dist2 < edgeDist)
                {
                    var newVert = new PolyVertex(){Position = f.PointToMeshSpace(intersect)};
                    ui_list.Add(new UnhandledIntersection(newVert, hEdge, true));
                    // GD.Print("  middle intersect at: ",intersect," ");
                }
                
            }
            else
            {
                // GD.Print("intersect discarded at: ", intersect);
                continue;
            }

        }
        
        return ui_list;
        
    }
    
    /// <summary>
    /// Splits face with an infinite length line; ASSUMES FACE IS CONVEX POLYGON
    /// </summary>
    /// 
    
    
    public List<Face> DivideFace(Face face, Vector2 clipLineDir, Vector2 clipLinePoint)
    {
        
        var hits = GenerateIntersections(face, clipLineDir, clipLinePoint);
        
        if (hits.Count != 2) return [face];
        
        if (hits[0].vertex.Position == hits[1].vertex.Position) GD.Print("SAME VERT INTERSECT");
    
        // generate twin edges for clip boundary
        var newEdge = new HalfEdge(hits[0].vertex, hits[1].vertex);
        newEdge.GenerateTwinEdge();
        
        foreach (var hit in hits)
        {
            var edgeToRoute = hit.halfEdge;

            // splits the edge, and its twin half edge
            if (hit.DoSplitEdge)
            {
                hit.halfEdge.SplitEdge(hit.vertex, false);
                edgeToRoute = hit.halfEdge.childEdge1;
            }

            var clipOut = newEdge;
            var clipIn = newEdge.TwinEdge;
            var splitIn = edgeToRoute;
            var splitOut = edgeToRoute.NextEdge;
            if (splitIn.EndVert != clipOut.StartVert) (clipOut, clipIn) = (clipIn, clipOut);

            splitIn.NextEdge = clipOut;
            clipOut.PrevEdge = splitIn;

            clipIn.NextEdge = splitOut;
            splitOut.PrevEdge = clipIn;

        }

        var newFaceA = GenerateFace(newEdge, face.Transform);
        var newFaceB = GenerateFace(newEdge.TwinEdge, face.Transform);
        
        return [newFaceA, newFaceB];
    }

    public Face GenerateFace(HalfEdge EdgeOnFace, Transform3D transform)
    {
            var face = new Face(){Transform = transform};
            var curEdge = EdgeOnFace;
            do
            {
                face.AddEdge(curEdge);
                curEdge.Face = face;
                curEdge = curEdge.NextEdge;
            } while (curEdge != EdgeOnFace);

            return face;
    }

    public Vector2? GetLineIntersectOrNull(Vector2 pA, Vector2 dirA, Vector2 pB, Vector2 dirB)
    {
        float dotProd = (dirA.Normalized()).Dot(dirB.Normalized());
        bool isParallelApprox = Math.Abs(dotProd) > 0.999f;
        if (isParallelApprox) GD.Print("paralel dot prod: ",dotProd);
        if (isParallelApprox) return null;
        return (Vector2)Geometry2D.LineIntersectsLine(pA, dirA, pB, dirB);

    }

    public LineSegment HalfEdgeAs2DLine(HalfEdge hEdge)
    {
        var face = hEdge.Face;
        var p1 = face.VertToLocalSpace(hEdge.StartVert);
        var p2 = face.VertToLocalSpace(hEdge.EndVert);
        return new LineSegment(p1, p2);
    }

    public void PairTwinEdges(HalfEdge a, HalfEdge b)
    {
        a.TwinEdge = b;
        b.TwinEdge = a;
    }

    public void DivideFaces(Vector2 clipPoint, Vector2 clipdirection)
    {

        var newFaces = faces.SelectMany(f => DivideFace(f, clipdirection, clipPoint));
        
        faces = newFaces.ToList();
    }

    public List<Face> GetFaces()
    {
        return faces;
    }


    public PolyVertex GetPointVertex(Vector2 point, Transform3D? transform = null)
    {
        var roundVec = RoundVector2(point);

        if (vertexMap.TryGetValue(roundVec, out PolyVertex vert)) return vert;
        else
        {
            Vector3 pos = new(point.X, point.Y, 0);
            vert = new PolyVertex()
            {
                Position = pos * (transform == null ? Transform3D.Identity : (Transform3D)transform )
            };
            vertexMap.Add(roundVec, vert);
            return vert;
        }
        
    }

    public Mesh GetMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);


        foreach (var face in faces)
        {
            var randColor = new Godot.Color (GD.Randf(),GD.Randf(),GD.Randf());
            var tris = TriangulateFace(face);
            foreach (var vert in tris.SelectMany(vert => vert)) 
            {
                AddVertex(vert, face.GetNormal(), randColor, st, true);
            }
        }
        return st.Commit();
    }

    

    public void AddVertex(PolyVertex vert, Vector3 normal, Godot.Color vertexColor, SurfaceTool st,  bool PerFaceNormal = true)
    {
        st.SetNormal(normal);
        st.SetColor(vertexColor);
        st.AddVertex(vert.Position);
    }

    public List<PolyVertex[]> TriangulateFace(Face face)
    {

        return face.TriangulateEdge().ToList();
    }




}
