using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using Vector2 = Godot.Vector2;
using Vector3 = Godot.Vector3;

public partial class FlatPlaneAssociativeMesh: GodotObject
{

    public float minimumQuadWidth = 0.25f;
    public const int Vector2RoundingDecimal = 3;
    
    public Dictionary<Rect2, Vector2[]> MeshFaceToRegionMap;
    public Dictionary<Vector2, IndexedVertex> Vector2ToVertexMap;
    public List<IndexedVertex> VertexList;

    private HashSet<Vector2> SafeVertexRemoveQueue = new();


    static FlatPlaneAssociativeMesh CreateFromCopy(FlatPlaneAssociativeMesh planeMesh)
    {
        var meshData = new FlatPlaneAssociativeMesh();
        meshData.MeshFaceToRegionMap = new Dictionary<Rect2, Vector2[]>(planeMesh.MeshFaceToRegionMap);
        meshData.Vector2ToVertexMap = new Dictionary<Vector2, IndexedVertex>(planeMesh.Vector2ToVertexMap);
        meshData.VertexList = new List<IndexedVertex>(planeMesh.VertexList);

        return meshData;
    }

    private Vector2 RoundVectorAsKey(Vector2 vec2)
    {
        return new Vector2(float.Round(vec2.X, Vector2RoundingDecimal), float.Round(vec2.X, Vector2RoundingDecimal));
    }

    public void IndexPoint(Vector2 vertexPosition2D, Vector3 vertexData, bool forceOverwrite = false)
    {
        var vecKey = RoundVectorAsKey(vertexPosition2D);

        if (forceOverwrite == false && Vector2ToVertexMap.ContainsKey(vecKey)) return;
        
        var indexedVert = new IndexedVertex()
        {
            Position = vertexData,
            ArrayIndex = VertexList.Count
        };
        VertexList.Add(indexedVert);
        Vector2ToVertexMap[vecKey] = indexedVert;

    }

    public void IndexFace(Rect2 region, Vector2[] polygon)
    {
        var keyRoundedPolygon = polygon.Select(RoundVectorAsKey).ToArray();

        MeshFaceToRegionMap[region] = keyRoundedPolygon;

    }


    public void DeindexFace(Rect2 region)
    {
        if (!MeshFaceToRegionMap.ContainsKey(region)) { GD.Print("Tried To remove non-existent face"); return; } 
        
        foreach (var point in MeshFaceToRegionMap[region]) SafeVertexRemoveQueue.Add(point);
        MeshFaceToRegionMap.Remove(region);
        
    }
   
    

    private void TriangulateRegion(Rect2 region)
    {
        
        // triangulate and return triangle indices points to master vertex list
    }

    public Mesh MeshFromRegionList(List<Rect2> regionList)
    {
        return new Mesh();
    }

}
