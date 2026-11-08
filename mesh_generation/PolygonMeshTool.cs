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

public partial class PolygonMeshTool : GodotObject
{
    HashSet<Face> faces = new();

    Dictionary<Face, List<Face>> SubdividedFacesMap = new();

    List<Face> initFrontFaces = new();
    List<Face> initBackFaces = new();

    List<HalfEdge> frontOutline = new();
    List<HalfEdge> backOutline = new();

    public HalfEdge OriginalEdgeSample;
    
    private bool hasExtruded = false;

    NormalPoly normPoly;

    Aabb aabb;

    // new system for face validation
    private List<HalfEdge> unresolvedEdgeList = new();
    
    static Vector2 RoundVector2(Vector2 vec)
    {
        return new Vector2(float.Floor(vec.X * 1000), float.Floor(vec.Y * 1000)) / 1000;
    }

    public Aabb GetAabb() => aabb;


    public HalfEdge GetStandaloneEdge(Vector3 posStart, Vector3 posEnd)
    {
        var newEdge = new HalfEdge()
        {
            StartVert = new PolyVertex(posStart),
            EndVert = new PolyVertex(posEnd)
        };
        newEdge.SetTwinEdge();

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

    public HalfEdge ConnectEdges(HalfEdge edgeA, HalfEdge edgeB, bool forceFaceResolve = true)
    {
        var connectEdge = new HalfEdge(edgeA.EndVert, edgeB.StartVert);
        connectEdge.SetTwinEdge();

        
        SetEdgeAsNext(edgeA, connectEdge);
        SetEdgeAsNext(connectEdge, edgeB);


        unresolvedEdgeList.Add(connectEdge); 
        unresolvedEdgeList.Add(connectEdge.TwinEdge); 

        // call the face generator after each connection
        if (forceFaceResolve) ResolveFaces();


        return connectEdge;
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


        if (nextEdge.Face != null ) RemoveFace(nextEdge.Face);
        if (prevEdge.Face != null) RemoveFace(prevEdge.Face);


        prevEdge.SetNextEdge(nextEdge);
        nextPrevOrTwin.SetNextEdge(prevNextOrTwin);
    }


    public void RemoveFace(Face face)
    {
        if (faces.Remove(face)) foreach (var edge in face.EdgeList) edge.Face = null;
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
            float areaMult = 1;
            areaMult = face.Normal.Dot(Vector3.Forward) / 2 + 0.5f;
            if (areaMult < 0.25) areaMult = 0.25f;
            if (face.Area * areaMult > customMaxArea) subdivideQueue.AddRange(SubdivideFace(face));
        } 
        
        
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

        hEdge.childEdge1 = new HalfEdge(){StartVert = sv, EndVert = mv};
        hEdge.childEdge2 = new HalfEdge(){StartVert = mv, EndVert = ev};

        hEdge.PrevEdge.SetNextEdge(hEdge.childEdge1);
        hEdge.childEdge1.SetNextEdge(hEdge.childEdge2);
        hEdge.childEdge2.SetNextEdge(hEdge.NextEdge);
        
    }


    private List<Face> SubdivideFace(Face face)
    {
        
        
        RemoveFace(face);   
        
        var lengthSortedEdges = face.GetEdges().OrderBy(e => -e.GetLen()).ToList();
       
        var longest = lengthSortedEdges[0];
        HalfEdge nextLongest;
        if (longest.EdgeIsConnected(lengthSortedEdges[1]))
        {
            if (longest.EdgeIsConnected(lengthSortedEdges[2]))
            {
                if (lengthSortedEdges.Count > 3) nextLongest = lengthSortedEdges[3];
                else nextLongest = lengthSortedEdges[2];
            }
            else nextLongest = lengthSortedEdges[2];
        }
        else nextLongest = lengthSortedEdges[1];

        
        SplitEdgeMiddle(longest);
        SplitEdgeMiddle(nextLongest);

        var newEdge = ConnectEdges(longest.childEdge1, nextLongest.childEdge2, false);

        ResolveFaces();
        return
        [
            newEdge.Face,
            newEdge.TwinEdge.Face
        ];
        // var centerPos = face.WeightedCentser;

    



        // var parallelAllowanceAngle = float.DegreesToRadians(15);

        // roughly deciding what edge is "too short" to split in middle based on area
        // var targetLen = Math.Sqrt(face.Area) * 0.75f; 

        //find longest edge to initiate as first subdivide point 
        
        // List<HalfEdge> SubdivideEdges = new();
        // int longestIdx = 0;
        // for (int i = 0; i < edges.Count; i++)
        // {
        //     if (edges[i].GetLen() > edges[longestIdx].GetLen()) longestIdx = i;
        // }
        
        // reorder edge list to put longest edge first
        // var longestFirstEdges = Enumerable.Range(0, edges.Count)
        //     .Select(i => edges[(longestIdx + i) % edges.Count])
        //     .ToList();
        // var longEdge = longestFirstEdges[0];

        // // if the longest edge is still too short to split in middle, its end point used as the sole subdivide point
        // // otherwise: search for any other edges meeting length req, split them and add split point to subdivide list
        // if (longEdge.GetLen() < targetLen) 
        // {
        //     SubdivideEdges.Add(longEdge);
        // }
        // else
        // {
        //     for (int i = 0; i < longestFirstEdges.Count; i++)
        //     {
        //         var edge = longestFirstEdges[i];
        //         if (edge.GetLen() > targetLen)
        //         {
        //             SplitEdgeMiddle(edge);
        //             SubdivideEdges.Add(edge.childEdge1);
        //         }
                
        //     }
        // }

        // look at angle formed between lines formed from subdivide points -> center point
        // check for an >180 gap that must be fixed by inserting an extra point

        // in case of a sole subdivide point, auto triggers an over 180 check

        // var subEdgeCount = SubdivideEdges.Count;
        // for (int i = 0; i < subEdgeCount; i++)
        // {
        //     var edge = SubdivideEdges[i];
        //     var nextEdge = i == subEdgeCount - 1 ? SubdivideEdges[0] : SubdivideEdges[i+1];

        //     var edgeToCenter = edge.EndVert.Position - centerPos;
        //     var nextEdgeToCenter = nextEdge.EndVert.Position - centerPos;

        //     var angle = edgeToCenter.AngleTo(nextEdgeToCenter);

        //     bool nearlyParallel = float.Abs(angle - float.Pi) < parallelAllowanceAngle;
            
        //     if (nearlyParallel) break;
        //     else if (angle < )

        // }
        

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
