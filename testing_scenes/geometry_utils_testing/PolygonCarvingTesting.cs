using Godot;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public partial class PolygonCarvingTesting : Node2D
{
    public override void _Ready()
    {
        // BasicClippingTest();
        GroupClippingTest();
    }

    private void BasicClippingTest()
    {
        var basePoly = GetNode("base") as Polygon2D;
        var clipPoly = GetNode("clip") as Polygon2D;

        var GeoUtil = new GeometryUtils();
        var clippedPolygonResults = GeoUtil.ClipPolygonRecursive(basePoly.Polygon, clipPoly.Polygon);

        

        foreach (var polygon in clippedPolygonResults)
        {
            AddChild(new Polygon2D{Polygon = polygon, Position = new(0, 300), SelfModulate = new Color(GD.Randf(), GD.Randf(), GD.Randf()) });
        }
    }

    private void GroupClippingTest()
    {
        var basePoly = GetNode("base") as Polygon2D;
        var clipPoly = GetNode("clip") as Polygon2D;
        var base2Poly = GetNode("base2") as Polygon2D;
        var clip2Poly = GetNode("clip2") as Polygon2D;

        List<Vector2[]> baseList = new() { basePoly.Polygon, base2Poly.Polygon };
        List<Vector2[]> clipList = new() { clipPoly.Polygon, clip2Poly.Polygon };


        var GeoUtil = new GeometryUtils();
        // var clippedPolygonResults = GeoUtil.ClipPolygonGroupsRecursive(baseList, clipList);
        // foreach (var polygon in clippedPolygonResults)
        // {
        //     AddChild(new Polygon2D{Polygon = polygon, Position = new(0, 300), SelfModulate = new Color(GD.Randf(), GD.Randf(), GD.Randf()) });
        // }

        GD.Print(Geometry2D.MergePolygons(basePoly.Polygon, base2Poly.Polygon).Count, "  merge results");
    }

}
