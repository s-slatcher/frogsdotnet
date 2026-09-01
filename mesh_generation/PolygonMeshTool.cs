using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;
using Microsoft.VisualBasic;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Linq.Expressions;
using System.Collections.Immutable;

public partial class PolygonMeshTool : GodotObject
{
    HashSet<Face> faces = new();

    List<Face> initFrontFaces = new();
    List<Face> initBackFaces = new();

    List<HalfEdge> frontOutline = new();
    List<HalfEdge> backOutline = new();

    public HalfEdge OriginalEdgeSample;
    
    private bool hasExtruded = false;

    NormalPoly normPoly;

    Aabb aabb;

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

    public Aabb GetAabb() => aabb;


    public void SetPolygon (NormalPoly normPoly)
    {
        this.normPoly = normPoly; 
        
        // generate two identical, but reverse wind order, faces
        var concavePolygon = normPoly.Polygon;
        initFrontFaces = GenerateFacesFromPoly(concavePolygon, true);
        initBackFaces = GenerateFacesFromPoly(concavePolygon, false);

        backOutline = SetOutsideEdge(initBackFaces[0].EdgeList[0]);
        frontOutline = SetOutsideEdge(initFrontFaces[0].EdgeList[0]);

        // foreach(var face in initBackFaces)
        
        List<Face> faces = [..initFrontFaces];
        faces.AddRange(initBackFaces);
        this.faces = [..faces];



    }

