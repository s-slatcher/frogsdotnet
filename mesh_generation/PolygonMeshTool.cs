using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Vector3 = Godot.Vector3;
using Vector2 = Godot.Vector2;

public partial class PolygonMeshTool : GodotObject
{
    HashSet<Face> faces = new();
    Dictionary<Vector2, PolyVertex> vertexMap = new();


    public void SetPolygon(NormalPoly poly)
    {
        var polygon = poly.Polygon;
        var len = polygon.Length;
        
        // convert points into 3d vertices
        // assign to HalfEdges
        // produce initial 'face' with forward vector

        var face = new Face()
        {
            Basis = new Basis(Vector3.Right, 0)
        };

    

        faces.Add(face);

        foreach (LinkedPoint linkedPoint in poly.GetIterator())
        {
            var p1 = linkedPoint.current; 
            var p2 = linkedPoint.next;
            

            var vert1 = GetPointVertex(p1, face);
            var vert2 = GetPointVertex(p2, face);

            var hEdge = new HalfEdge()
            {
                StartVert = vert1,
                EndVert = vert2,
                Face = face,
            };
            
            vert1.LeadingHalfEdges.Add(hEdge);

            face.AddEdge(hEdge);   
        }
        GD.Print(vertexMap.Count, poly.Polygon.Length);
        
        
    }

    

    public PolyVertex GetPointVertex(Vector2 point, Face face)
    {
        PolyVertex vert;
        if (vertexMap.TryGetValue(point, out vert))
        {
            return vert;
        }
        else
        {
            Vector3 pos = new(point.X, point.Y, 0);
            vert = new PolyVertex(){Position = pos * face.Basis};
            vertexMap.Add(point, vert);
        }
        return vert;
    }

    public Mesh GetMesh()
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var face = faces.ToList()[0];
        
        var tris = TriangulateFace(face);
        foreach (var vert in tris.SelectMany(vert => vert)) AddVertex(vert, face, st, true);
        
        
        // st.SetNormal(Vector3.Forward);
        // st.AddVertex(Vector3.Zero);
        // st.SetNormal(Vector3.Forward);
        // st.AddVertex(new Vector3(10,0,0));
        // st.SetNormal(Vector3.Forward);
        // st.AddVertex(new Vector3(5,10,0));
        
        return st.Commit();
    }

    public void AddVertex(PolyVertex vert, Face face, SurfaceTool st, bool PerFaceNormal = true)
    {
        st.SetNormal(face.GetNormal());
        st.AddVertex(vert.Position);
    }

    public List<PolyVertex[]> TriangulateFace(Face face)
    {

        return face.TriangulateEdge().ToList();
    }




}
