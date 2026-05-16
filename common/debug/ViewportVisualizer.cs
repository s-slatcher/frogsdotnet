using Godot;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;

public partial class ViewportVisualizer : Node2D
{

    [Export] SubViewport[] vpList = new SubViewport[10];

    public override void _Ready()
    {
        CallDeferred("GrabViewports");
        
    }

    void GrabViewports()
    {
        var gridCon = GetNode<GridContainer>("GridContainer");        
        foreach(var vp in vpList)
        {
            if (vp == null) continue;
            var texRect = new TextureRect(){Texture = vp.GetTexture()};
            gridCon.AddChild(texRect);
        }

       


    }


}
