using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;

using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Transactions;
using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

//  builds extruded, and distortable meshes from polygons (Vector2 arrays)

public partial class ExtrudedMesh : GodotObject
{
    

    public Vector2[] Polygon;
    public PolygonQuad ParentQuad;
    public float MinimumQuadWidth;
    public float EdgeRadius;
    public float EdgeExtension = 0;
    public bool GenerateBack = false;
    


    private float edgeInfluenceLimit;
    private GeometryUtils gUtils = new();
    private Dictionary<PolygonQuad, List<LineSegment>> quadNearbyEdgeLists = new();
    // private Dictionary<PolygonQuad, PolygonMeshSlice> meshSliceDictionary = new();
    private Dictionary<Vector2I, IndexedVertex> vertexDictionary = new();
    private List<IndexedVertex> vertexList = new();
    private List<PolygonQuad> leafNodeQuads = new();
    private Curve3D edgeRadiusCurve = (Curve3D)GD.Load("uid://c6avem4lbyumt");
    private Vector2 polygonSize;


    public ExtrudedMesh(Vector2[] polygon, float minimumQuadWidth, float edgeRadius, float edgeExtension)
    {
        var time = Time.GetTicksMsec();
        Polygon = polygon;
        polygonSize = gUtils.RectFromPolygon(polygon).Size;
        MinimumQuadWidth = minimumQuadWidth;
        EdgeRadius = edgeRadius;
        EdgeExtension = edgeExtension;
        SetupCurve2D();
        SetupPolygonQuad();
        GD.Print(vertexList.Count + " vertices, in " + (Time.GetTicksMsec() - time) + " ms, from " + quadNearbyEdgeLists[ParentQuad].Count, " line polygon");
        
    }

    

    private void SetupPolygonQuad()
    {
        ParentQuad = PolygonQuad.CreateRootQuad(Polygon, MinimumQuadWidth);
        quadNearbyEdgeLists[ParentQuad] = gUtils.LineSegmentsFromPolygon(Polygon);
        SubdivideQuads();
    }

    private void SetupCurve2D()
    {
        edgeRadiusCurve.SetPointOut(  0, edgeRadiusCurve.GetPointOut(0) * EdgeRadius);
        edgeRadiusCurve.SetPointPosition(1, edgeRadiusCurve.GetPointPosition(1) * EdgeRadius);
        edgeRadiusCurve.SetPointIn(1, edgeRadiusCurve.GetPointIn(1) * EdgeRadius);
        
        edgeRadiusCurve.BakeInterval = EdgeRadius / 100f;
        var length = edgeRadiusCurve.GetBakedLength();
        edgeInfluenceLimit = length; 
        
    }


    public void SubdivideQuads()
    {
        var queue = new List<PolygonQuad>{ParentQuad};
        int queuePosition = 0;
        while (queue.Count > queuePosition)
        {   
            var quad = queue[queuePosition];
            queuePosition += 1;
            
            float targetSubdivideWidth = MinimumQuadWidth * 8;
            if (quadNearbyEdgeLists[quad].Count > 0){
                var closestEdge = gUtils.ShortestDistanceBetweenSegmentAndRect(quad.BoundingRect, quadNearbyEdgeLists[quad][0]);
                if (closestEdge < MinimumQuadWidth * 1.5) targetSubdivideWidth = MinimumQuadWidth;
                else if (closestEdge < MinimumQuadWidth*2.5 ) targetSubdivideWidth = MinimumQuadWidth * 2;
                else if (closestEdge < EdgeRadius/2) targetSubdivideWidth = MinimumQuadWidth * 4;
            }

            if (targetSubdivideWidth <= quad.BoundingRect.Size.X / 2) 
            {
                quad.Subdivide();
            }
            foreach (var child in quad.GetChildren())
            {
                if (child == null) continue;
                var edgeCheckDistance = float.Clamp(child.BoundingRect.Size.X * 2, 0, edgeInfluenceLimit); 
                quadNearbyEdgeLists[child] = gUtils.SortLineSegmentsByDistanceToRect(child.BoundingRect, quadNearbyEdgeLists[quad], edgeCheckDistance);
                queue.Add(child);
            }
            if (!quad.HasChildren()) // still a leaf node after subdivision attempt
            {
                leafNodeQuads.Add(quad);
                IndexQuad(quad);
            }
            
        }
    }

