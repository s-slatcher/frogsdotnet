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
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.CompilerServices;
using System.Net;

public partial class PolygonMeshTool : GodotObject
{
    HashSet<Face> faces = new();

    Dictionary<HalfEdge, (Vector2, Vector2)> halfEdgeUVRangeMap = new();
    
    Dictionary<int, HashSet<Face>> smoothingGroups = new();
    Dictionary<Face, int> faceSmoothGroupMap = new();

    private NormalPoly normPoly;

    private List<HalfEdge> unresolvedEdgeList = new();
    
    static Vector2 RoundVector2(Vector2 vec)
    {
        return new Vector2(float.Floor(vec.X * 1000), float.Floor(vec.Y * 1000)) / 1000;
    }

    public HalfEdge GetStandaloneEdge(Vector3 posStart, Vector3 posEnd)
    {
        var newEdge = new HalfEdge()
        {
            StartVert = new PolyVertex(posStart),
            EndVert = new PolyVertex(posEnd)
        };
        newEdge.SetTwinEdge();

        newEdge.Face = Face.FaceFromInitialEdge(newEdge);  
        newEdge.TwinEdge.Face = Face.FaceFromInitialEdge(newEdge.TwinEdge);  

        return newEdge;
    }

    public HalfEdge CreateNextEdge(HalfEdge prevEdge, Vector3 nextVertexPosition)
    {
        var nextVert = new PolyVertex(nextVertexPosition);
        var nextEdge = new HalfEdge(prevEdge.EndVert, nextVert);
        nextEdge.SetTwinEdge();

        SetEdgeAsNext(prevEdge, nextEdge);



        return nextEdge;
    }

    public HalfEdge ConnectEdges(HalfEdge edgeA, HalfEdge edgeB)
    {
        var connectEdge = new HalfEdge(edgeA.EndVert, edgeB.StartVert);
        connectEdge.SetTwinEdge();

        
        SetEdgeAsNext(edgeA, connectEdge);
        SetEdgeAsNext(connectEdge, edgeB);

        // new face resolve will set a new face for each set of edges

        ResolveConnectEdgeFace(connectEdge);
        ResolveConnectEdgeFace(connectEdge.TwinEdge);


        // unresolvedEdgeList.Add(connectEdge); 
        // unresolvedEdgeList.Add(connectEdge.TwinEdge); 
        // call the face generator after each connection
        // if (forceFaceResolve) ResolveFaces();


        return connectEdge;
    }

    private bool ResolveConnectEdgeFace(HalfEdge edge)
    {
        var prevFace = edge.PrevEdge.Face;
        var nextFace = edge.PrevEdge.Face;
        bool sharedFace = prevFace == nextFace && prevFace != null;
        

        // first check if shared -- if so, a face resolve will be run 
        if (sharedFace)
        {
            
            var face = ResolveFaceFromEdge(edge);
            if (faceSmoothGroupMap.TryGetValue(face, out int smoothingGroup));
            IndexFace(face, smoothingGroup);
            return true;
        }
        // if not same, set the three immediately connected edges the same face so the next connection that completes will recognize them as resolvable 
        else
        {
            // face is temp and won't be indexed
            var face = Face.FaceFromInitialEdge(edge.PrevEdge);
            face.AddEdgeToEnd(edge);
            face.AddEdgeToEnd(edge.NextEdge);
            return false;
            
        }


    }

    private void IndexFace(Face face, int smoothingGroup = 0)
    {
        faces.Add(face);
        SetFaceSmoothingGroup(face, smoothingGroup);
        
    }


    private Face ResolveFaceFromEdge(HalfEdge edge)
    {
        var face = Face.FaceFromInitialEdge(edge);
        var cur = edge.NextEdge;
        while (cur != edge)
        {
            face.AddEdgeToEnd(cur);
            cur = cur.NextEdge;
        }
        return face;
    }

    // public HalfEdge ConnectEdge(HalfEdge connectEdge)
    // {

    // }



    public void SetFaceSmoothingGroup(Face face, int group)
    {
        if (smoothingGroups.TryGetValue(group, out var faceSet)) faceSet.Add(face);
        else smoothingGroups[group] = [face];
        faceSmoothGroupMap[face] = group;
    }
  
