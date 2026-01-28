using Godot;
using System;

public partial class TerrainTexture : SubViewport
{

    EdgeTextureGenerator edgeTexture = new();
    public float GrassLength;

    public override void _Ready()
    {


    }

    public void SetPolygon(Vector2[] polygon, float grassLength = 3)
    {

        // var polyInst = new Polygon2D() { Polygon = polygon };
        // AddChild(polyInst);
        GD.Print(polygon.Length == 0);
        GrassLength = grassLength;
        var tex = GetEdgeTexture(polygon);
        Size = (Vector2I)tex.GetSize();
        
        // var quad_mesh = new QuadMesh() { Size = this.Size };
        // MainMesh.Mesh = quad_mesh;
        // var mat = (ShaderMaterial)MainMesh.Material;
        // mat.SetShaderParameter("grass_texture", tex);


        GetNode<Sprite2D>("Sprite2D").Texture = tex;
        GetNode<Sprite2D>("Sprite2D").Centered = false;

    }

    public void AddExplosion(Vector2 center1, Vector2 center2, float radius)
    {

        var ppu = edgeTexture.PixelPerUnit;

        var line2d = new Line2D()
        {
            SelfModulate = Colors.Green,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            Width = radius * ppu * 2,
            RoundPrecision = 10,

            Points =
            [
                center1 * ppu,
                center2 != center1 ? center2 * ppu : (center1 + new Vector2(0.01f, 0.01f)) * ppu   
            ]
        };

        var container = GetNode<Node2D>("ExplosionLineContainer");
        AddChild(line2d);
    }

    ImageTexture GetEdgeTexture(Vector2[] polygon)
    {
        edgeTexture.Polygon = polygon;
        edgeTexture.edgeDistanceLimit = GrassLength;
        edgeTexture.edgeBuffer = 2;
        Image image = edgeTexture.Generate();
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    public override void _Process(double delta)
    {
        // GetNode<Polygon2D>("Polygon2D").Translate(new Vector2(5, 0) * (float)delta) ;
    }



}
