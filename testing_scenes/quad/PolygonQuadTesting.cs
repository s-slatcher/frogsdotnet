using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PolygonQuadTesting : Node2D
{
    
    public GeometryUtils gUtil = new();
    public Dictionary<Vector2, IndexedVertex> vertexDictionary;

    public override void _Ready()
    {
        var time = Time.GetTicksMsec();
        var polygon = GetNode<Polygon2D>("Polygon2D").Polygon;
        ExtrudedMesh em = new(polygon, 4, 24, 1);
        

        
        var dividedPolygons = em.GetMeshAsPolygons();
        
        Console.WriteLine("time to subdivide poly: " + (Time.GetTicksMsec() - time).ToString());
        
        var trianglePolygons = new List<Vector2[]>(); 
        foreach (var poly in dividedPolygons )
        {
            var tris = Geometry2D.TriangulatePolygon(poly);

            
            for (int i = 0; i < tris.Length; i+= 3)
            {
                trianglePolygons.Add(
                    [
                        poly[tris[i]],
                        poly[tris[i+1]],
                        poly[tris[i+2]],
                    ]
                );
            }
        }
        
        foreach (var triPolygon in trianglePolygons)
        {
            AddChild(new Polygon2D
            {
                Polygon = triPolygon, Position = new(0, 300), 
                SelfModulate = new Color(GD.Randf(), GD.Randf(), GD.Randf()) 
            });
        } 
            
        
        
    }

    


}
