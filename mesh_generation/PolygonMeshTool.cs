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
    HashSet<Face> faces = new();
    Dictionary<Vector2, PolyVertex> vertexMap = new();

    public HalfEdge OriginalEdgeSample;
    
    private float extrudeLength = 5;

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

    public void SetPolygon (NormalPoly normPoly, float extrudeLength = 5)
    {
        this.extrudeLength = extrudeLength;
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
        if (extrudeLength != 0) ExtrudeEdges();
        
    }

    public void SubdivideAll(int levels = 1)
    {
        for (int i = 0; i < levels; i++)
        {
            List<Face> newFaces = new();
            foreach (var face in faces) newFaces.AddRange(Subdivide(face));
            GD.Print("subdivided from ", faces.Count, " to ", newFaces.Count);
            faces = [.. newFaces]; 
        }
        
    }

    

    private List<UnhandledIntersection> GenerateIntersections(Face face, Vector2 clipLineDir, Vector2 clipLinePoint)
    {
        var ui_list = new List<UnhandledIntersection>();
        // var hitsList = new List<(PolyVertex, HalfEdge)>();
        var f = face;
        var c_dir = clipLineDir;
        var c_p = clipLinePoint;
        
        // if (face.GetNormal().Z > 0.9) return [];

        for (int i = 0; i < f.EdgeList.Count; i++)
        {
            
            var hEdge = f.EdgeList[i];
            LineSegment edge2D = HalfEdgeAs2DLine(hEdge); 
            var snapEpsilon = 0.01f;       

            Vector2? maybeIntersect = GetLineIntersectOrNull(c_p, c_dir, edge2D.Start, edge2D.DirectionVector().Normalized());
            if (maybeIntersect == null)
            {
                continue;
            }

            Vector2 intersect = (Vector2)maybeIntersect;

            var dist1 = intersect.DistanceTo(edge2D.Start);
            var dist2 = intersect.DistanceTo(edge2D.End);
            var edgeDist = edge2D.Length();
            
            if (dist2 < snapEpsilon) 
            {
                ui_list.Add(new UnhandledIntersection(hEdge.EndVert, hEdge, false));
            }
            else if ( dist1 > snapEpsilon && dist2 > snapEpsilon)
            {
                if (dist1 < edgeDist && dist2 < edgeDist)
                {
                    var newVert = new PolyVertex(){Position = f.PointToMeshSpace(intersect)};
                    ui_list.Add(new UnhandledIntersection(newVert, hEdge, true));
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
    

    public PolyVertex SplitEdgeMiddle(HalfEdge hEdge)
    {
        
        // check first if this edge was split already by adj face subdivide
        if (hEdge.HasChildren()) return hEdge.childEdge1.EndVert;
        
        SplitHalfEdge(hEdge);
        if (hEdge.HasTwin()) 
        {
            SplitHalfEdge(hEdge.TwinEdge);

            hEdge.childEdge1.TwinEdge = hEdge.TwinEdge.childEdge2;
            hEdge.childEdge2.TwinEdge = hEdge.TwinEdge.childEdge1;

            hEdge.TwinEdge.childEdge1.TwinEdge = hEdge.childEdge2;
            hEdge.TwinEdge.childEdge2.TwinEdge = hEdge.childEdge1;
        }
        
        return hEdge.childEdge1.EndVert;
    }

    
    public void SplitHalfEdge(HalfEdge hEdge)
    {
        var sv = hEdge.StartVert;
        var ev = hEdge.EndVert;

        var midPos = (hEdge.EndVert.Position - hEdge.StartVert.Position) / 2 + hEdge.StartVert.Position;
        var mv = new PolyVertex(){Position = midPos};

        hEdge.childEdge1 = new HalfEdge(){StartVert = sv, EndVert = mv};
        hEdge.childEdge2 = new HalfEdge(){StartVert = mv, EndVert = ev};

        hEdge.PrevEdge.SetNextEdge(hEdge.childEdge1);
        hEdge.childEdge2.SetNextEdge(hEdge.NextEdge);
        hEdge.childEdge1.SetNextEdge(hEdge.childEdge2);
    }


    public List<Face> Subdivide(Face convexFace)
    {
        var newFaces = new List<Face>();
        var edges = convexFace.EdgeList;
        
        var centroidPos = new Vector3();
        var splitVertices = new List<PolyVertex>();

        // loop edges to find centroid vertex and generate all split vertices
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];  
            centroidPos += edge.StartVert.Position;
            splitVertices.Add(SplitEdgeMiddle(edge));
        }

        centroidPos /= edges.Count;
        var centerVert = new PolyVertex(){Position = centroidPos};

        HalfEdge firstCutOut = null;
        HalfEdge lastCutIn = null;
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            var childIn = edge.childEdge1;
            var childOut = edge.childEdge2;
            
            var cutIn = new HalfEdge(centerVert, childIn.EndVert);
            var cutOut = cutIn.SetTwinEdge();

            childIn.SetNextEdge(cutOut);
            cutIn.SetNextEdge(childOut);

           
            if (i == 0) firstCutOut = cutOut;
            if (lastCutIn != null) cutOut.SetNextEdge(lastCutIn);
            lastCutIn = cutIn;
            
        }
        firstCutOut.SetNextEdge(lastCutIn);
        
        foreach (var edge in edges)
        {
            newFaces.Add(GenerateFace(edge.childEdge1, Transform3D.Identity));
        }


        return newFaces;        
    }
    
    public List<Face> DivideFace(Face face, Vector2 clipLineDir, Vector2 clipLinePoint)
    {
        
        var hits = GenerateIntersections(face, clipLineDir, clipLinePoint);
        
        if (hits.Count != 2) return [face];
    
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

            splitIn.SetNextEdge(clipOut);
            clipIn.SetNextEdge(splitOut);

        }

        var newFaceA = GenerateFace(newEdge, face.Transform);
        var newFaceB = GenerateFace(newEdge.TwinEdge, face.Transform);
        
        faces.Remove(face);
        faces.Add(newFaceA); faces.Add(newFaceB);
        return [newFaceA, newFaceB];
    }

    public Face GenerateFace(HalfEdge EdgeOnFace, Transform3D transform)
    {
            if (EdgeOnFace == null) GD.Print("null face passed to generate");
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

    public List<Face> GetFaces()
    {
        return faces.ToList();
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

    public ArrayMesh GetMesh___new()
    {
        // 0 = vertex
        // 1 = normal
        // 3 = color
        // 12 = index
       GetMesh();
        var meshArray = new Godot.Collections.Array();
        meshArray.Resize((int)Mesh.ArrayType.Max);

        List<PolyVertex> verts = new();
        List<Vector3> vertPositions = new();
        Dictionary<PolyVertex, int> vertIndexMap = new();
        Dictionary<PolyVertex, List<Vector3>> normalsMap = new();
        List<int> indices = new();

        // for each face:
        // tries to index each vert in master list
        // adds the face normal to that verts list of normals (to avergage in next loop)
        // add triangulation to index list using values of vertIndexMap (ASSUMES EACH FACE IS QUAD)
        
        // helper func for indexing verts
        int GetVertIndex(PolyVertex vert)
        {
            if (vertIndexMap.TryGetValue(vert, out int value) ) return value;
            verts.Add(vert);
            vertIndexMap[vert] = verts.Count - 1;
            return verts.Count - 1;
        }

        foreach (var face in faces)
        {
            var normal = face.GetNormal();
            var vertIds = new List<int>();
            foreach (var vert in face.GetVerts())
            {
                vertIds.Add(GetVertIndex(vert));
                if (!normalsMap.ContainsKey(vert)) normalsMap[vert] = new();
                normalsMap[vert].Add(normal);
            }
            indices.AddRange([
                vertIds[0], vertIds[1], vertIds[3],
                vertIds[3], vertIds[1], vertIds[2]
            ]);
        }

        var normalsList = new List<Vector3>();
        foreach (var vert in verts)
        {
            var normTotal = normalsMap[vert].Aggregate(Vector3.Zero, (sum, v) => sum + v);
            normalsList.Add( normTotal / normalsMap[vert].Count );
        }

        return new();
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


    public void ExtrudeEdges()
    {
        if (OriginalEdgeSample == null) return;
        var originalEdges = ReconstructOriginalEdge();

        Vector3 ExtrudeVec = new Vector3(0,0,-extrudeLength);
        // loop and extrude each edge into new face
        for (int i = 0; i < originalEdges.Count; i++)
        {
            
            // generate next faces edge as twin of original
            var originalEdge = originalEdges[i];
            var edge_0 = originalEdge.SetTwinEdge();

            // generate next 3 edges completing face
            var vert_0 = edge_0.EndVert;
            var vert_1 = GetExtrudedVertex(vert_0, ExtrudeVec);
            var vert_2 = GetExtrudedVertex(edge_0.StartVert, ExtrudeVec);
            var vert_3 = edge_0.StartVert;

            var edge_1 = ContinueEdgeToVertex(edge_0, vert_1);
            var edge_2 = ContinueEdgeToVertex(edge_1, vert_2);
            var edge_3 = ContinueEdgeToVertex(edge_2, vert_3);
            edge_3.NextEdge = edge_0;
            edge_0.PrevEdge = edge_3;

            if (i != 0)
            {
                var edge_1_twin = originalEdges[i-1].TwinEdge.PrevEdge;
                edge_1.TwinEdge = edge_1_twin;
                edge_1_twin.TwinEdge = edge_1;
            }

            
            var face = new Face();

            var faceOrigin = vert_1.Position;
            var basisX = (vert_1.Position-vert_0.Position).Normalized();
            var basisY = (vert_3.Position - vert_0.Position).Normalized();
            var basisZ = basisX.Cross(basisY).Normalized();
            
            var faceBasis = new Basis(basisX, basisY, basisZ).Orthonormalized();

            face.Transform = new Transform3D(
                faceBasis.Inverse(), 
                -(faceBasis.Inverse() * faceOrigin)
            );
            
            
            face.AddEdge(edge_0); 
            face.AddEdge(edge_1); 
            face.AddEdge(edge_2); 
            face.AddEdge(edge_3);
            faces.Add(face);
            
        }

    }

    public HalfEdge ContinueEdgeToVertex(HalfEdge edge, PolyVertex newVert)
    {
        var newHalfEdge = new HalfEdge(edge.EndVert, newVert);
        edge.NextEdge = newHalfEdge;
        newHalfEdge.PrevEdge = edge;
        return newHalfEdge;
    }
    public PolyVertex GetExtrudedVertex(PolyVertex vert, Vector3 extrusion)
    {
        return new PolyVertex(){Position = vert.Position + extrusion};
    }


    public List<HalfEdge> ReconstructOriginalEdge()
    {
        var outerEdgeList = new List<HalfEdge>();
        var cur = OriginalEdgeSample;
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
