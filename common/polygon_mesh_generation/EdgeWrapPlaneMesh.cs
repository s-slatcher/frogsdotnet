using Godot;
using System;

public partial class EdgeWrapPlaneMesh : GodotObject
{
    public float minimumQuadWidth = 0.25f;
    
    
    private FlatPlaneAssociativeMesh meshData;
    private PolygonQuad quadTreeRoot;



    public EdgeWrapPlaneMesh(FlatPlaneAssociativeMesh planeMesh, PolygonQuad polygonQuadRoot)
    {
        meshData = planeMesh;
        quadTreeRoot = polygonQuadRoot;
    }




}
