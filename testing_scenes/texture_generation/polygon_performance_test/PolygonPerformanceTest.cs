using Godot;
using System;

public partial class PolygonPerformanceTest : Node2D
{
    Line2D lineNode;
    float deltaSum = 0;
    float dupeDelay = 0.01f;
    int lineCount = 0;

    public override void _Ready()
    {

        lineNode = GetNode<Line2D>("Line2D");

    }

    public override void _Process(double delta)
    {
        deltaSum += (float)delta;
        if (dupeDelay > deltaSum) return;
        deltaSum = 0;
        

        Line2D dupe = lineNode.Duplicate() as Line2D;
        dupe.Translate(new Vector2(GD.RandRange(-500, 500), GD.RandRange(-500, 500)));
        AddChild(dupe);
        lineCount += 1;
        GD.Print("total lines: ", lineCount);
    }


}
