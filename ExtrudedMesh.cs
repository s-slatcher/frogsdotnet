using Godot;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;

using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    public float EdgeCutOffHeight = 0;
    public int LodFactor = 0; // each lod factor halves the minimum quad size used when generated the mesh

    public Mesh CachedMesh;

    public List<Mesh> WireframeMeshes = new();

    public int quadsDuplicated = 0;
    
    const float targetMaximumQuadWidth = 16;
    private float MaximumQuadWidth;

    private float edgeInfluenceLimit;
    private GeometryUtils gUtils = new();
    private Dictionary<PolygonQuad, List<LineSegment>> quadNearbyEdgeLists = new();
    private Dictionary<Vector2I, IndexedVertex> vertexDictionary = new();
    private List<IndexedVertex> vertexList = new();
    private List<PolygonQuad> leafNodeQuads = new();
    private Curve3D edgeRadiusCurve = (Curve3D)GD.Load("uid://c6avem4lbyumt").Duplicate();
    private Vector2 polygonSize;

    private List<Action> actionList = new();
    private ValueTask vertexIndexingTask;
    

    private float edgeRatio = 0;


    public ExtrudedMesh(Vector2[] polygon, float minimumQuadWidth, float edgeRadius, float edgeExtension, int meshDetailLevels = 1)
    {
        var time = Time.GetTicksMsec();
        Polygon = polygon;
        polygonSize = GeometryUtils.RectFromPolygon(polygon).Size;
        MinimumQuadWidth = minimumQuadWidth;

        // set max quad width to be closest value to target max while still cleanly dividing into min quad width
        var maxQuadWidthExponent = Math.Log2(targetMaximumQuadWidth / minimumQuadWidth );
        var roundedExponent = Math.Round(maxQuadWidthExponent);
        MaximumQuadWidth = minimumQuadWidth * (float)Math.Pow(2, roundedExponent);

        EdgeRadius = edgeRadius;
        EdgeExtension = edgeExtension;
        SetupCurve2D();
        
        // SetupPolygonQuad();
        // GD.Print(vertexList.Count + " vertices, in " + (Time.GetTicksMsec() - time) + " ms, from " + quadNearbyEdgeLists[ParentQuad].Count, " line polygon");
        
    }

    


    public void SetupPolygonQuad()
    {
        GD.Print("setup quad:");
        ParentQuad = PolygonQuad.CreateRootQuad(Polygon, MinimumQuadWidth);
        // trim off bottom edges that will be obscured. 
        quadNearbyEdgeLists[ParentQuad] = gUtils.LineSegmentsFromPolygon(Polygon).Where( lineSeg => {
            bool isBottomEdge = lineSeg.Start.Y < EdgeCutOffHeight && lineSeg.End.Y < EdgeCutOffHeight;
            return !isBottomEdge;
        }).ToList();
        
        SubdivideQuads();
        
    }

    private void SetupCurve2D()
    {
        
        edgeRadiusCurve.SetPointOut(  0, edgeRadiusCurve.GetPointOut(0) * EdgeRadius);
        edgeRadiusCurve.SetPointPosition(1, edgeRadiusCurve.GetPointPosition(1) * EdgeRadius);
        edgeRadiusCurve.SetPointIn(1, edgeRadiusCurve.GetPointIn(1) * EdgeRadius);
        
        edgeRadiusCurve.BakeInterval = EdgeRadius / 100f;
        var length = edgeRadiusCurve.GetBakedLength();
        edgeRatio = length / EdgeRadius; 
        edgeInfluenceLimit = EdgeRadius;
    }

    private List<LineSegment> TrimBottomEdges(List<LineSegment> lineSegs)
    {
        
        return lineSegs.Where( lineSeg => {
            bool isBottomEdge = lineSeg.Start.Y < EdgeCutOffHeight && lineSeg.End.Y < EdgeCutOffHeight;
            return !isBottomEdge;
        }).ToList();
    }

    public void SubdivideQuads()
    {
        
        var time = Time.GetTicksMsec();
        
        var indexingQueue = new List<PolygonQuad>{ParentQuad};
        var subdivideQueue = new List<PolygonQuad>();



        while (indexingQueue.Count > 0)
        {
            for (int i = 0; i < indexingQueue.Count; i++)
            {
                var quad = indexingQueue[i];
                IndexQuad(quad);
                if (QuadDensityTarget(quad) < quad.BoundingRect.Size.X) subdivideQueue.Add(quad);
            } 
            
            indexingQueue = new List<PolygonQuad>();
            
            // Meshes.Add(GetMesh());
            // WireframeMeshes.Add(GetWireframeMesh());
            
            for (int i = 0; i < subdivideQueue.Count; i++)
            {
                var quad = subdivideQueue[i];
                quad.Subdivide();
                foreach (var child in quad.GetChildren())
                {
                    var edgeCheckDistance = edgeInfluenceLimit;  
                    quadNearbyEdgeLists[child] = gUtils.SortLineSegmentsByDistanceToRect(child.BoundingRect, quadNearbyEdgeLists[quad], edgeCheckDistance);
                    indexingQueue.Add(child);
                }
                
            }
            
            subdivideQueue = new List<PolygonQuad>();
        }

        
        GD.Print("total subdivide time : "  + (Time.GetTicksMsec() - time) );
        
        // Parallel.Invoke([.. actionList]);
        // foreach (Action action in actionList)
        // {
        //     action.Invoke();
        // }
        // 
        
        // GD.Print("total actions: " + actionList.Count);
        for (int i = 0; i < actionList.Count; i++)
        {
            actionList[i].Invoke();
            
        }

        // CachedMesh = GetMesh();
        // GD.Print("total action invoke time : "  + (Time.GetTicksMsec() - time) );

        // Meshes.Add(GetMesh());
        // WireframeMeshes.Add(GetWireframeMesh());
      

    }

    private float QuadDensityTarget(PolygonQuad quad)
    {
        
        if (quadNearbyEdgeLists[quad].Count == 0) return MaximumQuadWidth;
        
        var closestEdgeDelta = gUtils.ShortestDistanceBetweenSegmentAndRect(quad.BoundingRect, quadNearbyEdgeLists[quad][0]);
        var curveProgress = (float)(edgeInfluenceLimit - closestEdgeDelta) * edgeRatio;
        curveProgress = (float)Math.Pow(float.Clamp(curveProgress, 0, 1), 2);
        // lerps from 0 to *half* max quad width, then outside that range defaults to max quad width;
        var quadWidthNeeded = float.Lerp(0, MaximumQuadWidth/2, 1 - curveProgress);  
        return quadWidthNeeded;
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
        actionList.Add(() => WrapPointAroundEdge(vertex, quad));

    }

    private void WrapPointAroundEdge(IndexedVertex vertex, PolygonQuad quad)
    {
        double nearestEdgeSqr = double.MaxValue;
        var edgeInfluenceSquare = edgeInfluenceLimit * edgeInfluenceLimit;
        Vector2 sPos = vertex.SourcePosition;
        Vector2 nearestEdgeDirection = Vector2.Zero;

        var edgeList = quad.Parent == null ? quadNearbyEdgeLists[quad] : quadNearbyEdgeLists[quad.Parent]; //uses parent quads larger edge list until "nearby" is better defined in subdivision checks  

        
        foreach (var lineSeg in edgeList)
        {
            var norm = lineSeg.GetNormal();
            var offset = norm * 0.001f;  // offset values away from edge to avoid divide-by-zero
            var l1 = lineSeg.Start + offset;
            var l2 = lineSeg.End + offset;

            var closePoint = Geometry2D.GetClosestPointToSegment(sPos, l1, l2); 

            var vecToLine = closePoint - sPos;
            if (Math.Abs(vecToLine.AngleTo(norm)) > Math.PI/2) continue;

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
            var curveProgress = (float)(edgeInfluenceLimit - nearestEdgeDelta) * edgeRatio;
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
            if (nearestEdgeDelta < 0.005)
            {
                vertex.Position.Z -= EdgeExtension;
            }
            
        }         
    }
    

    public async Task<Mesh> GetMesh(float quadSizeLimit = -1)
    {
        GD.Print("Started GetMeshTask");   
        return await Task.Run(() => {

        
        if (quadSizeLimit == -1) quadSizeLimit = MinimumQuadWidth;
        var vertexIndices = GetTriangleIndices_Lod(quadSizeLimit);

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        var polyRect = gUtils.RectIFromPolygon(Polygon);

        foreach(int index in vertexIndices)
        {
            // st.AddIndex(index);
            var vertex = vertexList[index];
            var UV = (new Vector2(vertex.Position.X, vertex.Position.Y)) / polyRect.Size;
            
            st.SetUV(UV);
            
            // st.SetNormal(vertex.Normal);

                st.SetColor(new Godot.Color(vertex.VertexColor.X, vertex.VertexColor.Y, vertex.VertexColor.Z, 1));
            st.AddVertex(vertex.Position);
        }
       
        // foreach(IndexedVertex vertex in vertexList)
        // {
        //     var UV = (new Vector2(vertex.Position.X, vertex.Position.Y)) / polyRect.Size;
        //     st.SetUV(UV);
        //     // st.SetNormal(vertex.Normal);
            
        //     st.SetColor(new Color(vertex.VertexColor.X, vertex.VertexColor.Y, vertex.VertexColor.Z, 1));
        //     st.AddVertex(vertex.Position);
        // }

            st.GenerateNormals();
            var mesh = st.Commit();
            CachedMesh = mesh;
            return mesh;
        });
    }

    public Mesh GetMeshBack()
    {
        return new Mesh();
        // var vertexIndices = GetTriangleIndices_Lod();
        // // vertexIndices.Reverse();
        // // var reversedVertexList = vertexList.ToArray().Reverse().ToList();

        // var st = new SurfaceTool();
        // st.Begin(Mesh.PrimitiveType.Triangles);

        // foreach(int index in vertexIndices)
        // {
        //     st.AddIndex(index);
        // }

        // foreach(IndexedVertex vertex in vertexList)
        // {
        //     var flipZ = new Vector3(1, 1, -1);
        //     var backPos = vertex.Position * flipZ + new Vector3(0, 0, - 1.9f * (EdgeExtension+EdgeRadius)); 
        //     var backNorm = vertex.Normal * new Vector3(-1 ,-1, 1);

        //     st.SetNormal(backNorm);
        //     st.SetColor(new Color(vertex.VertexColor.X, vertex.VertexColor.Y, vertex.VertexColor.Z, 1));
        //     st.AddVertex(backPos);
        // }
        // return st.Commit();
    }


    public Mesh GetWireframeMesh(float quadSizeLimit = -1)
    {
        if (quadSizeLimit == -1) quadSizeLimit = MinimumQuadWidth;
        var vertexIndices = GetTriangleIndices_Lod(quadSizeLimit);  
        var wireframeVertices = new List<Vector3>();
        
        for (int i = 0; i < vertexIndices.Count; i += 3)
        {
            List<Vector3> verts = [vertexList[vertexIndices[i]].Position, vertexList[vertexIndices[i+1]].Position, vertexList[vertexIndices[i+2]].Position];
            // var thickness = (float)(gUtils.AreaOfTriangle3D(verts[0], verts[1], verts[2]) * 0.05);  
            var thickness = 0.02f;

            wireframeVertices.AddRange(GetLineTriangles(verts[0], verts[1], thickness));
            wireframeVertices.AddRange(GetLineTriangles(verts[1], verts[2], thickness));
            wireframeVertices.AddRange(GetLineTriangles(verts[2], verts[0], thickness));
            
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var vertex in wireframeVertices){
            st.AddVertex(vertex);
        }
        st.GenerateNormals();
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

  

    private Vector2I RoundVectorAsKey(Vector2 point)
    {
        return new Vector2I(
            (int)(point.X * 1000),
            (int)(point.Y * 1000)
        );
    }
    
    


    private List<int> TriangulateQuad(PolygonQuad quad)
    {
        var vertexIndices = new List<int>();
        
        var polygon = quad.Polygons[0];
        if (polygon.Length == 0) return vertexIndices;

        var stitchPolygon = StitchQuadPolygon(quad);

            
        var triangleIndices = Geometry2D.TriangulatePolygon(stitchPolygon);
        
        var convertedIndices = new List<int>(); 

        foreach (var index in triangleIndices)
        {
            var polyPoint = stitchPolygon[index];
            var key = RoundVectorAsKey(polyPoint);
            if (!vertexDictionary.ContainsKey(key)) 
            {
                GD.Print("duplicate indexing");
                IndexPoint(polyPoint, quad);
            }
            
            convertedIndices.Add(vertexDictionary[key].ArrayIndex);
        }
        
        vertexIndices.AddRange(convertedIndices);
        return vertexIndices;
    }



    private int numberOfStitches = 0;
    private Vector2[] StitchQuadPolygon(PolygonQuad quad)
    {

        var stitchedPolygon = new List<Vector2>();
        var region = quad.BoundingRect;
        var polygon = quad.Polygons[0];
        if (region.Size.X == MinimumQuadWidth) return polygon;
        // assumes any quad that contains an edge piece will be the min quad width already
        // and that no min sized quads will need stitching
        var gridPoints = gUtils.PolygonFromRect(region);


        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 p1 = polygon[i];
            
            stitchedPolygon.Add(p1);

            Vector2 p2 = i+1 < polygon.Length ? polygon[i+1] : polygon[0];

            if (!gridPoints.Contains(p1) || !gridPoints.Contains(p2)) continue;

            var sharedAxis = p1.X == p2.X ? 0 : 1;
            var otherAxis = sharedAxis == 0? 1 : 0;

            if (p1[sharedAxis] != p2[sharedAxis])
            {
                GD.Print("error " + p1 + " not aligned with " + p2);
                continue;
            }
            var stitchVector = sharedAxis == 0 ? new Vector2(0, MinimumQuadWidth) : new Vector2(MinimumQuadWidth, 0);
            stitchVector *= p1[otherAxis] < p2[otherAxis]? 1 : -1;


            // loop over the possible stitch postiions based on the grid dimensions
            // if those points exist already in other faces, stitch them into this face 
            var stitchPositions = float.Round(region.Size.X / MinimumQuadWidth) - 1;
            for (int j = 0; j < stitchPositions; j++)
            {
                var pos = p1 + (stitchVector * (j + 1));
                var key = RoundVectorAsKey(pos);
                if (vertexDictionary.ContainsKey(key)) { stitchedPolygon.Add(pos); numberOfStitches += 1;}
                
            }
            

        }
        return stitchedPolygon.ToArray();
    }

    
    
    
    

    private List<int> GetTriangleIndices_Lod(float quadSizeLimit)
    {
        var time = Time.GetTicksMsec();
        
        var vertexIndices = new List<int>();
        var finalQuadSize = quadSizeLimit;
        
        var queue = new List<PolygonQuad>{ParentQuad}; 
        int queuePosition = 0;

        while (queue.Count > queuePosition)
        {
            var quad = queue[queuePosition];
            queuePosition += 1;
            
            if (!quad.HasChildren() || quad.BoundingRect.Size.X == finalQuadSize)
            {
                vertexIndices.AddRange(TriangulateQuad(quad));
                continue;
            }
            queue.AddRange(quad.GetChildren());
                
        }

        GD.Print("triangulation time: " + (Time.GetTicksMsec() - time).ToString());
        GD.Print("num of stitches: " + numberOfStitches);
        return vertexIndices;
            
            
        
    }

}
