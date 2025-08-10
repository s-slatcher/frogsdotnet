using Godot;
using Godot.NativeInterop;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;

public partial class PolygonMesh : MeshInstance3D
{

    Vector2[] polygon;


    public float DefaultSideLength = 2;
    public float QuadDensity = 0.25f;

    public Curve HeightDepthCurve;
    public Curve DepthMultiplierDomainCurve;

    // setting to 0 forces per-face normals for all faces 
    // POSSIBLY replace threshold with smooth separating of normals as angle increases  
    public float SmoothingAngleLimit = 999;
    public float SmoothingBump = 0.2f;
    public float SmoothDistance = 0.2f;

    // vertices store their own index on this list, so mesh can be indexed properly
    public List<IndexedVertex> VertexList = new();

    // maps a vertex on edge of poly to the point that extend from it to add depth 
    // vertices will share positions if per-face normal is needed
    public Dictionary<IndexedVertex, List<IndexedVertex>> EdgeFaceVertices = new();
    public Dictionary<PolygonQuad, List<IndexedVertex>> NearEdgePointsMap = new();


    public List<List<IndexedVertex>> EdgeList = new();

    public List<Vector4> ExplodeList = new();
    int explosionCount = 0;
    const int MAX_EXPLOSIONS = 500;  //keep aligned with shader constants

    Rect2 boundingRect;

    // modulas that wraps on negative values   

    static float Mod(float x, float m) => (x % m + m) % m;
    static int Mod(int x, int m) => (x % m + m) % m;

    // convert vec2 to vec3
    Vector3 D(Vector2 point, float depth)
    {
        return new Vector3(point.X, point.Y, depth);
    }


 

    public void GenerateMesh(Vector2[] polygon)
    {
        var translatedPoly = PreparePolygon(polygon);
        var interpolatedPolygon = InterpolatePolygonEdge(translatedPoly);

        // index interpolated edge polygon as vertices, return a list of sets of two vertices joined in a face
        // each vertex is used in two faces unless per-face normals are enabled

        var faceQuad = PolygonQuad.CreateRootQuad(translatedPoly, QuadDensity);
        SubdivideMainFace(faceQuad);

        List<int> sideFaceIndices = GenerateSideFaces(interpolatedPolygon);
        List<int> frontFaceIndices = GenerateFrontFace(translatedPoly);
        // List<int> backFaceIndices = GenerateBackFace(translatedPoly);
        var indices = sideFaceIndices.Concat(frontFaceIndices);//.Concat(backFaceIndices);


        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbFloat);
        st.SetCustomFormat(1, SurfaceTool.CustomFormat.RFloat);

        foreach (var idx in indices)
        {
            var vert = VertexList[idx];
            var UV = new Vector2(vert.Position.X, vert.Position.Y - 1f) / boundingRect.Size;  // TODO: fix the need for adjusting y UV's for grass shader
            st.SetNormal(vert.Normal);
            st.SetCustom(0, vert.Custom0);
            st.SetCustom(1, vert.Custom1);
            st.SetUV(UV);
            st.AddVertex(vert.Position);
        }

        var mesh = st.Commit();

        Mesh = mesh;