    private List<Face> GenerateFacesFromPoly(Vector2[] concavePoly, bool Clockwise)
    {
        var faces = new List<Face>();
        var convexPolys = Geometry2D.DecomposePolygonInConvex(concavePoly);

        Dictionary<(PolyVertex, PolyVertex), HalfEdge> edgeMap = new();
        Dictionary<Vector2, PolyVertex> vertexMap = new();
        
        PolyVertex GetPointVertex(Vector2 point)
        {
            var roundVec = RoundVector2(point);

            if (vertexMap.TryGetValue(roundVec, out PolyVertex vert)) return vert;
            else
            {
                Vector3 pos = new(point.X, point.Y, 0);
                
                vert = new PolyVertex(){Position = pos};
                vertexMap.Add(roundVec, vert);
                return vert;
            }
        }

        for (int p = 0; p < convexPolys.Count; p++)
        {
            var poly = convexPolys[p];
            if (!Clockwise) poly = poly.Reverse().ToArray();
            var face = new Face();
            
            for (int i = 0; i < poly.Length; i++)
            {
                var curP = poly[i];
                var nextP = i == poly.Length-1 ? poly[0] : poly[i+1];

                PolyVertex curVert = GetPointVertex(curP);
                PolyVertex nextVert = GetPointVertex(nextP);
                
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
        return faces;
    }


    // public void ExtrudeMesh(int length)
    // {
    //     if (hasExtruded)
    //     {
    //         GD.Print(this.GetType().Name, " mesh already extruded");
    //         return;
    //     }

    //     // ExtrudeEdges(length);
    //     SetAabb(length);
    //     hasExtruded = true;
    // }

    public void Extrude(int depth)
    {
        // move backface verts
        foreach (var face in initBackFaces)
        {
            foreach(var vert in face.GetVerts()) vert.Position.Z = -depth;
        }

        var reverseOutline = backOutline.ToList();
        reverseOutline.Reverse();

        foreach(var face in StitchEdges(frontOutline, reverseOutline))
        {
           faces.Add(face); 
        }
        
        SetAabb(depth);


    }

    public List<Face> StitchEdges(List<HalfEdge> edgesA, List<HalfEdge> edgesB)
    {
        if (edgesA.Count != edgesB.Count)
        {
            GD.Print("StitchEdge: edge list counts not same");
            return new();
        }

        var newFaces = new List<Face>();
        var newEdges = new List<HalfEdge>();
        // use a dictionary to match twins to more simply handle if faces loop around or not

        for (int i = 0; i < edgesA.Count; i++)
        {
            var edgeA = edgesA[i];
            var edgeB = edgesB[i];

            // verify
            if (i != edgesA.Count - 1)
            {
                if (edgeA.NextEdge != edgesA[i+1] || edgeB.PrevEdge != edgesB[i+1])
                {
                    GD.Print("StitchEdges: edge list(s) aren't ordered properly");
                    return new();
                } 
            }

            var startVert = edgeA.EndVert;
            var endVert = edgeB.EndVert;
            var intersect = new HalfEdge(startVert, endVert);
            intersect.SetTwinEdge();
            RerouteEdge(edgeA, intersect);
            RerouteEdge(edgeB, intersect);
            newEdges.Add(intersect);

        }
        foreach (var hEdge in newEdges) newFaces.Add(GenerateFace(hEdge));

        return newFaces;
        
    }

    public void RerouteEdge(HalfEdge edge, HalfEdge intersectingEdge)
    {
        if (intersectingEdge.TwinEdge == null)
        {
            GD.Print("RerouteEdge: intersect has no twin edge");
            return;
        }
        
        if (intersectingEdge.StartVert != edge.EndVert) intersectingEdge = intersectingEdge.TwinEdge;
        if (intersectingEdge.StartVert != edge.EndVert)
        {
            GD.Print("RerouteEdge: secon edge not intersection at edges end");
            return;
        }

        intersectingEdge.TwinEdge.SetNextEdge(edge.NextEdge);

        edge.SetNextEdge(intersectingEdge);


    }


    




    private void SetAabb(float extrudeLength)
    {
        var size2d = normPoly.Rect.Size;
        var size = new Vector3(size2d.X, size2d.Y, -extrudeLength);
        aabb = new Aabb(Vector3.Zero, size).Abs();
    }


    public void SubdivideAll(int levels = 1)
    {
        for (int i = 0; i < levels; i++)
        {
            List<Face> newFaces = new();
            foreach (var face in faces) newFaces.AddRange(Subdivide(face));
            faces = [.. newFaces]; 
        }
        
    }


    private PolyVertex SplitEdgeMiddle(HalfEdge hEdge)
    {
        
        // check first if this edge was split already by adj face subdivide
        if (hEdge.HasChildren()) return hEdge.childEdge1.EndVert;
        
        var midPos = (hEdge.EndVert.Position - hEdge.StartVert.Position) / 2 + hEdge.StartVert.Position;
        var mv = new PolyVertex(){Position = midPos};


        SplitHalfEdge(hEdge, mv);
        if (hEdge.HasTwin()) 
        {
            SplitHalfEdge(hEdge.TwinEdge, mv);

            hEdge.childEdge1.TwinEdge = hEdge.TwinEdge.childEdge2;
            hEdge.childEdge2.TwinEdge = hEdge.TwinEdge.childEdge1;

            hEdge.TwinEdge.childEdge1.TwinEdge = hEdge.childEdge2;
            hEdge.TwinEdge.childEdge2.TwinEdge = hEdge.childEdge1;
        }
        
        return hEdge.childEdge1.EndVert;
    }

        private Face GenerateFace(HalfEdge EdgeOnFace)
    {
            var face = new Face();
            var curEdge = EdgeOnFace;
            do
            {
                face.AddEdge(curEdge);
                curEdge.Face = face;
                curEdge = curEdge.NextEdge;
            } while (curEdge != EdgeOnFace);

            return face;
    }

    private void SplitHalfEdge(HalfEdge hEdge, PolyVertex splitVert)
    {
        var sv = hEdge.StartVert;
        var ev = hEdge.EndVert;
        var mv = splitVert;

        hEdge.childEdge1 = new HalfEdge(){StartVert = sv, EndVert = mv};
        hEdge.childEdge2 = new HalfEdge(){StartVert = mv, EndVert = ev};

        hEdge.PrevEdge.SetNextEdge(hEdge.childEdge1);
        hEdge.childEdge2.SetNextEdge(hEdge.NextEdge);
        hEdge.childEdge1.SetNextEdge(hEdge.childEdge2);
    }


    private List<Face> Subdivide(Face convexFace)
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
            newFaces.Add(GenerateFace(edge.childEdge1));
        }


        return newFaces;        
    }

