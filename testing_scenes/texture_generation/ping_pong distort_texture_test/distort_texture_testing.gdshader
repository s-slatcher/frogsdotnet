shader_type canvas_item;

uniform sampler2D distort_texture;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	COLOR = texture(distort_texture, UV);
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
