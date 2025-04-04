using Godot;
using System;

public partial class EdgeTextureGenerator : GodotObject
{

    // goals:
    // shaders need a texture to help decide where to draw grass -
    // simple approach -- brute force, draw each pixel based on closest edge line 
    // could use some spatial hashing (just divide into columns for simplicity)
    // already have a filterLinesForRect function -- so each pixel gets sorted into one of the columns, and reads each line for distance
    // texture is filled in with 1-0 in red channel for proximity within a limit (10 meters?)
    // pixel density is something like half the expected minimum size of a quad

    // each column grabs anything with its bounds + the edge limit (10m)

    // If i want to have drooping grass, then I would need to code in special colors to indicate that the color is pointing towards a grass floor
    // this texture map would also need to be effected by explosions, since exploded terrain can't be grassy
    // I may also want to just use multiple textures, have the shader read from each one, since not each textures needs to be the same resolution

    //
}