    private void IndexQuad(PolygonQuad quad)
    {
        List<int> indexedPolygon3D = new();
        foreach (var polygon in quad.Polygons)
        {
            foreach (var point in polygon)
            {
                var key = RoundVectorAsKey(point);
                IndexPoint(point, quad);
                indexedPolygon3D.Add(vertexDictionary[key].ArrayIndex);
            }
        }
    }

    private void IndexPoint(Vector2 point, PolygonQuad quad)
    {
        var key = RoundVectorAsKey(point);
        if (vertexDictionary.ContainsKey(key)) return;

        var vertex = new IndexedVertex(){
            SourcePosition = point,
            Position = new Vector3(point.X, point.Y, 0),
            Normal = Vector3.Back,
            ArrayIndex = vertexList.Count
        };
        
        vertexList.Add(vertex);
        vertexDictionary[key] = vertex;

        WrapPointAroundEdge(vertex, quad);

    }

    private void WrapPointAroundEdge(IndexedVertex vertex, PolygonQuad quad)
    {
        double nearestEdgeSqr = double.MaxValue;
        var edgeInfluenceSquare = edgeInfluenceLimit * edgeInfluenceLimit;
        Vector2 sPos = vertex.SourcePosition;
        Vector2 nearestEdgeDirection = Vector2.Zero;

        var edgeList = quad.Parent != null ? quadNearbyEdgeLists[quad.Parent] : quadNearbyEdgeLists[quad]; //uses parent quads larger edge list until "nearby" is better defined in subdivision checks  

        List<Vector2> edgeInfluenceVectors = new();
        foreach (var lineSeg in edgeList)
        {
            var norm = lineSeg.GetNormal();
            var offset = norm * 0.001f;  // offset values away from edge to avoid divide-by-zero
            var l1 = lineSeg.Start + offset;
            var l2 = lineSeg.End + offset;

            var closePoint = Geometry2D.GetClosestPointToSegment(sPos, l1, l2); 
            
            var vecToLine = closePoint - sPos;
    
            var distSqr = vecToLine.LengthSquared();
           
            if (distSqr < edgeInfluenceSquare)
            {
                if (distSqr < nearestEdgeSqr) nearestEdgeSqr = distSqr; 
                var inverseLengthWeightedVector =  (edgeInfluenceSquare - distSqr) / edgeInfluenceSquare * vecToLine.Normalized(); // longer vectors become smaller in weighting
                var angleProjectedWeightedVector = inverseLengthWeightedVector.Project(norm); // further lessen in weighting if direction to edge is indirect 
                // if (angleProjectedWeightedVector.Dot(norm) < 0) continue;
                nearestEdgeDirection += angleProjectedWeightedVector;
            }

        }        
        nearestEdgeDirection = nearestEdgeDirection.Normalized();
        var nearestEdgeDelta = Math.Sqrt(nearestEdgeSqr);
        if (nearestEdgeDirection != Vector2.Zero)
        {
            var curveProgress = (float)(edgeInfluenceLimit - nearestEdgeDelta);
            Transform3D curvePointTransform = edgeRadiusCurve.SampleBakedWithRotation(curveProgress);
            

            var edgePos2D = sPos + (float)nearestEdgeDelta * nearestEdgeDirection;
            
            var curveOrigin2D = edgePos2D - (nearestEdgeDirection * EdgeRadius);
            var curveOrigin3D = gUtils.AddDepth(curveOrigin2D, 0);
            
            var curvePos = curvePointTransform.Origin;
            var rotatedCurvePos = curvePos.Rotated(Vector3.Back, Vector2.Right.AngleTo(nearestEdgeDirection));
            
            var curveNormal = curvePointTransform.Basis.X;
            var rotatedNormal = curveNormal.Rotated(Vector3.Back, Vector2.Right.AngleTo(nearestEdgeDirection));
            vertex.Position = rotatedCurvePos + curveOrigin3D;
            vertex.Normal = rotatedNormal.Normalized();
            vertex.VertexColor = (vertex.Normal + new Vector3(1,1,1))/2;
            if (nearestEdgeDelta < 0.01)
            {
                vertex.Position.Z -= EdgeExtension;
            }
            
        }         
    }
    
