    using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class ThrowbackMeshGen : Node3D
{

    float maxPolygonSize = 10;
    float sideLength = 2;
    int subdivisionLevel = 4;
    float quadSize = 0.25f;
    PolygonQuad faceQuad;
    Dictionary<Vector3, Vector3> VertexNormals = new();
    Dictionary<Vector2, PolygonQuad> LeafNodeCornerPoints = new();
    HashSet<PolygonQuad> LeafNodes = new();

    public override void _Ready()
    {

        // prepare polygon
        var polyNode = GetNode<Polygon2D>("Polygon2D");
        var polygon = polyNode.Polygon;
        polygon = PreparePolygon(polygon);

        if (!Geometry2D.IsPolygonClockwise(polygon)) Array.Reverse(polygon);

        var quad = PolygonQuad.CreateRootQuad(polygon, quadSize);
        faceQuad = quad;
        SubdivideMainFace(quad);


        // var csg = new CsgPolygon3D() { Polygon = polygon };
        // AddChild(csg);
        // var mesh = (Mesh)csg.GetMeshes()[1];


        // meshify the polygon
        var mesh = GenerateMesh(polygon);
        var meshInst = GetNode<MeshInstance3D>("MeshInstance3D");
        meshInst.Mesh = mesh;

        // edit existing mesh created by csgPolygon class
        mesh.SurfaceGetArrays(0);


        // foreach (var vec in LeafNodeCornerPoints.Keys)
        // {
        //     var meshInstance = new MeshInstance3D();
        //     meshInstance.Mesh = new SphereMesh(){Radius = 0.05f, Height = 0.1f};
        //     meshInstance.Position = D(vec, 0);
        //     AddChild(meshInstance);
        // }

    }

    private void SubdivideMainFace(PolygonQuad rootQuad)
    {
        // associate each edge leaf node with its first point in the winding order
        // then when reconstructing the polygon edge, use this map find adjacent leaf nodes to by getting the point on the opposite corner

        // run through and subdivide entire mesh, storing leaf nodes 

        var leafNodes = new HashSet<PolygonQuad>();
        
        var queue = new List<PolygonQuad>() { rootQuad };
        var pos = 0;

        while (pos < queue.Count)
        {
            var quad = queue[pos];
            pos++;

            if (quad.GetWidth() > quadSize)
            {
                quad.Subdivide();
                queue.AddRange(quad.GetChildren());
            }
            else
            {

                if (quad.HasEdgePoly())
                {
                    var poly = SortPolygonClockwise(quad.Polygons[0]);
                    quad.Polygons[0] = poly;
                    LeafNodeCornerPoints[poly[0]] = quad;

                }
                else Array.Reverse(quad.Polygons[0]);
                leafNodes.Add(quad);
            }

        }

        LeafNodes = leafNodes;

    }

    private bool pointOnEdge(Vector2 point)
    {
        return point.X % quadSize == 0 && point.Y % quadSize == 0;
    }

    private Vector2[] SortPolygonClockwise(Vector2[] polygon)
    {
        List<Vector2> sorted = new();

        bool clockwise = Geometry2D.IsPolygonClockwise(polygon);
        if (!clockwise) Array.Reverse(polygon);

        int firstEdgeIndex = -1;


        for (int i = 0; i < polygon.Length; i++)
        {
            var p = polygon[i];
            var prev_p = i == 0 ? polygon[^1] : polygon[i - 1];
            bool p_is_gridPoint = pointOnEdge(p);
            bool prev_p_is_gridPoint = pointOnEdge(prev_p);
            bool isFirstEdgePoint = !p_is_gridPoint && prev_p_is_gridPoint;

            if (isFirstEdgePoint)
            {
                firstEdgeIndex = i;
                break;
            }
        }

        if (firstEdgeIndex == -1) return polygon;
        for (int i = firstEdgeIndex; i < polygon.Length + firstEdgeIndex; i++) sorted.Add(polygon[i % polygon.Length]);


        return sorted.ToArray();
    }

    private Mesh GenerateMesh(Vector2[] polygon)
    {
        var st = new SurfaceTool();

        var faces = new List<List<Vector3>>();
        var totalVertices = new List<Vector3>();


        for (int i = 0; i < polygon.Length; i++)
        {

            var p1 = D(polygon[i], 0);
            var p2_index = i == polygon.Length - 1 ? 0 : i + 1;
            var p2 = D(polygon[p2_index], 0);
            var p0_index = i == 0 ? polygon.Length - 1 : i - 1;
            var p0 = D(polygon[i], 0);

            var p3 = p2 + new Vector3(0, 0, -1);
            var p4 = p1 + new Vector3(0, 0, -1);

            // get data for this and previous edge to get one sides normal vectors
            var face_normal_0 = (p1 - p0).Rotated(Vector3.Back, float.Pi / 2).Normalized();
            var face_normal_1 = (p2 - p1).Rotated(Vector3.Back, float.Pi / 2).Normalized();


            var p1_normal = (Vector3.Back + face_normal_0 + face_normal_1).Normalized();
            var p4_normal = (Vector3.Forward + face_normal_0 + face_normal_1).Normalized();
            VertexNormals[p1] = p1_normal;
            VertexNormals[p4] = p4_normal;

            faces.Add(new() { p2, p1, p4, p3 });


            // Vector3[] tri_verts = [p2, p1, p4, p4, p3, p2];

            // totalVertices.AddRange(tri_verts);
        }

        for (int i = 0; i < subdivisionLevel; i++)
        {
            faces = faces.SelectMany(face => SubdivideFace(face)).ToList();
        }
        foreach (var face in faces)
        {
            totalVertices.AddRange(VerticesFromFace(face));
        }

        foreach (var node in LeafNodes)
        {
            var poly = node.Polygons[0];
            if (poly.Length == 4) totalVertices.AddRange(VerticesFromFace(poly.Select(p => D(p, 0)).ToList()));
            else totalVertices.AddRange(VerticesFromFrontEdgePoly(poly));
            
        }

        // var mainIndexes = Geometry2D.TriangulatePolygon(polygon);
            // GD.Print(mainIndexes.Length);
            // for (int i = 2; i < mainIndexes.Length; i += 3)
            // {

            //     List<int> i_list = [i - 2, i - 1, i];
            //     Vector2[] tri = i_list
            //         .Select(index => mainIndexes[index])
            //         .Select(index => polygon[index])
            //         .ToArray();



            //     Array.Reverse(tri);
            //     bool isClockwise = Geometry2D.IsPolygonClockwise(tri);
            //     if (isClockwise) GD.Print("clockwise tri");
            //     else GD.Print("counter tri");

            //     totalVertices.AddRange(tri.Select(p => D(p, 0)).ToList());

            // }

            st.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var p in totalVertices)
        {
            var normal = VertexNormals.ContainsKey(p) ? VertexNormals[p] : Vector3.Back;
            st.SetNormal(normal);
            st.SetColor(ColorFromNormal(normal));
            st.AddVertex(p);

        }
        // st.GenerateNormals();
        var mesh = st.Commit();
        return mesh;

    }

    List<Vector3> VerticesFromFace(List<Vector3> face)
    {
         return [face[0], face[1], face[2], face[2], face[3], face[0]];
        
    }

    List<Vector3> VerticesFromFrontEdgePoly(Vector2[] poly)
    {
        
        var indices = Geometry2D.TriangulatePolygon(poly).Reverse();
        var vec3s = indices.Select(i => D(poly[i], 0));
        return vec3s.ToList();

    }

    List<List<Vector3>> SubdivideFace(List<Vector3> face)
    {
        // face array is 4 points starting at top right (when viewed head on), going clockwise
        var newFaces = new List<List<Vector3>>();

        var midPoint1 = (face[0] + face[3]) / 2;
        var midPoint2 = (face[1] + face[2]) / 2;

        VertexNormals[midPoint1] = VertexNormals[face[0]] + VertexNormals[face[3]].Normalized();
        VertexNormals[midPoint2] = VertexNormals[face[1]] + VertexNormals[face[2]].Normalized();

        newFaces.Add(new() { face[0], face[1], midPoint2, midPoint1 });
        newFaces.Add(new() { midPoint1, midPoint2, face[2], face[3] });
        return newFaces;

    }

    Godot.Color ColorFromNormal(Vector3 normal)
    {
        var color_norm = (normal / 2) + new Vector3(0.5f, 0.5f, 0.5f);
        return new Godot.Color(color_norm.X, color_norm.Y, color_norm.Z, 1);

    }

    Vector3 D(Vector2 point, float depth)
    {
        return new Vector3(point.X, point.Y, depth);
    }

    Vector2[] PreparePolygon(Vector2[] polygon)
    {

        var minX = 9999f;
        var minY = 9999f;
        var maxX = -9999f;
        var maxY = -9999f;

        foreach (var point in polygon)
        {
            minX = Math.Min(point.X, minX);
            maxX = Math.Max(point.X, maxX);
            minY = Math.Min(point.Y, minY);
            maxY = Math.Max(point.Y, maxY);
        }

        var minVec = new Vector2(minX, minY);
        var maxVec = new Vector2(maxX, maxY);
        var size = maxVec - minVec;

        var xRatio = size.X / maxPolygonSize;
        var yRatio = size.Y / maxPolygonSize;

        var scale = 1f / Math.Max(xRatio, yRatio);

        var transformedPoly = polygon.Select(point => (point - minVec) * scale);

        return transformedPoly.ToArray();


    }

}

