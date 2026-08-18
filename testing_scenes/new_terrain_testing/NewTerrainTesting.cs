using Godot;
using System;

public partial class NewTerrainTesting : Node3D
{
    [Export] Node PolygonNodeContainer;
    [Export] ShaderMaterial material;
    public override void _Ready()
    {
        //generate a series of box polygons of various heights

        var currentPos = new Vector2();
        var gu = new GeometryUtils();
        var totalBoxes = 5;
        for (int i = 0; i < totalBoxes; i++)
        {
            var rect = new Rect2(){
                Position = currentPos, 
                Size = new Vector2(GD.RandRange(5,7), GD.RandRange(5, 15) ) 
            };
            var poly = gu.PolygonFromRect(rect);
            var norm = new NormalPoly(poly);

            var meshTool = new PolygonMeshTool();
            meshTool.SetPolygon(norm, 3);
            var meshInst = new MeshInstance3D()
            {
                Mesh = meshTool.GetMesh()
            };
            meshInst.Position = new Vector3(norm.Position.X, norm.Position.Y, 0);
            AddChild(meshInst);
            // meshInst.MaterialOverride = material;
            currentPos += new Vector2(rect.Size.X, 0);
        }
        
    }

    public void GeneratePolyTerrainChunk()
    {
        
    }

}
