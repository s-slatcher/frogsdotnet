using Godot;
using Vector2 = Godot.Vector2;
using System;
using System.Collections.Generic;


public struct TerrainDistortion
{
    public TerrainDistortion(float _radius, Vector2 center)
    {
        radius = _radius;
        depthMult = 1;
        center1 = center2 = center;
    }

    public TerrainDistortion(float _radius, float _depthMult, Vector2 _center1, Vector2 _center2)
    {
        radius = _radius;
        depthMult = _depthMult;
        center1 = _center1;
        center2 = _center2;
    }

    public float radius;
    public float depthMult;
    public Vector2 center1;
    public Vector2 center2;
    
}

public partial class MeshDistortTexture : Node2D
{
    
    [Signal]
    public delegate void TextureUpdatedEventHandler(Godot.ViewportTexture texture);


    // as of yet, only update 
    List<TerrainDistortion> DistortQueue = new();


    int PixelPerUnit = 6;

    Rect2 rect = new();
    [Export] SubViewport vp_a_read;
    [Export] SubViewport vp_a_write;

    // [Export] SubViewport vp_b_read;
    // [Export] SubViewport vp_b_write;


    [Export] bool DebugExplosionsInQueue = false;

   

    // rect size is in same units as distort data, multiplied by pixel per unit to get total resolution of distort texture
    public void SetRect(Rect2 _rect)
    {
        rect = _rect;
        SetupViewports();

    }

    public void ExplosionStressTest()
    {
        Vector2 lastPos = new();
        Vector2 nextPos = new();
        float rad = 4;
        float length = 1;
        for (int i = 0; i < rect.Size.X / (rad*2+2); i++)
        {
            nextPos.X += (rad*2+2);
            nextPos.Y = 0;
            lastPos = nextPos;
            for (int j = 0; j < (rect.Size.Y / length); j++)
            {
                nextPos.Y += length;
                var distort = new TerrainDistortion(rad, 0.5f, lastPos, nextPos);
                lastPos = nextPos;
                ApplyDistortion(distort);
            }
        }
    }

    


    public void ApplyDistortion(TerrainDistortion distortion)
    {
        DistortQueue.Add(distortion);
    }

    public override void _Ready()
    {
        if (DebugExplosionsInQueue) GetTree().CreateTimer(1).Timeout += OnDebugExplodeTimer;
        // if (DebugExplosionsInQueue) GetTree().CreateTimer(2).Timeout += ExplosionStressTest;
        SetRect(new Rect2(){Size  = new Vector2(100,100)});
    }

    private void OnDebugExplodeTimer()
    {
        var ranRad = (GD.Randf() * 10f) + 1f;
        var ranPos1 = rect.Size * (new Vector2(GD.Randf(), GD.Randf()));
        var ranPos2 = ranPos1 + ( new Vector2( 5, 5) );
        var explosion = new TerrainDistortion(ranRad, 0.4f, ranPos1, ranPos2);
        DistortQueue.Add(explosion);
        GetTree().CreateTimer(1.5f).Timeout += OnDebugExplodeTimer;
        
        GD.Print("new explosion at ", ranPos1 );
    }


    public override void _Process(double delta)
    {
        vp_a_read.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        vp_a_write.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;

        if (DistortQueue.Count > 0)
        {
            var dis = DistortQueue[0];
            DistortQueue.RemoveAt(0);

           
            // update ALL viewport shaders with latest explosion data;
            SetNewDistortParams(dis);
            UpdateViewports();
            SwapReadWriteViewports();

            EmitSignal(SignalName.TextureUpdated, vp_a_read.GetTexture());

        }
    }

    private void SwapReadWriteViewports()
    {
        (vp_a_read, vp_a_write) = (vp_a_write, vp_a_read);


        //testing texture
        var testMat = (ShaderMaterial)GetNode<ColorRect>("TestRect").Material;
        testMat.SetShaderParameter("distort_texture", vp_a_read.GetTexture());
    }


    private void UpdateViewports()
    {
        vp_a_write.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }



    private void SetNewDistortParams(TerrainDistortion dis)
    {

       SetParam(vp_a_write, "pos_a", dis.center1);
       SetParam(vp_a_write, "pos_b", dis.center2);
       SetParam(vp_a_write, "rad", dis.radius);
       SetParam(vp_a_write, "depth_mult", dis.depthMult);
    }

    public void SetupViewports()
    {
        vp_a_write.Size = vp_a_read.Size = (Vector2I)(PixelPerUnit * rect.Size);  
        SetParam(vp_a_read, "meter_size", rect.Size);
        SetParam(vp_a_write, "meter_size", rect.Size);

        SetParam(vp_a_write, "sister_texture", vp_a_read.GetTexture());
        SetParam(vp_a_read, "sister_texture", vp_a_write.GetTexture());

        // testing texture rect
        var testRect = GetNode<ColorRect>("TestRect");
        testRect.Size = rect.Size * PixelPerUnit;   
        var mat = (ShaderMaterial)testRect.Material;
        mat.SetShaderParameter("meter_size", rect.Size);

             
    }


    void SetParam(SubViewport vp, string param, Variant value)
    {
        GetMat(vp).SetShaderParameter(param, value);
    }

    // public Godot.ViewportTexture GetTexture()
    // {
    //     return vp_a_read.GetTexture();
    // }

    ShaderMaterial GetMat(SubViewport vp)
    {
        var rect = vp.GetChild<ColorRect>(0);
        return (ShaderMaterial)rect.Material;

    }

}
