    using Godot;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
using Vector4 = Godot.Vector4;

public partial class ThrowbackMeshGen : Node3D
{

    static float Mod(float x, float m)
    {
        return (x%m + m)%m;
    }
    static int Mod(int x, int m)
    {
        return (x%m + m)%m;
    }

    float maxPolygonSize = 10;
    float terrainHeight = 80;
    float maxSideLength = 10;
    float sideLength = 0.5f;
    int subdivisionLevel = 4;
    float quadSize = 0.25f;
    float edgeSmooth = 0.20f;
    PolygonQuad faceQuad;
    Dictionary<Vector3, Vector3> VertexNormals = new();
    Dictionary<Vector2, PolygonQuad> LeafNodeCornerPoints = new();
    HashSet<PolygonQuad> LeafNodes = new();
    HashSet<PolygonQuad> EdgeNodes = new();
    Dictionary<Vector2I, Vector2> KeyMap = new();

    List<IndexedVertex> VertexList = new();
    List<IndexedVertex> EdgeVertices = new();
    Dictionary<PolygonQuad, List<IndexedVertex>> EdgeSmoothingMap = new();

    ShaderMaterial shaderMaterial;

    int explosion_count = 0;
    private Vector4[] ShaderArray;

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

    public void AddDebugPath(List<Vector3> points)
    {
        var curve = new Curve3D();
        foreach (var p in points) curve.AddPoint(p);
        var pathNode = new Path3D() { Curve = curve };
        AddChild(pathNode);
    }
    public void AddDebugLabel(Vector3 pos, string text)
    {
        var label = new Label3D() { Text = text, Position = pos, FontSize = 12 };
        AddChild(label);
    }


    public override void _Ready()
    {
       

        PlaneMouseCapture planeCap = GetNode<PlaneMouseCapture>("PlaneMouseCapture");
        planeCap.PlaneClicked += OnPlaneClicked; 

        var time = Time.GetTicksMsec();
       
        // prepare polygon
        var polyNode = GetNode<Polygon2D>("Polygon2D");
        var poly = PreparePolygon(polyNode.Polygon);
        
        // var terrain = new TerrainMap(20);
        // terrain.MaxHeight = terrainHeight;
        
        // var terrainPoly = terrain.GenerateNext(100)[0].Polygon;
        // poly = terrainPoly;

        if (!Geometry2D.IsPolygonClockwise(poly)) Array.Reverse(poly);


        // interpolate edge 
        
        var interpPoly = InterpPolyEdge(poly);

        polyNode.Polygon = interpPoly;

        var totalIndices = new List<int>();
        totalIndices.AddRange(GenerateSideFaces(interpPoly));
        totalIndices.AddRange(GenerateFrontFace(poly));

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbFloat);

        foreach (var idx in totalIndices)
        {
            var vert = VertexList[idx];
            st.SetNormal(vert.Normal);
            st.SetCustom(0, vert.Custom0);
            // st.SetColor(ColorFromNormal(normal));
            st.AddVertex(vert.Position);

        }
        st.Index();
        // foreach (var vert in frontFaceIndices)
        // {
        //     st.SetNormal(vert.Normal);
        //     st.SetCustom(0, vert.Custom0);
        //     st.AddVertex(vert.Position);
        // }

        // st.GenerateNormals();
        var mesh = st.Commit();

        var meshInst = GetNode<MeshInstance3D>("MeshInstance3D");
        meshInst.Mesh = mesh;
        GD.Print("total mesh time: ", Time.GetTicksMsec() - time);

        shaderMaterial = meshInst.MaterialOverride as ShaderMaterial;
        shaderMaterial.SetShaderParameter("ledge_depth_max", sideLength * 20);
        shaderMaterial.SetShaderParameter("ledge_depth_min", sideLength);
        shaderMaterial.SetShaderParameter("ledge_depth_height_max", terrainHeight);

        //set up explosion array
        Godot.Vector4[] shaderArray = new Godot.Vector4[256];
        ShaderArray = shaderArray;
        // shaderArray[0] = new Vector4(20, 16, 0, 5);
        shaderMaterial.SetShaderParameter("explosion_array", shaderArray);
        
