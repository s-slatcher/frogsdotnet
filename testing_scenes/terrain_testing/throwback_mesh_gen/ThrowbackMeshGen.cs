    using Godot;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security;
using System.Security.Cryptography;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class ThrowbackMeshGen : Node3D
{

    static float Mod(float x, float m)
    {
        return (x%m + m)%m;
    }

    float maxPolygonSize = 20;
    float sideLength = 2;
    int subdivisionLevel = 4;
    float quadSize = 0.25f;
    PolygonQuad faceQuad;
    Dictionary<Vector3, Vector3> VertexNormals = new();
    Dictionary<Vector2, PolygonQuad> LeafNodeCornerPoints = new();
    HashSet<PolygonQuad> LeafNodes = new();
    HashSet<PolygonQuad> EdgeNodes = new();
    Dictionary<Vector2I, Vector2> KeyMap = new();


    Vector2 RoundVec(Vector2 vec2)
    {
        return PolygonQuadMesh.RoundVector2(vec2);
    }

    Vector2I GetVectorKey(Vector2 vec)
    {
        var key = new Vector2I((int)Math.Round(vec.X * 3), (int)Math.Round(vec.Y * 3));
        if (!KeyMap.ContainsKey(key)) KeyMap[key] = vec;
        return key;
    }



    public override void _Ready()
    {

        // prepare polygon
        var polyNode = GetNode<Polygon2D>("Polygon2D");
        var polygon = polyNode.Polygon;
        var poly = PreparePolygon(polygon);


        if (!Geometry2D.IsPolygonClockwise(poly)) Array.Reverse(poly);

        var quad = PolygonQuad.CreateRootQuad(poly, quadSize);
        faceQuad = quad;
        SubdivideMainFace(quad);

        var interpPoly = InterpPolyEdge(poly);
        // var Line2d = new Line2D();
        // AddChild(Line2d);
        // Line2d.Width = 0.5f;
        // Line2d.Points = interpPoly;
        // Line2d.Scale = new Vector2(10, 10);

        // var staticBody = new StaticBody3D();
        // staticBody.Translate(new Vector3(0, 0, 1));
        // AddChild(staticBody);
        // foreach (var node in EdgeNodes)
        // {
        //     var collisionPoly = new CollisionPolygon3D() { Polygon = node.Polygons[0] };
        //     staticBody.AddChild(collisionPoly);
        // }

        // meshify the polygon
        var mesh = GenerateMesh(interpPoly);
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

    // returns false if both x and y sit on a grid corner, returns true otherwise (one or both axis are unaligned) 
    bool IsEdgePoint(Vector2 point)
    {
        return !(Math.Abs(point.X % quadSize) < 0.001 && Math.Abs(point.Y % quadSize) < 0.001 );  
    }
     private bool PointIsOnEdge(Vector2 point)
    {
        return point.X % quadSize == 0 && point.Y % quadSize == 0;
    }

    Vector2[] InterpPolyEdge(Vector2[] polygon)
    {
        var newPoly = new List<Vector2>();
        var len = polygon.Length;

        for (int i = 0; i < len; i++)
        {
            var p1 = polygon[i];
            var p2 = polygon[i == len - 1 ? 0 : i + 1];

            var dirVec = p2 - p1;

            // convert line into a range of x-y values from lowest-highest both axis
            var rangeRect = new Rect2() { Position = p1, End = p2 };
            rangeRect = rangeRect.Abs();
            var rect_pos = rangeRect.Position;
            

            // define new start point for range aligned to grid, but clamped into range
            var startPoint = new Vector2
            (
                rect_pos.X - Mod(rect_pos.X, quadSize) + quadSize,
                rect_pos.Y - Mod(rect_pos.Y, quadSize) + quadSize
            );

            GD.Print("range start: ", rect_pos, " adjusted start: ", startPoint, "   full range: ", rangeRect.Size);
            
            rangeRect.Position = startPoint;
            rect_pos = rangeRect.Position;
            var range = rangeRect.Size;

            var intervals = new Vector2I((int)(range.X / quadSize), (int)(range.Y / quadSize));

            var newPoints = new List<Vector2>() { p1 };

            

            for (int k = 0; k < intervals.X; k++)
            {
                var interval_add = k * quadSize;
                var x_val = startPoint.X + interval_add;
                var vec = p2 - p1;
                var x_vec = x_val - p1.X;
                var ratio = x_vec / vec.X;
                var vec_point = ratio * vec;
                var trans_point = vec_point + p1;
                newPoints.Add(trans_point);
            }
            for (int k = 0; k < intervals.Y; k++)
            {
                var interval_add = k * quadSize;
                var y_val = startPoint.Y + interval_add;
                var vec = p2 - p1;
                var y_vec = y_val - p1.Y;
                var ratio = y_vec / vec.Y;
                var vec_point = ratio * vec;
                var trans_point = vec_point + p1;
                newPoints.Add(trans_point);
            }

            newPoints = newPoints.OrderBy(p => p.DistanceSquaredTo(p1)).ToList();
            newPoly.AddRange(newPoints);
        }
        return newPoly.ToArray();
    }

    Vector2[] GetPolyPerimeter()
    {
        var currentPoint = Vector2I.Zero;
        var EdgeGraph = new Dictionary<Vector2I, HashSet<Vector2I>>();
        GD.Print("total edge nodes: ", EdgeNodes.Count);
        foreach (var node in EdgeNodes)
        {
            var poly = node.Polygons[0];
            if (node.Polygons.Count > 1) GD.Print("multipoly");

            for (int i = 0; i < poly.Length; i++)
            {
                var p1 = poly[i];
                var p2_i = i == poly.Length - 1 ? 0 : i + 1;
                var p2 = poly[p2_i];

                var p1_key = GetVectorKey(p1);
                var p2_key = GetVectorKey(p2);
                //skip if either point is a grid corner
                
                if (!IsEdgePoint(p1) || !IsEdgePoint(p2)) continue;

                if (EdgeGraph.ContainsKey(p1_key)) EdgeGraph[p1_key].Add(p2_key);
                else EdgeGraph[p1_key] = new() { p2_key };

                if (EdgeGraph.ContainsKey(p2_key)) EdgeGraph[p2_key].Add(p1_key);
                else EdgeGraph[p2_key] = new() { p1_key };

                if (currentPoint == Vector2I.Zero) currentPoint = p1_key;
            }
        }

        var buggedPoints = EdgeGraph.Keys.Where((key) => EdgeGraph[key].Count != 2).ToList();
        GD.Print("num of bugged points: ", buggedPoints.Count);
        var zeroNeighbors = buggedPoints.Where(key => EdgeGraph[key].Count == 0).ToList();
        GD.Print("zero neighbors: ", zeroNeighbors.Count);

        List<Vector2> polygon = new() {};
        HashSet<Vector2> visited = new();
        
        
        while (!visited.Contains(currentPoint))
        {
            visited.Add(currentPoint);
            polygon.Add(KeyMap[currentPoint]);
            var nextPoints = EdgeGraph[currentPoint];
            if (nextPoints.Count != 2) GD.Print("neighbor point count:" , nextPoints.Count);
            currentPoint = nextPoints.FirstOrDefault(p => !visited.Contains(p));
            if (currentPoint == default) break;

            if (polygon.Count > 10000) { GD.Print("broke from infinite while"); break; }

        }
        GD.Print(polygon.Count);
        return polygon.ToArray();



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
                if (quad.HasEdgePoly()) EdgeNodes.Add(quad); 
                
                leafNodes.Add(quad);
            }

        }

        LeafNodes = leafNodes;

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
            if (GD.Randf() < 0.05) GD.Print(p);
            var prev_p = i == 0 ? polygon[^1] : polygon[i - 1];
            bool p_is_gridPoint = PointIsOnEdge(p);
            bool prev_p_is_gridPoint = PointIsOnEdge(prev_p);
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
            faces = faces.SelectMany(SubdivideFace).ToList();
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