        var shader = (ShaderMaterial)MaterialOverride;
        shader.SetShaderParameter("edge_smooth", SmoothingBump);



    }

    // may re-enable as a solution to casting shadows without as much light bleed onto mesh

    // private List<int> GenerateBackFace(Vector2[] translatedPoly)
    // {
    //     var indices = new List<int>();
    //     var offsetPoly = Geometry2D.OffsetPolygon(translatedPoly, 1)[0];
    //     var triangle = Geometry2D.TriangulatePolygon(offsetPoly);
    //     foreach (var triIndex in triangle)
    //     {
    //         var p = offsetPoly[triIndex];
    //         var vert = IndexVertex(D(p, -LedgeDeptAtPoint(p)), Vector3.Forward, Vector3.Back, 100);
    //         indices.Add(vert.ArrayIndex);
    //     }
    //     return indices;
    // }


    private List<PolygonQuad> SubdivideMainFace(PolygonQuad rootQuad)
    {
        // refine list of edge vertices within range of a node, unless value is zero then 
        // track  refined lists of nearby edges for smoothing normals

        NearEdgePointsMap[rootQuad] = EdgeFaceVertices.Keys.ToList();
        if (SmoothingAngleLimit < float.Pi / 2) NearEdgePointsMap[rootQuad] = new();  // empty set to prevent any smoothing


        var leafNodes = new List<PolygonQuad>();
        var queue = new List<PolygonQuad>() { rootQuad };
        var queuePos = 0;

        while (queuePos < queue.Count)
        {
            var quad = queue[queuePos];
            var edgeSmoothPoints = NearEdgePointsMap[quad];

            queuePos++;

            if (quad.GetWidth() > QuadDensity)
            {
                quad.Subdivide();
                var children = quad.GetChildren();

                foreach (var child in children)
                {
                    // refine nodes nearby edge vertices list for each child's smaller bounding rect
                    var rect = child.BoundingRect.Grow(SmoothDistance);
                    NearEdgePointsMap[child] = edgeSmoothPoints.Where(vert => rect.HasPoint(new Vector2(vert.Position.X, vert.Position.Y))).ToList();
                }

                queue.AddRange(quad.GetChildren());
            }
            else
            {
                leafNodes.Add(quad);
            }
        }
        return leafNodes;
    }


    private List<int> GenerateFrontFace(Vector2[] poly)
    {
        var indices = new List<int>();


        // convert position to int vector that rounds out small FPP errors 
        Vector2I KeyifyVector(Vector3 vertexPos) => new((int)(vertexPos.X * 100), (int)(vertexPos.Y * 100));

        // assign converted positions to vertices
        Dictionary<Vector2I, IndexedVertex> FrontVertexMap = new();

        var quadRoot = PolygonQuad.CreateRootQuad(poly, QuadDensity);
        var leafNodes = SubdivideMainFace(quadRoot);

        foreach (var node in leafNodes)
        {
            var edgeList = NearEdgePointsMap[node];
            var nodePoly = node.Polygons[0];
            var poly3d = new List<IndexedVertex>();

            foreach (var p in nodePoly)
            {
                var pos = D(p, LedgeDeptAtPoint(p));

                var posAsKey = KeyifyVector(pos);
                bool pointExists = FrontVertexMap.ContainsKey(posAsKey);
                if (pointExists)
                {
                    poly3d.Add(FrontVertexMap[posAsKey]);
                    continue;
                }

                var faceNorm = FaceNormalAtPoint(p);
                var vertNorm = faceNorm;

                if (edgeList.Count > 0)
                {
                    var sortedEdgePoints = edgeList.OrderBy(p => p.Position.DistanceSquaredTo(pos)).ToList();
                    var closePoint = sortedEdgePoints[0];
                    var closeNormal = closePoint.Normal;
                    if (sortedEdgePoints.Count > 1 && sortedEdgePoints[1].Position == closePoint.Position)
                    {
                        closeNormal = (closeNormal + sortedEdgePoints[1].Normal) / 2;
                    }

                    var delta = float.Clamp(closePoint.Position.DistanceTo(pos), 0, SmoothDistance);
                    vertNorm = closePoint.Normal.Lerp(faceNorm, delta / SmoothDistance).Normalized();
                }

                var vert = IndexVertex(pos, vertNorm, faceNorm, DefaultSideLength);
                poly3d.Add(vert);

                FrontVertexMap[posAsKey] = vert;

            }
            // get list of poly indexes for face triangles
            var polyTriIndices = Geometry2D.TriangulatePolygon(nodePoly).Reverse();
            // convert into a list of indices pointing to IndexedVertex's in VertexList
            var vertexTriIndices = polyTriIndices.Select(index => poly3d[index].ArrayIndex);
            indices.AddRange(vertexTriIndices);

        }

        return indices;

    }


    // TODO: get these values by sampling ledge curve

    private float LedgeDeptAtPoint(Vector2 point)
    {
        var depthAtHeight = HeightDepthCurve.SampleBaked(point.Y);
        var domainMultiplier = DepthMultiplierDomainCurve.SampleBaked(point.X);
        var depthValue = (depthAtHeight - HeightDepthCurve.MinValue) * domainMultiplier + HeightDepthCurve.MinValue;
        return depthValue;


        return DefaultSideLength;
    }
    private Vector3 FaceNormalAtPoint(Vector2 point)
    {

        var p1 = new Vector3(point.X, point.Y, LedgeDeptAtPoint(point));
        var p2_2d = point + new Vector2(0.1f, 0.1f);
        var p2 = new Vector3(p2_2d.X, p2_2d.Y, LedgeDeptAtPoint(p2_2d));
        var p3_2d = point + new Vector2(-0.1f, 0.1f);
        var p3 = new Vector3(p3_2d.X, p3_2d.Y, LedgeDeptAtPoint(p3_2d));

        var vec1 = p2 - p1;
        var vec2 = p3 - p1;
        var normal = vec1.Cross(vec2).Normalized();
        if (normal.Z < 0) normal.Z *= -1;

        return normal;
    }

    private List<IndexedVertex> GenerateSideFaceVertices(IndexedVertex frontVertex, Vector3 sideNormal, Vector3 edgeFaceNormal)
    {
        var frontNormal = frontVertex.Normal;
        var backNormal = frontNormal * new Vector3(1, 1, -1);

        var frontPos = frontVertex.Position;
        var frontOffset = frontPos.Z; // positive Z offset of point
        var sideLength = frontOffset * 2;  // terrain is offset by same amount both directions on Z axis

        var lenRat = sideLength / QuadDensity;
        var vertNum = (int)lenRat;
        var quadSize = (lenRat / vertNum) * QuadDensity;

        var edgeList = new List<IndexedVertex>() { frontVertex };

        for (int i = 0; i < vertNum; i++)
        {
            var distFront = (i + 1) * quadSize;
            var distBack = (vertNum * quadSize) - distFront;  // distance from opposite end of face
            var pos = frontPos + new Vector3(0, 0, -distBack);

            Vector3 vertNormal;

            // special case where smooth distance set too high, directly lerp front to back normals
            if (sideLength < SmoothDistance) vertNormal = frontNormal.Lerp(backNormal, distFront / sideLength);

            else if (distFront > SmoothDistance && distBack > SmoothDistance) vertNormal = sideNormal;

            else if (distFront < distBack) vertNormal = frontNormal.Lerp(sideNormal, distFront / SmoothDistance).Normalized();

            else vertNormal = backNormal.Lerp(sideNormal, distBack / SmoothDistance).Normalized();


            edgeList.Add(IndexVertex(pos, vertNormal, edgeFaceNormal, DefaultSideLength));

        }

        return edgeList;

    }

    private List<int> GenerateSideFaces(Vector2[] interpolatedPolygon)
    {
        var indicesList = new List<int>();
        var poly = interpolatedPolygon;


        // list of 2-length sets of edge vertices defining an edge face
        var lastList = new List<IndexedVertex>() { null, null };
        EdgeList = new List<List<IndexedVertex>>() { lastList };



        var len = poly.Length;
        GD.Print("polylen: ", len);
        for (int i = 0; i < len; i++)
        {

            float zOffset = LedgeDeptAtPoint(poly[i]);
            var p0 = D(poly[Mod(i - 1, len)], zOffset);
            var p1 = D(poly[i], zOffset);
            var p2 = D(poly[Mod(i + 1, len)], zOffset);

            var faceNorm1 = (p1 - p0).Rotated(Vector3.Back, float.Pi / 2).Normalized();
            var faceNorm2 = (p2 - p1).Rotated(Vector3.Back, float.Pi / 2).Normalized();

            var angle = faceNorm1.AngleTo(faceNorm2);
            bool smoothNormals = angle < SmoothingAngleLimit;
            bool smoothWithFront = SmoothingAngleLimit > float.Pi / 2;



            IndexedVertex leftVert;
            IndexedVertex rightVert;
            float edgeVertCount = (int)(zOffset * 2 / QuadDensity);




            // shared vertex for both sides with averaged normal vector
            if (smoothNormals)
            {


                var sideFaceNormAvg = (faceNorm1 + faceNorm2) / 2;
                var frontFaceNormal = FaceNormalAtPoint(poly[i]);
                var edgePointNormal = (sideFaceNormAvg + frontFaceNormal) / 2;
                // var oppositeEdgeNorm = edgePointNormal * new Vector3(1, 1, -1);


                leftVert = rightVert = IndexVertex(p1, edgePointNormal, faceNorm1, DefaultSideLength); // face norm chosen is arbitrary 
                var edgeFaceVerts = GenerateSideFaceVertices(leftVert, sideFaceNormAvg, faceNorm1);



                EdgeFaceVertices[leftVert] = edgeFaceVerts;
            }
            // separate left and right vertices-- assigned respective face normal
            else
            {
                leftVert = IndexVertex(p1, faceNorm1, faceNorm1, DefaultSideLength);
                rightVert = IndexVertex(p1, faceNorm2, faceNorm2, DefaultSideLength);


                var leftVertList = new List<IndexedVertex>() { leftVert };
                var rightVertList = new List<IndexedVertex>() { rightVert };
                for (int k = 0; k < edgeVertCount; k++)
                {
                    var dist = (k + 1) * QuadDensity;
                    var translate = new Vector3(0, 0, -dist);
                    leftVertList.Add(IndexVertex(p1 + translate, leftVert.Normal, faceNorm1, DefaultSideLength));
                    rightVertList.Add(IndexVertex(p1 + translate, rightVert.Normal, faceNorm2, DefaultSideLength));
                }

                EdgeFaceVertices[leftVert] = leftVertList;
                EdgeFaceVertices[rightVert] = rightVertList;

            }

            // insert edge in previous and next edge lines; wrap to first edge on last loop
            EdgeList[^1][1] = leftVert;
            if (i != len - 1) EdgeList.Add(new() { rightVert, null });
            else EdgeList[0][0] = rightVert;

        }

        indicesList = EdgeList
            .SelectMany(edgeList => TriangulateEdgeFace(edgeList[0], edgeList[1]))
            .ToList();

        return indicesList;
    }


    private List<int> TriangulateEdgeFace(IndexedVertex edge1, IndexedVertex edge2)
    {
        var indices = new List<int>();


        var leftVerts = EdgeFaceVertices[edge1];
        var rightVerts = EdgeFaceVertices[edge2];
        // form two triangles for each set of two points in edges,
        // indices point to spot on VertexList
        var leftPos = 0;
        var rightPos = 0;

        // break loop when last index reached for both edges
        while (leftPos < leftVerts.Count - 1 || rightPos < rightVerts.Count - 1)
        {
            if (leftPos < leftVerts.Count - 1)
            {
                indices.AddRange(
                [
                    leftVerts[leftPos].ArrayIndex,
                    leftVerts[leftPos + 1].ArrayIndex,
                    rightVerts[rightPos].ArrayIndex
                ]);
                leftPos++;
            }

            // different triangulation pattern on opposite side to match winding orders
            if (rightPos < rightVerts.Count - 1)
            {
                indices.AddRange(
                [
                    rightVerts[rightPos].ArrayIndex,
                    leftVerts[leftPos].ArrayIndex,
                    rightVerts[rightPos+1].ArrayIndex
                ]);
                rightPos++;
            }

        }

        return indices;


    }



    private Vector2[] InterpolatePolygonEdge(Vector2[] polygon)
    {
        if (QuadDensity == 0) return polygon;
        var newPoly = new List<Vector2>();
        var len = polygon.Length;

        for (int i = 0; i < len; i++)
        {
            // get points defining edge
            var p1 = polygon[i];
            var p2 = polygon[i == len - 1 ? 0 : i + 1];

            // get positive-size rect containing the edge
            var rangeRect = new Rect2() { Position = p1, End = p2 };
            rangeRect = rangeRect.Abs();
            var rect_pos = rangeRect.Position;

            // adjust corner of rect to start at Vector of first possible X and Y grid values to insert  
            var startPoint = new Vector2(
                rect_pos.X - Mod(rect_pos.X, QuadDensity) + QuadDensity,
                rect_pos.Y - Mod(rect_pos.Y, QuadDensity) + QuadDensity
            );
            rangeRect = new Rect2(startPoint, rangeRect.End - startPoint);
            var range = rangeRect.Size;

            // get total intervals of X and Y grid positions crossed on edge
            // check for case where line crosses no X or Y intervals and produces a negative value
            var intervals = new Vector2I((int)(range.X / QuadDensity) + 1, (int)(range.Y / QuadDensity) + 1);
            if (range.X < 0) intervals.X = 0;
            if (range.Y < 0) intervals.Y = 0;

            //loop x and y intervals, find corresponding point on line and insert between line ends
            var newPoints = new List<Vector2>() { p1 };
            for (int k = 0; k < intervals.X; k++)
            {
                var interval_add = k * QuadDensity;
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
                var interval_add = j * QuadDensity;
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


    private Vector2[] PreparePolygon(Vector2[] polygon)
    {
        if (!Geometry2D.IsPolygonClockwise(polygon)) Array.Reverse(polygon);
        var rect = GeometryUtils.RectFromPolygon(polygon);
        boundingRect = rect;
        var translatedPoly = polygon.Select(p => p - rect.Position).ToArray(); // normalize polygon to start at origin
        return translatedPoly;

    }

    Godot.Color ColorFromNormal(Vector3 normal)
    {
        var color_norm = (normal / 2) + new Vector3(0.5f, 0.5f, 0.5f);
        return new Godot.Color(color_norm.X, color_norm.Y, color_norm.Z, 1);

    }

    private IndexedVertex IndexVertex(Vector3 position, Vector3 normal, Vector3 faceNormal, float maxDepth)
    {
        var vert = new IndexedVertex()
        {
            Position = position,
            ArrayIndex = VertexList.Count,
            Normal = normal,
            Custom0 = ColorFromNormal(faceNormal),
            Custom1 = new Color(LedgeDeptAtPoint(new Vector2(position.X, position.Y)), 0, 0)
        };
        VertexList.Add(vert);
        return vert;
    }

    public void ExplodeTerrain(Vector3 vector, float radius)
    {
        var explosion = new Vector4(vector.X, vector.Y, 0, radius);
        if (ExplodeList.Count > MAX_EXPLOSIONS) return;

        var index = ExplosionInsertPosition(vector, radius);

        ExplodeList.Insert(index, explosion);

        var explodeArray = ExplodeList.ToArray();
        var shader = (ShaderMaterial)MaterialOverride;
        shader.SetShaderParameter("explosion_array", explodeArray);
        explosionCount++;
    }

    public int ExplosionInsertPosition(Vector3 center, float radius)
    {

        // array length is MAX_EXPLOSIONS constant, but actual length is explosionCount
        var rightEdge = center.X + radius;

        var length = ExplodeList.Count;
        var left = 0;
        var right = length;

        while (left < right)
        {
            var mid = left + (right - left) / 2;
            if (ExplosionRightEdge(ExplodeList[mid]) > rightEdge)
            {
                right = mid;
            }
            else left = mid + 1;
        }

        return left;


    }

    public float ExplosionRightEdge(Vector4 explosion)
    {
        // returns center X value + radius
        return explosion.X + explosion.W; 
    }

    

}