        for (int i = 0; i < 250; i++)
        {
            // ExplodeTerrain(new Vector3(GD.RandRange(0, 50), GD.RandRange(0, 50), 0));
        }

    }

    private void OnPlaneClicked(Vector3 vector)
    {
        ExplodeTerrain(vector);
    }

    private void ExplodeTerrain(Vector3 vector)
    {
        var explosion = new Vector4(vector.X, vector.Y, 0, (float)GD.RandRange(2, 3));
        if (explosion_count > 255) return; 
        ShaderArray[explosion_count] = explosion;
        shaderMaterial.SetShaderParameter("explosion_array", ShaderArray);
        explosion_count++;
    }

    // returns false if both x and y sit on a grid corner, returns true otherwise (one or both axis are unaligned) 




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


            // // add a debug label in scene
            // var midPoint = D(p1.Lerp(p2, 0.5f), 0);
            // var norm = D(p2 - p1.Rotated(float.Pi / 2), 0).Normalized();
            // var labelPos = midPoint + norm * 0.5f;
            // AddDebugLabel(labelPos, i.ToString());





            // define new start point for range aligned to grid, but clamped into range
            var startPoint = new Vector2
        (
            rect_pos.X - Mod(rect_pos.X, quadSize) + quadSize,
            rect_pos.Y - Mod(rect_pos.Y, quadSize) + quadSize
        );

            // if (startPoint.DistanceSquaredTo(p1) > p1.DistanceSquaredTo(p2))
            // {
            //     newPoly.Add(p1);
            //     continue;
            // }

            rangeRect = new Rect2(startPoint, rangeRect.End - startPoint);
            // rangeRect.Position = startPoint;
            var range = rangeRect.Size;


            var intervals = new Vector2I((int)(range.X / quadSize) + 1, (int)(range.Y / quadSize) + 1);

            var newPoints = new List<Vector2>() { p1 };

            if (range.X < 0) intervals.X = 0;
            if (range.Y < 0) intervals.Y = 0;

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
            for (int j = 0; j < intervals.Y; j++)
            {
                var interval_add = j * quadSize;
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

    

    private void SubdivideMainFace(PolygonQuad rootQuad)
    {
        
        // refine list of edge vertices within range of a node, unless value is zero then 
        EdgeSmoothingMap[rootQuad] = EdgeVertices;

        var leafNodes = new HashSet<PolygonQuad>();
        var queue = new List<PolygonQuad>() { rootQuad };
        var pos = 0;

        while (pos < queue.Count)
        {
            var quad = queue[pos];
            var edgeSmoothPoints = EdgeSmoothingMap[quad];
            pos++;

            if (quad.GetWidth() > quadSize)
            {
                quad.Subdivide();
                var children = quad.GetChildren();

                foreach (var child in children)
                {
                    // refine nodes nearby edge vertices list for each child's smaller bounding rect
                    var rect = child.BoundingRect.Grow(edgeSmooth);
                    EdgeSmoothingMap[child] = edgeSmoothPoints.Where(vert => rect.HasPoint(new Vector2(vert.Position.X, vert.Position.Y))).ToList();
                }
            
                queue.AddRange(quad.GetChildren());
            }
            else
            {
                if (quad.HasEdgePoly()) EdgeNodes.Add(quad);
                leafNodes.Add(quad);
            }

        }
        GD.Print("total leaf nodes: ", leafNodes.Count);
        LeafNodes = leafNodes;

    }







    private List<int> GenerateSideFaces(Vector2[] polygon)
    {


        var edgeLines = new List<List<IndexedVertex>>();


        // double the quad density on surface, will fix or make more dynamic in future (less density on flat top of surface) 
        float halfQuad = quadSize * 0.5f;



        var len = polygon.Length;
        for (int i = 0; i < len; i++)
        {
            var p0 = D(polygon[Mod(i - 1, len)], 0);
            var p1 = D(polygon[i],0);
            var p2 = D(polygon[Mod(i + 1, len)],0);

            var faceNorm1 = (p1 - p0).Rotated(Vector3.Back, float.Pi / 2).Normalized();
            var faceNorm2 = (p2 - p1).Rotated(Vector3.Back, float.Pi / 2).Normalized();

            var sideFaceNormAvg = (faceNorm1 + faceNorm2) / 2;
            var pointNormAvg = (sideFaceNormAvg + Vector3.Back) / 2;    

            var frontEdgeNormal = pointNormAvg.Normalized();
            var oppositeEdgeNormal = frontEdgeNormal * new Vector3(1,1,-1);
            var edgePoints = new List<IndexedVertex>() { };


            // TODO: rather than add any points to lengthen edge -- just lengthen each face (means)
            // or .. add points and also increase side length to make edges smoother
            
            var sideLengthMult = 1 + (1.0 - (p1.Y / terrainHeight)) * 19;  // lerps from 4 to 1 over the distance from 0 to max height
            var edgeLinePointCount = (int)(sideLength * sideLengthMult / halfQuad + 1);
            
            for (int k = 0; k < edgeLinePointCount; k++)
            {
                // move point back (-Z) by k times min quad size
                var distA = k * halfQuad;
                var distB = (edgeLinePointCount - 1 - k) * halfQuad;
                var pos = p1 + new Vector3(0, 0, -distA);
                var vert = new IndexedVertex() { Position = pos, ArrayIndex = VertexList.Count, Custom0 = ColorFromNormal(faceNorm2) };

                // spherical Slerp normal around the imaginary circle at at either edge
                if (distA > edgeSmooth && distB > edgeSmooth) vert.Normal = sideFaceNormAvg;
                else if (distA < distB) vert.Normal = frontEdgeNormal.Lerp(sideFaceNormAvg, distA / edgeSmooth).Normalized();
                else vert.Normal = oppositeEdgeNormal.Lerp(sideFaceNormAvg, distB / edgeSmooth).Normalized();

                VertexList.Add(vert);
                edgePoints.Add(vert);

            }
            EdgeVertices.Add(edgePoints[0]);
            edgeLines.Add(edgePoints);

        }

        var totalFaceIndices = new List<int>();

        // loop again with and convert sets of lines into triangle indices
        for (int i = 0; i < edgeLines.Count; i++)
        {
            var edge1 = edgeLines[i];
            // AddDebugPath(edge1.Select(v => v.Position).ToList());
            var edge2 = edgeLines[Mod(i + 1, edgeLines.Count)];

            var indices = new List<int>();

            // form two triangles for each set of two points in edges,
            // indices point to spot on VertexList
            var edge1Pos = 0;
            var edge2Pos = 0;

            // break loop when last index reached for both edges
            while (edge1Pos < edge1.Count - 1 || edge2Pos < edge2.Count - 1)
            {
                if (edge1Pos < edge1.Count - 1)
                {
                    indices.AddRange(
                    [
                        edge1[edge1Pos].ArrayIndex,
                        edge1[edge1Pos + 1].ArrayIndex,
                        edge2[edge2Pos].ArrayIndex
                    ]);
                    
                    edge1Pos++;

                }

                // different triangulation pattern on opposite side so winding orders match
                if (edge2Pos < edge2.Count - 1)
                {
                    indices.AddRange(
                    [
                        edge2[edge2Pos].ArrayIndex,
                        edge1[edge1Pos].ArrayIndex,
                        edge2[edge2Pos+1].ArrayIndex
                    ]);
                    
                    edge2Pos++;

                }

            }            


            totalFaceIndices.AddRange(indices);
        }

        return totalFaceIndices;

    }

    private List<int> GenerateFrontFace(Vector2[] poly)
    {
        var st = new SurfaceTool();



        var quad = PolygonQuad.CreateRootQuad(poly, quadSize);
        faceQuad = quad;
        SubdivideMainFace(quad);

        var triangleIndices = new List<int>();


        foreach (var node in LeafNodes)
        {
            var leafPoly = node.Polygons[0];
            var poly3d = new List<IndexedVertex>();

            var edgePoints = EdgeSmoothingMap[node];

            foreach (var p in leafPoly)
            {
                var pos = D(p, 0);
                var norm = Vector3.Back;

                if (edgePoints.Count != 0)
                {
                    var sortedEdgePoints = edgePoints.OrderBy(p => p.Position.DistanceSquaredTo(pos)).ToList();
                    var closePoint = sortedEdgePoints[0];
                    var delta = float.Clamp(closePoint.Position.DistanceTo(pos), 0, edgeSmooth);
                    norm = closePoint.Normal.Lerp(norm, delta / edgeSmooth).Normalized();

                    
                }


                var vertex = new IndexedVertex() { Position = pos, Normal = norm, Custom0 = ColorFromNormal(Vector3.Back), ArrayIndex = VertexList.Count };
                VertexList.Add(vertex);
                poly3d.Add(vertex);

            }

            // get list of poly indexes for face triangles
            var polyTriIndices = Geometry2D.TriangulatePolygon(leafPoly).Reverse();
            // convert into a list of indices pointing to IndexedVertex's in VertexList
            var vertexTriIndices = polyTriIndices.Select(index => poly3d[index].ArrayIndex);
            triangleIndices.AddRange(vertexTriIndices);


        }

        return triangleIndices;

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

