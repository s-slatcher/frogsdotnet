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

    public void SubdivideAll(int levels = 1)
    {
        _Subdivide(0.1f);   

        
        var faceList = faces.ToList();

        // for (int i = 0; i < levels; i++)
        // {
        //     faceList = faces.ToList();
        //     foreach (var face in faceList) 
        //     {
        //         if (face.Normal.Y > 0.5) Subdivide(face);
        //     }
        // }

        // for (int i = 0; i < 2; i++)
        // {
        //     faceList = faces.ToList();
        //     foreach (var face in faceList) 
        //     {
        //         if (face.Normal.Y < 0.5) Subdivide(face);
        //     }
        // }
        
        

        
    }

    // new subdivide call 
    public void _Subdivide(float maxArea)
    {
        // setup queue of faces to subdivide (SD), fill with all current faces 
        var subdivideQueue = faces.ToList();  

        int qPos = 0;
        while (qPos < subdivideQueue.Count)
        {
            
            var face = subdivideQueue[qPos];
            qPos++;

            if (face.Area > maxArea) subdivideQueue.AddRange(Subdivide(face));
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

        // set a buffer +- PI rads from initial subdivide, where split is forced and only
        // two splits are made total. the two edges are direct connected without middle vert
        var bufferRads = 0.65;

        for (int i = 0; i < edgeCount - 1; i++)
        {
            curEdge = curEdge.NextEdge;

            var lastSplitVec = subdivideEdges[^1].GetCenter() - centroid;
            var curSplitVec = curEdge.GetCenter() - centroid;
            var ang = lastSplitVec.AngleTo(curSplitVec);

            bool isTargetLen = curEdge.GetLen() >= targetLen;

            // check if candidate for special divide rules
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

    private List<Face> Subdivide(Face face)
    {
        
         
        RemoveFace(face);
        
        var edges = face.GetEdges();
        var centerPos = face.WeightedCenter;
        var centerSum = new Vector3();
        foreach(var edge in edges) centerSum+= edge.StartVert.Position;
        centerSum/= edges.Count;

        HalfEdge pivotEdge = null;  

        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];  
            SplitEdgeMiddle(edge);

            if (i == 0) pivotEdge = CreateNextEdge(edge.childEdge1, centerSum);
            else
            {
                ConnectEdges(pivotEdge, edge.childEdge2, false);
            }

        }

        ResolveFaces();
        var childFaces = edges.Select(e => e.childEdge1.Face ).ToList();
        return childFaces;
        

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
