shader_type canvas_item;
uniform sampler2D grass_texture;
uniform vec4 albedo : source_color;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec4 grass_tex = texture(grass_texture, UV);
	COLOR = grass_tex;

}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