    public void BevelEdge(HalfEdge edge, float bevelDist, int faceCount)
    {
        // for now, ignore the back edges with no extra face to bevel with
        var twinEdge = edge.TwinEdge;


        // collect relevant faces, edges.
        // for now, assume the edge passed is the one pointing up
        var edgeOut = edge.NextEdge;
        var edgeIn = edge.PrevEdge;
        var twinOut = twinEdge.NextEdge;
        var twinIn = twinEdge.PrevEdge;

        GD.Print(edgeOut.TwinEdge, "edge out twin");
        GD.Print(edgeIn.TwinEdge, "edge in twin");
        // debug prints

        // var topFace = edgeOut.TwinEdge.Face;
        // var botFace = edgeIn.TwinEdge.Face;
        // if (topFace == null || botFace == null) GD.Print("null top or bottom face");

        
        // if (twinIn.TwinEdge.Face != topFace || twinOut.TwinEdge.Face != botFace)
        // {
        //     GD.Print("opposite incident edge faces don't match");
        //     return;
        // }


    }
    

    // deprecated 'divide face' methods
    // could divide convex polygons with any split line and maintain the edge-face relationship
    // used transform data not part of the new mesh subdivide method, which now always generates quads
    // future use:
    // - custom 2d mesh system that needs a halfedge-vert system
    // - fast slicing for UI effects or collision boxes
    // - 

    
    // private List<Face> DivideFace(Face face, Vector2 clipLineDir, Vector2 clipLinePoint)
    // {
        
    //     var hits = GenerateIntersections(face, clipLineDir, clipLinePoint);
        
    //     if (hits.Count != 2) return [face];
    
    //     // generate twin edges for clip boundary
    //     var newEdge = new HalfEdge(hits[0].vertex, hits[1].vertex);
    //     newEdge.GenerateTwinEdge();
        
    //     foreach (var hit in hits)
    //     {
    //         var edgeToRoute = hit.halfEdge;

    //         // splits the edge, and its twin half edge
    //         if (hit.DoSplitEdge)
    //         {
    //             hit.halfEdge.SplitEdge(hit.vertex, false);
    //             edgeToRoute = hit.halfEdge.childEdge1;
    //         }

    //         var clipOut = newEdge;
    //         var clipIn = newEdge.TwinEdge;
    //         var splitIn = edgeToRoute;
    //         var splitOut = edgeToRoute.NextEdge;
    //         if (splitIn.EndVert != clipOut.StartVert) (clipOut, clipIn) = (clipIn, clipOut);

    //         splitIn.SetNextEdge(clipOut);
    //         clipIn.SetNextEdge(splitOut);

    //     }

    //     var newFaceA = GenerateFace(newEdge);
    //     var newFaceB = GenerateFace(newEdge.TwinEdge);
        
    //     faces.Remove(face);
    //     faces.Add(newFaceA); faces.Add(newFaceB);
    //     return [newFaceA, newFaceB];
    // }


    // private static Vector2? GetLineIntersectOrNull(Vector2 pA, Vector2 dirA, Vector2 pB, Vector2 dirB)
    // {
    //     float dotProd = (dirA.Normalized()).Dot(dirB.Normalized());
    //     bool isParallelApprox = Math.Abs(dotProd) > 0.999f;
    //     if (isParallelApprox) return null;
    //     return (Vector2)Geometry2D.LineIntersectsLine(pA, dirA, pB, dirB);

    // }

    // private static LineSegment HalfEdgeAs2DLine(HalfEdge hEdge)
    // {
    //     var face = hEdge.Face;
    //     var p1 = face.VertToLocalSpace(hEdge.StartVert);
    //     var p2 = face.VertToLocalSpace(hEdge.EndVert);
    //     return new LineSegment(p1, p2);
    // }

    
    // private List<UnhandledIntersection> GenerateIntersections(Face face, Vector2 clipLineDir, Vector2 clipLinePoint)
    // {
    //     var ui_list = new List<UnhandledIntersection>();
    //     // var hitsList = new List<(PolyVertex, HalfEdge)>();
    //     var f = face;
    //     var c_dir = clipLineDir;
    //     var c_p = clipLinePoint;
        
    //     // if (face.GetNormal().Z > 0.9) return [];

    //     for (int i = 0; i < f.EdgeList.Count; i++)
    //     {
            
