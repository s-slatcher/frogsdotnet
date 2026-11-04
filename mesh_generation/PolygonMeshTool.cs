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
    Dictionary<Face, Basis> FaceBasisMap = new();

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
        //SKIP FACES WITH 5+ POINTS UNTIL ITS SET TO HANDLE
        GD.Print("face count at GETMESH: ", faces.Count);
        foreach (var face in faces)
        {
            
            var faceVerts = face.GetVerts();
            if (faceVerts.Count > 4)
            {
                // project roughly into 2d 

            }
            IndexTriangle(faceVerts[0], faceVerts[3], faceVerts[1]);
            IndexTriangle(faceVerts[1], faceVerts[3], faceVerts[2]);
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
        faces.Remove(face);
        foreach (var edge in face.EdgeList) edge.Face = null;
        
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

    public void SubdivideAll(int levels = 1)
    {
        for (int i = 0; i < levels; i++)
        {
            var faceList = faces.ToList();
            foreach (var face in faceList) Subdivide(face);
        }
        
    }

    // new subdivide call 
    public void _Subdivide()
    {
        // setup queue of faces to subdivide (SD), fill with all current faces 
        var subdivideQueue = faces.ToList();  

        int qPos = 0;
        while (qPos < subdivideQueue.Count)
        {
            var face = subdivideQueue[qPos];
            // face.GetArea();
        }
        
        
    }


    private void SplitEdgeMiddle(HalfEdge hEdge)
    {
        if (hEdge.HasChildren()) return;

        var twin = hEdge.TwinEdge;
        var midPos = (hEdge.EndVert.Position - hEdge.StartVert.Position) / 2 + hEdge.StartVert.Position;
        var mv = new PolyVertex(){Position = midPos};


        SplitHalfEdge(hEdge, mv);
        SplitHalfEdge(twin, mv);

        hEdge.childEdge1.SetAsTwinEdge(twin.childEdge2);
        hEdge.childEdge2.SetAsTwinEdge(twin.childEdge1);
        
    }

    private Face GenerateFace(HalfEdge EdgeOnFace)
    {
        var face = new Face();
        var curEdge = EdgeOnFace;
        var count = 0;
        var limit = 100;
        do
        {
            face.AddEdge(curEdge);
            
            curEdge.Face = face;
            curEdge = curEdge.NextEdge;
            if (curEdge == null)
            {
                GD.Print("edge not part of valid of face");
                break;
            }
            
            
            count ++;
            if (count > limit)
            {
                GD.Print("face not found, infinite loop?");
                break;
                
            }
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

        // if (hEdge.Face != null)
        // {
        //     var face = hEdge.Face;
        //     var eList = face.EdgeList;
        //     var idx = eList.IndexOf(hEdge);
        //     eList.RemoveAt(idx);
        //     eList.InsertRange(idx, [hEdge.childEdge1, hEdge.childEdge2]);
        // }

        
    }

    private void SubdivideFace(Face face)
    {
        // new subdivide that uses new SetNextEdge and ConnectEdge 
        // has rules for limiting subdivision along smaller edges
        
        var edges = face.EdgeList.ToList();
        var edgeCount = edges.Count;
        // add up perimeter, find longest edge and find centroid
        // use avg line length to decide which edges will get subdivided  
        var longEdge = edges[0];
        float perim = 0;
        Vector3 totalPos = new();
        var posList = new List<Vector3>();
        foreach (var edge in edges)
        {
            var len = edge.GetLen();
            perim += len;
            posList.Add(edge.StartVert.Position);
            totalPos += edge.StartVert.Position;
            longEdge = longEdge.GetLen() < len ? edge : longEdge; 
        }

        var areaAndCenter = GeometryUtils.AreaAndCenterOf3DPolyFace(posList);
        
        var avgLen = perim / edgeCount;
        var centroid = areaAndCenter.Item2;
        var targetLen = avgLen * 0.65f; 
        
        var subdivideEdges = new List<HalfEdge>(){longEdge};
        var curEdge =  longEdge;

        // set a buffer +- PI rads from initial subsivide, where split is forced and only
        // two splits are made total. the two edges are direct connected without middle vert
        var bufferRads = 0.65;

        for (int i = 0; i < edgeCount - 1; i++)
        {
            curEdge = curEdge.NextEdge;

            var lastSplitVec = subdivideEdges[^1].GetCenter() - centroid;
            var curSplitVec = curEdge.GetCenter() - centroid;
            var ang = lastSplitVec.AngleTo(curSplitVec);

            bool isTargetLen = curEdge.GetLen() >= targetLen;

            // check if candiate for special divide rules
            if (subdivideEdges.Count == 1 && isTargetLen) 
            {
                if (float.Abs(ang - float.Pi) < bufferRads)  // within buffer +- range
                {
                    subdivideEdges.Add(curEdge);
                    break;  
                }
            }

            if (ang < float.Pi)
            {
                if (isTargetLen) 
                {
                    subdivideEdges.Add(curEdge);
                }
            }
            else
            {
                subdivideEdges.Add(curEdge.PrevEdge);
            }

        }

        // split and connect edges 
        // special case if only two edges split
        if (subdivideEdges.Count == 2)
        {
            var e1 = subdivideEdges[0];
            var e2 = subdivideEdges[1];
            SplitEdgeMiddle(e1);
            SplitEdgeMiddle(e2);

            ConnectEdges(e1, e2, false);
            
        }




    }

    private void Subdivide(Face face)
    {
        
         
        RemoveFace(face);
        
        var parentEdges = face.GetParentEdges();
        var centerPos = face.WeightedCenter;

        var posSum = new Vector3();
        foreach(var vert in face.GetVertPositions())
        {
            posSum += vert;
        }
        posSum /= face.GetVertPositions().Count;


        HalfEdge pivotEdge = null;

        GD.Print ("weight center: ", centerPos, " avg pos: ", posSum);



        for (int i = 0; i < parentEdges.Count; i++)
        {
            var edge = parentEdges[i];  
            SplitEdgeMiddle(edge);

            if (i == 0) pivotEdge = CreateNextEdge(edge.childEdge1, centerPos);
            else
            {
                ConnectEdges(pivotEdge, edge.childEdge2, false);
            }

        }

        ResolveFaces();

    }





    // assumes edges are passed in winding order
    // connects edges through a new edge
    // sets twin edges connected as well
    HalfEdge GenerateConnectingEdge(HalfEdge edgeA, HalfEdge edgeB)
    {
        var conEdge = new HalfEdge(edgeA.EndVert, edgeB.StartVert);
        conEdge.SetTwinEdge();

        conEdge.SetNextEdge(edgeB);
        edgeA.SetNextEdge(conEdge);
        edgeB.TwinEdge.SetNextEdge(conEdge.TwinEdge);
        conEdge.TwinEdge.SetNextEdge(edgeA.TwinEdge);

        return conEdge;
    }

    void ShortenAndSeparateConnectedEdges(HalfEdge edge, float dist)
    {
        var eOut = edge.NextEdge;
        var eIn = edge.PrevEdge;

        var outStart = new PolyVertex(eOut.GetDirection() * dist + eOut.StartVert.Position);
        eOut.StartVert = outStart;
        eOut.TwinEdge.EndVert = outStart;

        var inEnd = new PolyVertex(eIn.GetDirection() * dist * -1 + eIn.EndVert.Position);
        eIn.EndVert = inEnd;
        eIn.TwinEdge.StartVert = inEnd;
        
    }

    


    private static void PairTwinEdges(HalfEdge a, HalfEdge b)
    {
        a.TwinEdge = b;
        b.TwinEdge = a;
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

    private static Vector3 GetOutwardVector(HalfEdge edge)
    {
        var tri = new Vector3[]
        {
            edge.StartVert.Position, edge.EndVert.Position, edge.NextEdge.EndVert.Position, 
        };
        var norm = GetNormal(tri);

        return edge.GetDirection().Cross(norm);

    }


    private HalfEdge ContinueEdgeToVertex(HalfEdge edge, PolyVertex newVert)
    {
        var newHalfEdge = new HalfEdge(edge.EndVert, newVert);
        edge.NextEdge = newHalfEdge;
        newHalfEdge.PrevEdge = edge;
        return newHalfEdge;
    }
   





}
