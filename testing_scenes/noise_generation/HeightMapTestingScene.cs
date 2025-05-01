using Godot;
using Godot.NativeInterop;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Numerics;
using System.Threading;
using Vector2 = Godot.Vector2;

public partial class HeightMapTestingScene : Node2D
{
    int seed = 0;
    float MaxHeight = 80;
    float MinHeight = 5;
    int Width = 300; 
    float minUnitWidth = 5;

    int islandCount = 3;
    
    
    GeometryUtils gu = new();

    public override void _Ready()
    {   
        
        

    }

}

   