    //         var hEdge = f.EdgeList[i];
    //         LineSegment edge2D = HalfEdgeAs2DLine(hEdge); 
    //         var snapEpsilon = 0.01f;       

    //         Vector2? maybeIntersect = GetLineIntersectOrNull(c_p, c_dir, edge2D.Start, edge2D.DirectionVector().Normalized());
    //         if (maybeIntersect == null)
    //         {
    //             continue;
    //         }

    //         Vector2 intersect = (Vector2)maybeIntersect;

    //         var dist1 = intersect.DistanceTo(edge2D.Start);
    //         var dist2 = intersect.DistanceTo(edge2D.End);
    //         var edgeDist = edge2D.Length();
            
    //         if (dist2 < snapEpsilon) 
    //         {
    //             ui_list.Add(new UnhandledIntersection(hEdge.EndVert, hEdge, false));
    //         }
    //         else if ( dist1 > snapEpsilon && dist2 > snapEpsilon)
    //         {
    //             if (dist1 < edgeDist && dist2 < edgeDist)
    //             {
    //                 var newVert = new PolyVertex(){Position = f.PointToMeshSpace(intersect)};
    //                 ui_list.Add(new UnhandledIntersection(newVert, hEdge, true));
    //             }
    //         }
    //         else
    //         {
    //             // GD.Print("intersect discarded at: ", intersect);
    //             continue;
    //         }

    //     }
        
    //     return ui_list;
        
    // }
    


    private static void PairTwinEdges(HalfEdge a, HalfEdge b)
    {
        a.TwinEdge = b;
        b.TwinEdge = a;
    }

    public List<Face> GetFaces()
    {
        return faces.ToList();
    }


