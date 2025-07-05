using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public partial class PolygonQuadMesh : GodotObject
{


    public const int VectorRoundingDecimal = 3;

    public PolygonQuad RootQuad;
    public Rect2I BoundingRect;
    public Dictionary<Vector2, IndexedVertex> Vector2ToVertexMap;


    public List<Mesh> Meshes = new();
    public int TargetMeshSize = 8;
    public float MeshSize;

    private GeometryUtils gUtils = new();

    private int leafNodeCounter = 0;

    public float triangulationTimeCount = 0;


    static Vector2 RoundVector2(Vector2 vec2, int roundingDecimal)
    {
        return new Vector2(float.Round(vec2.X, roundingDecimal), float.Round(vec2.Y, roundingDecimal));
    }

    private void SetMeshSize()
    {
        var maxQuadWidthExponent = Math.Log2(TargetMeshSize / RootQuad.MinimumQuadWidth);
        var roundedExponent = Math.Round(maxQuadWidthExponent);
        MeshSize = RootQuad.MinimumQuadWidth * (float)Math.Pow(2, roundedExponent);

    }

    public PolygonQuadMesh(Vector2[] polygon, float minimumQuadWidth = 0.25f)
    {
        
        RootQuad = PolygonQuad.CreateRootQuad(polygon, minimumQuadWidth);
        Vector2ToVertexMap = new();
        BoundingRect = gUtils.RectIFromPolygon(polygon);
        SetMeshSize();
        

    }




    public PolygonQuadMesh(PolygonQuadMesh polyMesh)
    {
        var time = Time.GetTicksMsec();
        // constructor that shallow duplicates another polymesh by ref to same quad tree
        // duplicates the list of vertices and their mappings to 2d vectors
        RootQuad = polyMesh.RootQuad.Duplicate();
        Vector2ToVertexMap = new(polyMesh.Vector2ToVertexMap);
        BoundingRect = polyMesh.BoundingRect;
        MeshSize = polyMesh.MeshSize;
        GD.Print("duplicate quad mesh time: ", Time.GetTicksMsec() - time);
        
    }


    public void IndexPoint(Vector2 polygonPoint, Vector3 vertexPosition)
    {
        var key = RoundVector2(polygonPoint, VectorRoundingDecimal);
        var vertex = new IndexedVertex() { SourcePosition = polygonPoint, Position = vertexPosition };
        // VertexList.Add(vertex);
        // vertex.ArrayIndex = VertexList.Count - 1;
        Vector2ToVertexMap[RoundVector2(polygonPoint, VectorRoundingDecimal)] = vertex;
    }

    public Vector3? GetVertex(Vector2 point)
    {
        if (Vector2ToVertexMap.ContainsKey(RoundVector2(point, VectorRoundingDecimal)))
        {
            return Vector2ToVertexMap[RoundVector2(point, VectorRoundingDecimal)].Position;
        }
        return null;
    }

    
    public Dictionary<Rect2, Mesh> GenerateMeshes(Rect2? region = null)
    {


        var meshStartQuads = GetQuadsAtTargetDepth(RootQuad, MeshSize);
        if (region != null)
        {
            meshStartQuads = meshStartQuads.Where(quad => quad.BoundingRect == region).ToList();
            if (meshStartQuads.Count == 0) GD.Print("region mesh size not found: ", region);
        }

        var meshMap = new Dictionary<Rect2, Mesh>();
        // GD.Print(VertexList.Count);
        

        foreach (PolygonQuad quad in meshStartQuads)
        {
            var mesh = GenerateMeshFromQuad(quad);
            meshMap[quad.BoundingRect] = mesh;
        }


        return meshMap;
    }
    

    int Mod(int x, int m) {
        return (x%m + m)%m;
    }

    public Mesh GenerateMeshFromQuad(PolygonQuad quad)
    {
        // searches max depth to collect all leaf nodes that descend from passed quad
        var tri_time = Time.GetTicksMsec();

        var leafNodes = GetQuadsAtTargetDepth(quad, 0);
        leafNodeCounter += leafNodes.Count;

        var vertexIndices = leafNodes.SelectMany(quad => TriangulateQuad(quad).ToList()).ToList();

        triangulationTimeCount += Time.GetTicksMsec() - tri_time;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);


        Vector3 triCenter = new();
        for (int i = 0; i < vertexIndices.Count; i++)
        {
            var vertex = vertexIndices[i];

            if (Mod(i, 3) == 0)  triCenter = GetTriangleCenter(vertex, vertexIndices[i+1], vertexIndices[i+2]) / 100;

            var UV = (new Vector2(vertex.Position.X, vertex.Position.Y)) / BoundingRect.Size;
            // if (UV.X > 1 || UV.Y > 1) GD.Print(vertex.Position);
            st.SetUV(UV);
            st.SetColor(new Godot.Color(triCenter.X, triCenter.Y, triCenter.Z, 1));
            st.AddVertex(vertex.Position);
        }

        st.Index();
        st.GenerateNormals();

        return st.Commit();
    }

    private Vector3 GetTriangleCenter(IndexedVertex vert1, IndexedVertex vert2, IndexedVertex vert3)
    {
        Vector3 center = Vector3.Zero;

        center += vert1.Position;
        center += vert2.Position;
        center += vert3.Position;
        return center / 3;
    }

    private List<IndexedVertex> TriangulateQuad(PolygonQuad quad)
    {

        var totalVertices = new List<IndexedVertex>();
        var polygon = quad.Polygons[0];
        if (polygon.Length == 0) return totalVertices;

        var stitchPolygon = StitchQuadPolygon(quad);
        // var stitchPolygon = quad.Polygons[0];  

        var triangleIndices = Geometry2D.TriangulatePolygon(stitchPolygon);
        triangleIndices = triangleIndices.Reverse().ToArray();
        var vertices = new List<IndexedVertex>();
        foreach (var index in triangleIndices)
        {
            var polyPoint = stitchPolygon[index];
            var key = RoundVector2(polyPoint, VectorRoundingDecimal);
            vertices.Add(Vector2ToVertexMap[key]);
        }

        totalVertices.AddRange(vertices);
        return totalVertices;
    }

    private Vector2[] StitchQuadPolygon(PolygonQuad quad)
    {

        var stitchedPolygon = new List<Vector2>();
        var region = quad.BoundingRect;
        var polygon = quad.Polygons[0];
        var minWidth = quad.MinimumQuadWidth;

        if (region.Size.X == minWidth) return polygon;

        // only tries stitching on poly sides match the grid (works for now, but will be made more robust in future)

        var gridPoints = gUtils.PolygonFromRect(region);


        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 p1 = polygon[i];
            Vector2 p2 = i + 1 < polygon.Length ? polygon[i + 1] : polygon[0];

            stitchedPolygon.Add(p1);

            if (!gridPoints.Contains(p1) || !gridPoints.Contains(p2)) continue;

            var sharedAxis = p1.X == p2.X ? 0 : 1;
            var otherAxis = sharedAxis == 0 ? 1 : 0;
            var stitchVector = sharedAxis == 0 ? new Vector2(0, minWidth) : new Vector2(minWidth, 0);
            stitchVector *= p1[otherAxis] < p2[otherAxis] ? 1 : -1;

            // loop over the possible stitch positions based on the grid dimensions
            // if those points exist already in other faces, stitch them into this face 
            var stitchPositions = float.Round(region.Size.X / minWidth) - 1;
            for (int j = 0; j < stitchPositions; j++)
            {
                var pos = p1 + (stitchVector * (j + 1));
                var key = RoundVector2(pos, VectorRoundingDecimal);
                if (Vector2ToVertexMap.ContainsKey(key)) stitchedPolygon.Add(pos);

            }

        }
        return stitchedPolygon.ToArray();
    }

    public List<Vector2[]> GetPolygons()
    {
        var polyList = new List<Vector2[]>();

        var leafNodes = GetQuadsAtTargetDepth(RootQuad, 0);
        GD.Print(leafNodes.Count);
        foreach (PolygonQuad node in leafNodes)
        {
            polyList.Add(node.Polygons[0]);
        }


        return polyList;
    }

    public List<PolygonQuad> GetQuadsAtTargetDepth(PolygonQuad startQuad, float widthAtTargetDepth = 0)
    {
        List<PolygonQuad> quadList = new();

        var queue = new List<PolygonQuad>() { startQuad };
        var queuePos = 0;

        while (queuePos < queue.Count)
        {
            var quad = queue[queuePos];
            queuePos += 1;

            if (quad.GetWidth() <= widthAtTargetDepth)
            {
                quadList.Add(quad);
                continue;
            }
            if (quad.HasChildren()) queue.AddRange(quad.GetChildren());
            else
            {
                quadList.Add(quad);
                if (widthAtTargetDepth == MeshSize) GD.Print("off-sized mesh: ", quad.GetWidth());
            }   
        }
        
        return quadList;
    }

    public void DeIndexPoint(Vector2 point)
    {
        Vector2ToVertexMap.Remove(RoundVector2(point, VectorRoundingDecimal));
    }

}
