shader_type canvas_item;

uniform sampler2D viewport_tex_1;
uniform sampler2D viewport_tex_2;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec4 tex_1 = texture(viewport_tex_1, UV);
	vec4 tex_2 = texture(viewport_tex_2, UV);
	vec3 combined = (tex_1.rgb + tex_2.rgb);
	float alpha = max(tex_1.a, tex_2.a);
	vec4 color = vec4(combined, alpha);
	COLOR = color;
	
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
