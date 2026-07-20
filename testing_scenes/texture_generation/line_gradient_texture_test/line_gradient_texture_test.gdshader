shader_type canvas_item;


uniform sampler2D line_texture: filter_nearest, source_color;
uniform vec2 rect_size;
uniform float line_width;




void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	
	vec4 line_color = texture(line_texture, UV);
	vec2 line_pos = line_color.rg * rect_size;
	float line_alpha = line_color.a;
	float line_radius = line_width/2.;
	
	float dist = distance(UV * rect_size, line_pos);
	float dist_alpha = smoothstep(line_radius, 0, dist);
	
	dist_alpha *= line_alpha;
	

	COLOR = vec4(vec3(0),dist_alpha);
	
	
	

	
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
