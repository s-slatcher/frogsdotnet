shader_type canvas_item;
render_mode blend_mix;
// one piece of the distort data, the two center coordinates defining a tunnel
uniform vec3 distort_data;


uniform vec2 pos_a;
uniform vec2 pos_b;
uniform float rad;
uniform float depth_mult;
 


// the copy of this texture holding last frames data 
uniform sampler2D sister_texture: filter_nearest; 

uniform vec2 meter_size;

// IGNORING DEPTH MULT ALPHA CHANNEL NOT WORKING IDK
vec4 encode_color(vec2 center, float radius, float depth_multiplier) {
	float div = max(meter_size.x, meter_size.y);
	return vec4( center/div, radius/div, 1) ;   
}
vec4 decode_color(vec4 color) {return vec4( color.xyz * max(meter_size.x, meter_size.y), color.a) ; }


vec2 closestPointOnSegment(vec2 p, vec2 a, vec2 b)
{
    vec2 ab = b - a;

    // Project AP onto AB, normalized by |AB|^2
    float t = dot(p - a, ab) / dot(ab, ab);

    // Clamp so it stays on the segment
    t = clamp(t, 0.0, 1.0);

    return a + ab * t;
}

float get_depth_at_frag(vec2 uv, vec2 center, float xy_rad, float depth_multiplier)
{
	
	vec2 meter_pos = uv * meter_size;
	float dist_from_cent = distance(meter_pos, center);
	if (dist_from_cent >= xy_rad) return 0.;
	float depth_from_explosion = sqrt( (xy_rad * xy_rad) - (dist_from_cent * dist_from_cent ) );
	return depth_from_explosion;
}


void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec2 meter_pos = UV * meter_size; 
	
	vec4 old_color = texture(sister_texture, UV);
	vec4 old_distort = decode_color(old_color);
	
	vec2 old_pos = old_distort.rg;
	float old_rad = old_distort.b;
	float old_depth_mult = old_distort.a;
	
	
	
	
	vec2 new_center = closestPointOnSegment(meter_pos, pos_a, pos_b);
	
	vec4 new_color = encode_color(new_center, rad, depth_mult);
	
	
	float old_depth = get_depth_at_frag(UV, old_pos, old_rad, old_depth_mult);
	float new_depth = get_depth_at_frag(UV, new_center, rad, depth_mult);
	
	COLOR = mix(old_color, new_color, step(old_depth, new_depth - 0.001));
	
	//COLOR = vec4(new_depth/10., 0., 0., 1.);
	
	//float r = distance(meter_pos, new_distort_center) / 100.;
	//COLOR = vec4(vec3(r),1);
	
}



//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
