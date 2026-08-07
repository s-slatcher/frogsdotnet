using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PolyFaceVisualized : Node3D
{

    static int LastColorIdx = 0; 
    static ColorPalette colorPalette = ResourceLoader.Load<ColorPalette>("uid://btb13a7fpjc7d");
    static Godot.Color GetNextColor()
    {
        LastColorIdx++;
        if (LastColorIdx > colorPalette.Colors.Length - 1) LastColorIdx = 0;
        return colorPalette.Colors[LastColorIdx];
    }

    [Export] MeshInstance3D meshInst;
    [Export] CollisionPolygon3D colPoly;

    public void SetFace(Face face)
    {
        var faceColor = GetNextColor();
        var currentTriColor = faceColor;
        var tris = face.TriangulateEdge();
        
        SurfaceTool st = new();
        st.Begin(Mesh.PrimitiveType.Triangles);

        foreach(var vertSet in tris )
        {
            foreach(var vert in vertSet)
            {
                st.SetNormal(face.GetNormal());
                st.SetColor(currentTriColor);
                st.AddVertex(vert.Position);
            }
            currentTriColor *= 0.85f;
        }
        var mesh = st.Commit();
        meshInst.Mesh = mesh;

        var poly = new List<Vector2>();
        foreach (var pointPair in face.GetPoint2DPair())
        {
            poly.Add(pointPair.Item2);
        }
        colPoly.Polygon = poly.ToArray();
        colPoly.Transform = face.Transform.Inverse();
    }

    public override void _Ready()
    {
        GetNode<Area3D>("Area3D").MouseEntered += OnHover;
        GetNode<Area3D>("Area3D").MouseExited += OnHoverEnd;
    }

    private void OnHoverEnd()
    {
        var mat = (StandardMaterial3D)meshInst.MaterialOverride;
        mat.AlbedoColor = new Color(1,1,1);
        
    }


    private void OnHover()
    {
        var mat = (StandardMaterial3D)meshInst.MaterialOverride;
        mat.AlbedoColor = new Color(1.5f,1.5f,1.5f);
    }

}
