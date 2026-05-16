shader_type canvas_item;
uniform sampler2D viewport_read_source;
uniform vec3 new_explosion;
varying vec2 position_2d;

void vertex() {
	position_2d = vec2(VERTEX.x, VERTEX.y);
}

float depth_from_color(vec3 color, vec2 position){
	vec2 explode_center = color.xy;
	float rad = color.z;
	float dist = distance(position, explode_center);
	if (dist >= rad) return 0.;
	float depth_from_explosion = sqrt( (rad * rad) - (dist * dist) );
	return depth_from_explosion;
}

void fragment() {
	vec4 prev_color = texture(viewport_read_source, UV);  // default to re-render past state viewport
	
	float prev_depth = depth_from_color(prev_color.xyz, position_2d);
	float next_depth = depth_from_color(new_explosion, position_2d);
	
	float color_mix = step(prev_depth, next_depth);
	vec3 color = mix(prev_color.xyz, new_explosion, color_mix);
	COLOR = vec4(color, 1);
	
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