    public List<Vector2[]> GetMeshAsPolygons()
    {
        List<Vector2[]> polyList = new();
        foreach (var quad in leafNodeQuads )
        {
            polyList.AddRange(quad.Polygons);
        }
        
        return polyList;
    } 

    public Mesh GetMesh()
    {
        var vertexIndices = GetTriangleIndices();

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var polyRect = gUtils.RectIFromPolygon(Polygon);

        foreach(int index in vertexIndices)
        {
            st.AddIndex(index);
        }
       
        foreach(IndexedVertex vertex in vertexList)
        {
            var UV = (new Vector2(vertex.Position.X, vertex.Position.Y)) / polyRect.Size;
            st.SetUV(UV);
            st.SetNormal(vertex.Normal);
            st.SetColor(new Color(vertex.VertexColor.X, vertex.VertexColor.Y, vertex.VertexColor.Z, 1));
            st.AddVertex(vertex.Position);
        }


        var mesh = st.Commit();
        return mesh;
    }

    public Mesh GetMeshBack()
    {
        var vertexIndices = GetTriangleIndices();
        // vertexIndices.Reverse();
        // var reversedVertexList = vertexList.ToArray().Reverse().ToList();

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach(int index in vertexIndices)
        {
            st.AddIndex(index);
        }

        foreach(IndexedVertex vertex in vertexList)
        {
            var flipZ = new Vector3(1, 1, -1);
            var backPos = vertex.Position * flipZ + new Vector3(0, 0, - 1.9f * (EdgeExtension+EdgeRadius)); 
            var backNorm = vertex.Normal * new Vector3(-1 ,-1, 1);

            st.SetNormal(backNorm);
            st.SetColor(new Color(vertex.VertexColor.X, vertex.VertexColor.Y, vertex.VertexColor.Z, 1));
            st.AddVertex(backPos);
        }
        return st.Commit();
    }


    public Mesh GetWireframeMesh()
    {
        var vertexIndices = GetTriangleIndices();  
        var wireframeVertices = new List<Vector3>();
        
        for (int i = 0; i < vertexIndices.Count; i += 3)
        {
            List<Vector3> verts = [vertexList[vertexIndices[i]].Position, vertexList[vertexIndices[i+1]].Position, vertexList[vertexIndices[i+2]].Position];
            var thickness = (float)(gUtils.AreaOfTriangle3D(verts[0], verts[1], verts[2]) * 0.01);  
            // thickness = Math.Clamp(thickness, 0.075f, 1.5f);
            thickness = 0.025f * MinimumQuadWidth;

            wireframeVertices.AddRange(GetLineTriangles(verts[0], verts[1], thickness));
            wireframeVertices.AddRange(GetLineTriangles(verts[1], verts[2], thickness));
            wireframeVertices.AddRange(GetLineTriangles(verts[2], verts[0], thickness));
            
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var vertex in wireframeVertices){
            st.AddVertex(vertex);
        }
        return st.Commit();
    }

    private List<Vector3> GetLineTriangles(Vector3 point1, Vector3 point2, float thickness)
    {
        var vector = point1 - point2;
        var normal = vector.Cross(Vector3.Back).Normalized();
        
        
        var sideVector = normal * thickness / 2;

        return new List<Vector3>(){
            point1 - sideVector,
            point1 + sideVector,
            point2 - sideVector,
            point1 + sideVector,
            point2 + sideVector,
            point2 - sideVector,    
        };


    }

    private List<int> GetTriangleIndices()
    {
        var vertexIndices = new List<int>();

        foreach (var quad in leafNodeQuads)
        {
            foreach (var polygon in quad.Polygons )
            {
                if (polygon.Length == 0) continue;
                var triangleIndices = Geometry2D.TriangulatePolygon(polygon);
                
                var triangleIndicesList = triangleIndices.ToList();

                triangleIndicesList = triangleIndices.Select(index => 
                    vertexDictionary[ RoundVectorAsKey(polygon[index])].ArrayIndex).ToList();
                vertexIndices.AddRange(triangleIndicesList);
            }
        }
        vertexIndices.Reverse();
        return vertexIndices;
    }

    private Vector2I RoundVectorAsKey(Vector2 point)
    {
        return new Vector2I(
            (int)(point.X * 1000),
            (int)(point.Y * 1000)
        );
    }

}
