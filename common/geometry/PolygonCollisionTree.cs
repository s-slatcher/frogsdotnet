using Godot;
using System;
using System.Collections.Generic;

public partial class PolygonCollisionTree : GodotObject
{
    
    public PolygonCollisionTree Parent;
    public List<PolygonCollisionTree> Children = new();
    

}
