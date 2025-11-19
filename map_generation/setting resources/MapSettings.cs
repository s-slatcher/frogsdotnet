using Godot;
using System;

public partial class MapSettings : Resource
{

    [Export] public float Width = 200;
    [Export] public float MaxHeight = 80;
    [Export] public float MajorDivisions = 3; // number of land "clusters" separated by large ocean gaps
    [Export] public float MeanMajorDivideWidth = 50;
    [Export] public float BaseMinorDivideChance = 0.1f; // chance of minor division happening between min-height landmasses, goes down higher up
    [Export] public float MeanMinorDivideWidth = 10;
 
}