    public ArrayMesh GetMesh()
    {
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
        // add triangulation to index list using values of vertIndexMap 
        
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

        GD.Print("face count at GETMESH: ", faces.Count);
        foreach (var face in faces)
        {
            // start by getting all edges including children
            var faceEdges = face.GetEdges();
            var faceVerts = new List<PolyVertex>();
            foreach(var edge in faceEdges)
            {
                if (edge.HasTwin() && edge.TwinEdge.HasChildren())
                {
                    // stitch in vertices of twin edge if it has been subdivide more
                    var allTwinChildVerts = edge.TwinEdge.GetChildVerticesRecursive();
                    allTwinChildVerts.Reverse();
                    allTwinChildVerts.RemoveAt(allTwinChildVerts.Count - 1);
                    faceVerts.AddRange(allTwinChildVerts);
                }
                else
                {
                    faceVerts.Add(edge.StartVert);
                }
            }

            var facePositions = faceVerts.Select(v => v.Position).ToList();

            var triangles = GeometryUtils.TriangulateFace3D(facePositions);
            foreach (var intArr in triangles)
            {
                IndexTriangle(faceVerts[intArr[0]], faceVerts[intArr[1]], faceVerts[intArr[2]]);
            }

        }

        foreach (var vert in verts)
        {
            var normals = normalsMap[vert];
            var normTotal = normals.Aggregate(Vector3.Zero, (sum, v) => sum + v);
            mesh_norms.Add( normTotal / normals.Count );
        }

        // set mesh arrays 
        meshArray[(int)Mesh.ArrayType.Vertex] = mesh_verts.ToArray();
        meshArray[(int)Mesh.ArrayType.Normal] = mesh_norms.ToArray();
        meshArray[(int)Mesh.ArrayType.Index] = mesh_indices.ToArray();
        meshArray[(int)Mesh.ArrayType.TexUV] = verts.Select(v => v.UV).ToArray();

        var arrMesh = new ArrayMesh();
        arrMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArray);
        return arrMesh;
    }
    
    private void SetEdgeAsNext(HalfEdge prevEdge, HalfEdge nextEdge)
    {
        
        Debug.Assert(prevEdge.EndVert == nextEdge.StartVert,
        "tried to connect discontinuous edges");

        var prevNextOrTwin = prevEdge.NextEdge ?? prevEdge.TwinEdge;
        var nextPrevOrTwin = nextEdge.PrevEdge ?? nextEdge.TwinEdge;

        

        // if (nextEdge.Face != null ) RemoveFace(nextEdge.Face);
        // if (prevEdge.Face != null) RemoveFace(prevEdge.Face);


        prevEdge.SetNextEdge(nextEdge);
        nextPrevOrTwin.SetNextEdge(prevNextOrTwin);

        if (prevEdge.Face != null && prevEdge.Face.IsValid)
        {
            
        }
    
    }


    // public void RemoveFace(Face face)
    // {
    //     if (faces.Remove(face)) foreach (var edge in face.EdgeList) edge.Face = null;
    // }

    public void DeIndexFace(Face face)
    {
        faces.Remove(face);
        smoothingGroups[faceSmoothGroupMap[face]].Remove(face);
    }


    public void ResolveFaces()
    {
        HashSet<HalfEdge> seenEdges = new();
        HashSet<HalfEdge> resolvedEdges = new();
        
        for (int i = 0; i < unresolvedEdgeList.Count; i++)
        {
            var edge = unresolvedEdgeList[i];

            // follow the next-edge chain until a loop is formed
            // on dead end, or when hitting an edge with a face already seen, break;
            var face = new Face();
            var curEdge = edge;
            do
            {
                if (!seenEdges.Add(curEdge)) break;
                face.AddEdgeToEnd(curEdge);
                resolvedEdges.Add(curEdge);
                curEdge.Face = face;
                curEdge = curEdge.NextEdge;
                if (curEdge == null )
                {
                    GD.Print("no next edge found");
                    break;
                }

            } while (curEdge != edge);
            
            if (face.IsValid) faces.Add(face);
            
        }
        
        
        unresolvedEdgeList = unresolvedEdgeList
            .Where(e => !resolvedEdges.Contains(e)).ToList(); 

    }


    // new subdivide call 
    public void SubdivideMesh(float maxArea)
    {

        // setup queue of faces to subdivide (SD), fill with all current faces 
        var subdivideQueue = faces.ToList();  

        int qPos = 0;
        while (qPos < subdivideQueue.Count)
        {
            
            var face = subdivideQueue[qPos];
            qPos++;
            var customMaxArea = maxArea;
            var absNormY = float.Abs(face.Normal.Y);
            if (absNormY > 0.5) customMaxArea *= 5;
            if (face.Normal.Z > 0.5) customMaxArea *= 5;
            
            if (face.Area > customMaxArea) subdivideQueue.AddRange(SubdivideFace(face));
        } 
        
        
    }

    private List<Face> SubdivideFace(Face face)
    {
        DeIndexFace(face); 

        var edges = face.GetEdges();
        var lens = new Dictionary<HalfEdge, float>();
        foreach ( var edge in edges) lens[edge] = edge.GetLen();
        var edgesByLen = face.GetEdges().OrderBy(e => -lens[e]).ToList();

        var bestNextSubEdge = edgesByLen[0];
        var highScore = 0f;
        for (int i = 1; i < edgesByLen.Count; i++)
        {
            // edges compete to edge that is connected to the longest edge with score 
            // 1 point can be earned by being close in length, dot product presents a 2-point swing from -1, to 1
            //TODO (MAYBE) construct the dot product test with an avg of two edges if the edge picked is too small to split down middle
            var edge = edgesByLen[i];
            var dotScore = edge.GetVector().Normalized().Dot(edgesByLen[0].GetVector().Normalized()) * -1;
            var lengthScore = lens[edge] / lens[edgesByLen[0]];
            var score = lengthScore + dotScore;

            if (score > highScore) 
            {
                bestNextSubEdge = edge;
                highScore = score;
            }

            // call it when a score is high enough to not likely be beat
            // if (score > 1.5f) break;
            
        }

        SplitEdgeMiddle(edgesByLen[0]);
        var subEdge1 = edgesByLen[0].childEdge1;
        var subEdge2 = bestNextSubEdge;          
        // if the smaller subdivide edge is large enough, split first and snap to child edge 
        if (lens[bestNextSubEdge] > lens[edgesByLen[0]] / 2)
        {
            SplitEdgeMiddle(bestNextSubEdge);
            subEdge2 = bestNextSubEdge.childEdge2;
        }
        
        // special case where the chosen subdivide edge is already the next edge
        if (subEdge1.NextEdge == subEdge2)
        {
            subEdge2 = subEdge2.NextEdge;
        }

        var newEdge = ConnectEdges(subEdge1, subEdge2);
        var newFaces = new List<Face>(){newEdge.Face, newEdge.TwinEdge.Face};

        

        return newFaces;

        
    }

    private void SplitEdgeMiddle(HalfEdge hEdge)
    {
        Debug.Assert(!hEdge.HasChildren(), "Edge already split in middle");
        
        PolyVertex mv;
        bool hasSplitTwin = hEdge.HasTwin() && hEdge.TwinEdge.HasChildren();
        if (hasSplitTwin)
        {
            mv = hEdge.TwinEdge.childEdge1.EndVert;
        }
        else
        {
            var midPos = (hEdge.EndVert.Position - hEdge.StartVert.Position) / 2 + hEdge.StartVert.Position;
            mv = new PolyVertex(){Position = midPos};
        }

        SplitHalfEdge(hEdge, mv);       

        if (hasSplitTwin)
        {
            hEdge.childEdge1.SetAsTwinEdge(hEdge.TwinEdge.childEdge2);
            hEdge.childEdge2.SetAsTwinEdge(hEdge.TwinEdge.childEdge1);
        }        
        
        
    }

    private void SplitHalfEdge(HalfEdge hEdge, PolyVertex splitVert)
    {
        var sv = hEdge.StartVert;
        var ev = hEdge.EndVert;
        var mv = splitVert;

        var svUV = sv.UV;
        var evUV = ev.UV;
        if (evUV.X == 1) evUV = new Vector2(0, evUV.Y);

        
        mv.UV = (evUV + svUV) / 2;

        hEdge.childEdge1 = new HalfEdge(){StartVert = sv, EndVert = mv};
        hEdge.childEdge2 = new HalfEdge(){StartVert = mv, EndVert = ev};

        hEdge.PrevEdge.SetNextEdge(hEdge.childEdge1);
        hEdge.childEdge1.SetNextEdge(hEdge.childEdge2);
        hEdge.childEdge2.SetNextEdge(hEdge.NextEdge);
        
    }


    





  
    public List<Face> GetFaces()
    {
        return faces.ToList();
    }


    private List<List<PolyVertex>> TriangulateNonQuadFace(Face face)
    {
        var triList = new List<List<PolyVertex>>();
        


        return triList;
    }

  

    public static Vector3 GetNormal(Vector3[] triangle)
    {
        // var posList = triangle.Select(v => v.Position).ToArray();
        var normal = (triangle[2]-triangle[0]).Cross(triangle[1]-triangle[0]).Normalized();
        return normal;
    }
    public static Vector3 GetNormal(HalfEdge edge)
    {
        var tri = new Vector3[]
        {
            edge.StartVert.Position, edge.EndVert.Position, edge.NextEdge.EndVert.Position, 
        };
        return GetNormal(tri);
    }




}