    /// <summary>
    /// new mesh-making method, ASSUMES ALL FACES ARE QUADS (i.e., subdivide-all run at least once)
    /// </summary>
    /// <returns></returns>
    public ArrayMesh GetMesh()
    {
        // 0 = vertex
        // 1 = normal
        // 3 = color
        // 12 = index
        var meshArray = new Godot.Collections.Array();
        meshArray.Resize((int)Mesh.ArrayType.Max);

        List<PolyVertex> verts = new();
        Dictionary<PolyVertex, int> vertIndexMap = new();
        Dictionary<PolyVertex, List<Vector3>> normalsMap = new();

        List<Vector3> mesh_verts = new();
        List<int> mesh_indices = new();
        List<Vector3> mesh_norms = new();

        // for each face:
        // tries to index each vert in master list
        // adds the face normal to that verts list of normals (to avergage in next loop)
        // add triangulation to index list using values of vertIndexMap (ASSUMES EACH FACE IS QUAD)
        
        // helper func for indexing verts and their positions
        int GetVertIndex(PolyVertex vert)
        {
            if (vertIndexMap.TryGetValue(vert, out int value) ) return value;
            verts.Add(vert);
            mesh_verts.Add(vert.Position);
            normalsMap[vert] = new();
            vertIndexMap[vert] = verts.Count - 1;
            return verts.Count - 1;
        }
        // index triangle and calculate normal (quad can have 2 normals after triangulating)
        void IndexTriangle(PolyVertex a, PolyVertex b, PolyVertex c)
        {
            int[] indices = [GetVertIndex(a), GetVertIndex(b), GetVertIndex(c)];
            mesh_indices.AddRange(indices);
            var norm = GetNormal([.. indices.Select(i => mesh_verts[i])]);
            normalsMap[a].Add(norm);
            normalsMap[b].Add(norm);
            normalsMap[c].Add(norm);
        }

        

        // loop each face indexing verts, triangles positions, and normals
        foreach (var face in faces)
        {
            var faceVerts = face.GetVerts();
            IndexTriangle(faceVerts[0], faceVerts[3], faceVerts[1]);
            IndexTriangle(faceVerts[1], faceVerts[3], faceVerts[2]);
        }

        foreach (var vert in verts)
        {
            var normals = normalsMap[vert];
            var normTotal = normals.Aggregate(Vector3.Zero, (sum, v) => sum + v);
            mesh_norms.Add( normTotal / normals.Count );
        }

        // for (int i = 0; i < mesh_verts.Count; i++)
        // {
        //     var randPos = new Vector3(GD.Randf()/20,GD.Randf()/20,GD.Randf()/20);
        //     mesh_verts[i] += randPos;
        // }

        // set mesh
        meshArray[(int)Mesh.ArrayType.Vertex] = mesh_verts.ToArray();
        meshArray[(int)Mesh.ArrayType.Normal] = mesh_norms.ToArray();
        meshArray[(int)Mesh.ArrayType.Index] = mesh_indices.ToArray();

        var arrMesh = new ArrayMesh();
        arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArray);
        return arrMesh;
    }

    private static Vector3 GetNormal(Vector3[] triangle)
    {
        // var posList = triangle.Select(v => v.Position).ToArray();
        var normal = (triangle[2]-triangle[0]).Cross(triangle[1]-triangle[0]).Normalized();
        return normal;
    }

    private void AddVertex(PolyVertex vert, Vector3 normal, Godot.Color vertexColor, SurfaceTool st,  bool PerFaceNormal = true)
    {
        st.SetNormal(normal);
        st.SetColor(vertexColor);
        st.AddVertex(vert.Position);
    }

  
    // private void ExtrudeEdges(float extrudeLength)
    // {
    //     if (OriginalEdgeSample == null) return;
    //     var frontEdgeList = ReconstructOriginalEdge();
        
    //     var back


    //     Vector3 ExtrudeVec = new Vector3(0,0,-extrudeLength);
    //     // loop and extrude each edge into new face
    //     for (int i = 0; i < originalEdges.Count; i++)
    //     {
            
    //         // generate next faces edge as twin of original
    //         var originalEdge = originalEdges[i];
    //         var edge_0 = originalEdge.SetTwinEdge();

    //         // generate next 3 edges completing face
    //         var vert_0 = edge_0.EndVert;
    //         var vert_1 = GetExtrudedVertex(vert_0, ExtrudeVec); 
    //         var vert_2 = GetExtrudedVertex(edge_0.StartVert, ExtrudeVec);
    //         var vert_3 = edge_0.StartVert;

    //         var edge_1 = ContinueEdgeToVertex(edge_0, vert_1);
    //         var edge_2 = ContinueEdgeToVertex(edge_1, vert_2);
    //         var edge_3 = ContinueEdgeToVertex(edge_2, vert_3);
    //         edge_3.NextEdge = edge_0;
    //         edge_0.PrevEdge = edge_3;

    //         if (i != 0)
    //         {
    //             var edge_1_twin = originalEdges[i-1].TwinEdge.PrevEdge;
    //             edge_1.TwinEdge = edge_1_twin;
    //             edge_1_twin.TwinEdge = edge_1;
    //         }

            
    //         var face = new Face();

            
    //         face.AddEdge(edge_0); 
    //         face.AddEdge(edge_1); 
    //         face.AddEdge(edge_2); 
    //         face.AddEdge(edge_3);
    //         faces.Add(face);
            
    //     }

    
    // }

    private HalfEdge ContinueEdgeToVertex(HalfEdge edge, PolyVertex newVert)
    {
        var newHalfEdge = new HalfEdge(edge.EndVert, newVert);
        edge.NextEdge = newHalfEdge;
        newHalfEdge.PrevEdge = edge;
        return newHalfEdge;
    }
    private PolyVertex GetExtrudedVertex(PolyVertex vert, Vector3 extrusion)
    {
        return new PolyVertex(){Position = vert.Position + extrusion};
    }


    private List<HalfEdge> SetOutsideEdge(HalfEdge initEdge)
    {
        var outerEdgeList = new List<HalfEdge>();
        var cur = initEdge;
        do
        {
            if (cur.TwinEdge != null)
            {
                cur = cur.TwinEdge.NextEdge;
                continue;
            }
            
            outerEdgeList.Add(cur);
            cur = cur.NextEdge;
                
        } while (outerEdgeList.Count == 0 || outerEdgeList[0] != cur);

        var tempFace = new Face(); 
        for (int i = outerEdgeList.Count-1; i >= 0; i--)
        {
            var twin = outerEdgeList[i].SetTwinEdge();
            tempFace.AddEdge(twin); // 'AddFace' validates and sets edge connections
        }

        return tempFace.EdgeList.ToList();

       
    }





}
