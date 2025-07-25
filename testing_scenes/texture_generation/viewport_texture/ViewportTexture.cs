using Godot;
using System;

public partial class ViewportTexture : Node2D
{

    public override void _Ready()
    {
        var vp_1 = GetNode<SubViewport>("SubViewport");
        var vp_2 = GetNode<SubViewport>("SubViewport2");

         
    }

}
