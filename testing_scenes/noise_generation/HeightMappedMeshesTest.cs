using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class HeightMappedMeshesTest : Node3D
{
    
    [Export] public bool Wireframe = false;
    [Export] public float grassHangLength = 4;

    GeometryUtils gu = new();
    

    
    public override void _Ready()
    {
        var poly = GetHeightMapPolygon();

        var em = new ExtrudedMesh(poly, 0.25f, 1f, 5f);
        GetNode<MeshInstance3D>("MeshContainer").Mesh = Wireframe ? em.GetWireframeMesh() : em.GetMesh();

        // texture generation
        var tex = GetEdgeTexture(poly);

        var material = GetNode<MeshInstance3D>("MeshContainer").MaterialOverride as ShaderMaterial;
        var sprite = GetNode<Sprite2D>("CanvasLayer/Sprite2D");

        material.SetShaderParameter("texture_edge", tex);
        // sprite.Texture = tex;
        // sprite.Centered = false;

    }

    ImageTexture GetEdgeTexture(Vector2[] polygon)
    {
        var edgeTextureGenerator = new EdgeTextureGenerator();
        edgeTextureGenerator.Polygon = polygon;
        edgeTextureGenerator.edgeDistanceLimit = grassHangLength;
        edgeTextureGenerator.edgeBuffer = 2;
        Image image = edgeTextureGenerator.Generate();
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    Vector2[] GetHeightMapPolygon()
    {
        var heightMap = new HeightMap(300, (int)GD.Randi());
        heightMap.noise.Frequency = 0.025f;
        heightMap.MaxHeight = 80;
        var points = heightMap.GetPointsOfInterest();
        var polygonList = new List<Vector2[]>();
        for (int i = 1; i < points.Count - 1; i++)   // range excludes first and last point
        {
            var point = points[i];
            var lastPointDist = points[i-1].X - point.X;
            var nextPointDist = points[i+1].X - point.X;
            var startCorner = new Vector2( point.X + (lastPointDist / 2) , 0);
            var endCorner = new Vector2(point.X + (nextPointDist / 2), point.Y);
            
            var rect = new Rect2(){Position = startCorner, End = endCorner};
            var poly = new Vector2[]{
                rect.Position - new Vector2(1,0),
                rect.Position + new Vector2(rect.Size.X + 1, 0),
                rect.End,
                rect.End - new Vector2(rect.Size.X, 0 ) 
            };
            
            gu.PolygonFromRect(rect);
            rect.GrowIndividual(1f ,0 ,0 ,0);
            polygonList.Add(poly);

        }
        var mergeResult = polygonList[0];
        foreach (var poly in polygonList)
        {
            mergeResult = Geometry2D.MergePolygons(mergeResult, poly)[0];
        }

        // normalize polygon
        
        var mergeRect = gu.RectFromPolygon(mergeResult);
        
       return gu.TranslatePolygon(mergeResult,  - mergeRect.Position);
    }


    

}
