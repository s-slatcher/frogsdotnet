using Godot;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

public partial class PolygonSimplification : Node2D
{
    public Polygon2D PolygonNode;
    public Polygon2D SimplePolygon;
    public HashSet<int> PolygonDeletablePoints = new();

    float _epsilon = 50;

    [Export(PropertyHint.Range, "0, 200, 0.15")]
    public float Epsilon
    {
        get => _epsilon;
        set
        {
            _epsilon = value;
            if (IsNodeReady()) GenerateSimplePoly();
        }
    }

    public override void _Ready()
    {
        PolygonNode = GetNode<Polygon2D>("Polygon2D");
        SimplePolygon = GetNode<Polygon2D>("SimplifiedPolygon");

        var noiseEdgePoly = new NoiseEdgePoly(100, 190, 80, false);
        PolygonNode.Polygon = noiseEdgePoly.Polygon;

        GenerateSimplePoly();



    }

    private void GenerateSimplePoly()
    {

        var time = Time.GetTicksMsec();

        var polygon = PolygonNode.Polygon;
        var deletablePoints = new HashSet<int>();

        var segments = new List<(int, int)>
        {
            (0, polygon.Length - 1)
        };

        float epSquare = Epsilon * Epsilon;

        while (segments.Count > 0)
        {
            var segment = segments[^1];
            segments.RemoveAt(segments.Count - 1);

            int start = segment.Item1;
            int end = segment.Item2;

            if (end == start + 1) continue;

            var p1 = polygon[start];
            var p2 = polygon[end];



            float maxDistSquare = 0;
            int maxDistIndex = -1;

            for (int i = start + 1; i < end; i++)
            {
                var p = polygon[i];
                var closePoint = Geometry2D.GetClosestPointToSegment(p, p1, p2);
                var distSquare = closePoint.DistanceSquaredTo(p);
                if (maxDistSquare < distSquare)
                {
                    maxDistSquare = distSquare;
                    maxDistIndex = i;
                }
            }

            if (maxDistSquare > epSquare)
            {
                segments.Add((start, maxDistIndex));
                segments.Add((maxDistIndex, end));
            }
            else
            {
                for (int i = start + 1; i < end; i++)
                {
                    deletablePoints.Add(i);
                }
            }

        }

        GD.Print("simplified ", polygon.Length, " points down to," , polygon.Length - deletablePoints.Count, " in ", Time.GetTicksMsec() - time, " ms");


        PolygonDeletablePoints = deletablePoints;
        SetPolygonOverlay();
    }

    private void SetPolygonOverlay()
    {
        var simplePoly = new List<Vector2>();
        for (int i = 0; i < PolygonNode.Polygon.Length; i++)
        {
            if (PolygonDeletablePoints.Contains(i)) continue;
            simplePoly.Add(PolygonNode.Polygon[i]);
        }
        SimplePolygon.Polygon = simplePoly.ToArray();
    }

}
