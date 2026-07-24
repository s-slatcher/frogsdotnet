using Godot;
using System;

public partial class DestructibleCollisionPolygon : Node2D
{
    
    public void AddPolygon(NormalPoly poly)
    {
        var con = GetNode<StaticBody2D>("Container");
        var colPoly = new CollisionPolygon2D(){Polygon = poly.Polygon, Position = poly.Position};
        con.AddChild(colPoly);
    }
}
