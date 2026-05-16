using Godot;
using Vector2 = Godot.Vector2;
using System;
using System.Collections.Generic;


public struct TerrainDistortion
{
    public TerrainDistortion(float radius, Vector2 center)
    {
        width = depth = radius;
        center1 = center2 = center;
    }

    public TerrainDistortion(float _width, float _depth, Vector2 _center1, Vector2 _center2)
    {
        width = _width;
        depth = _depth;
        center1 = _center1;
        center2 = _center2;
    }

    public float width;
    public Vector2 center1;
    public Vector2 center2;
    public float depth;
}

public partial class MeshDistortTexture : Node2D
{
    
    // as of yet, only update 
    List<TerrainDistortion> DistortQueue = new();

    int PixelPerUnit = 4;

    Rect2 rect = new();
    [Export] SubViewport vp_a_read;
    [Export] SubViewport vp_a_write;

    [Export] SubViewport vp_b_read;
    [Export] SubViewport vp_b_write;

    [Export] SubViewport vp_update_mask;

    [Export] bool DebugExplosionsInQueue = false;

    string coordsDataParam = "distort_coordinates";
    string radiusDataParam = "distort_radius_data";
    string sizeParam = "meter_size";
    string jointViewportParam = "sister_viewport";
    string updateMaskParam = "update_mask";
    string viewportCoordsParam = "viewport_coords";
    string viewportRadiusParam = "viewport_radius";

    public void SetRect(Rect2 _rect)
    {
        rect = _rect;
        SetViewportSizes();

    }

    public void ApplyDistortion(TerrainDistortion distortion)
    {
        DistortQueue.Add(distortion);
    }

    public override void _Ready()
    {
        if (DebugExplosionsInQueue) GetTree().CreateTimer(1).Timeout += OnDebugExplodeTimer;
    }

    private void OnDebugExplodeTimer()
    {
        var ranRad = GD.Randf() * 3;
        var ranPos1 = rect.Size * (new Vector2(GD.Randf(), GD.Randf()));
        var ranPos2 = (rect.Size - ranPos1) * new Vector2(GD.Randf(), GD.Randf());
        var explosion = new TerrainDistortion(ranRad, ranRad, ranPos1, ranPos2);
        DistortQueue.Add(explosion);
        
    }


    public override void _Process(double delta)
    {
        
        if (DistortQueue.Count > 0)
        {
            var dis = DistortQueue[0];
            DistortQueue.RemoveAt(0);

            // split the explosion distortion struct into two separate color-encodable chunks
            Vector4 vp_a_data = new(
                dis.center1.X,
                dis.center1.Y,
                dis.center2.X,
                dis.center2.Y
            );

            Vector2 vp_b_data = new (
                dis.width,
                dis.depth
            );

             
        }
    }


    public void SetViewportSizes()
    {
        List<SubViewport> vp_list = new(){
            vp_a_read, vp_a_write, vp_b_read, vp_b_write, vp_update_mask
        };

        foreach (var vp in vp_list)
        {
            vp.Size = (Vector2I)rect.Size * PixelPerUnit;  // set viewport (and color rect) size in pixels to have certain pixel-per-meter quality
            var colorRect = (ColorRect)vp.GetChild(0);
            var mat = (ShaderMaterial)colorRect.Material;
            mat.SetShaderParameter("meter_size", (Vector2I)rect.Size); // get color rect material and pass rect size in meters to convert UV to a meter distance;
            
            if (vp != vp_update_mask) SetViewportParam(vp, updateMaskParam, vp_update_mask );
            else
            {
                // set the initial viewports that the update mask will read to compare to new distortions
                // draws update mask, which is read by which ever vp is classified as "write"
                SetViewportParam(vp, viewportCoordsParam, vp_a_read); 
                SetViewportParam(vp, viewportRadiusParam, vp_b_read);
            }
        }
        
        SetViewportParam(vp_a_read, jointViewportParam, vp_a_write.GetTexture());
        SetViewportParam(vp_a_write, jointViewportParam, vp_a_read.GetTexture());
        SetViewportParam(vp_b_read, jointViewportParam, vp_b_write.GetTexture());
        SetViewportParam(vp_b_write, jointViewportParam, vp_b_read.GetTexture());


        
    }

    void SetViewportParam(SubViewport vp, string param, Variant value)
    {
        vp.GetChild<ColorRect>(0).SetInstanceShaderParameter(param, value);
    }

    public Godot.ViewportTexture GetTexture()
    {
        return vp_a_read.GetTexture();
    }

    ShaderMaterial GetMat(SubViewport vp)
    {
        var rect = vp.GetChild<ColorRect>(0);
        return (ShaderMaterial)rect.Material;

    }

}
