shader_type canvas_item;
render_mode blend_mix;

uniform vec2 meter_size;
uniform sampler2D distort_texture: filter_nearest, source_color;

vec4 encode_color(vec4 data) { return vec4( data.xyz/max(meter_size.x, meter_size.y), 1) ; }
vec4 decode_color(vec4 color) {return vec4( color.xyz * max(meter_size.x, meter_size.y), 1) ; }

vec2 closestPointOnSegment(vec2 p, vec2 a, vec2 b)
{
    vec2 ab = b - a;

    // Project AP onto AB, normalized by |AB|^2
    float t = dot(p - a, ab) / dot(ab, ab);

    // Clamp so it stays on the segment
    t = clamp(t, 0.0, 1.0);

    return a + ab * t;
}

float get_depth_at_frag(vec2 uv, vec2 center1, vec2 center2, float xy_rad, float depth_rad)
{
	vec2 meter_pos = uv * meter_size;
	//vec2 relative_center = closestPointOnSegment(meter_pos, center1, center2);
	vec2 relative_center = center1;
	float dist_from_cent = distance(meter_pos, relative_center);
	
	if (dist_from_cent >= xy_rad) return 0.;
	float depth_from_explosion = sqrt( (xy_rad * xy_rad) - (dist_from_cent * dist_from_cent ) );
	return depth_from_explosion;
}

void vertex() {
	
}

void fragment() {
	vec2 meter_pos = UV * meter_size;
	vec4 distort_color = texture(distort_texture, UV);
	vec4 distort_data = decode_color(distort_color);
	
	
	vec2 distort_center = distort_data.xy;
	float distort_rad = distort_data.z;
	float depth = get_depth_at_frag(UV, distort_center, distort_center, distort_rad, distort_rad);
	float relative_depth = 1. - clamp( depth/15., 0, 1 ); // presume max depth to be 15.
	COLOR = vec4( vec3(relative_depth), 1);
	// Called for every pixel the material is visible on.
}

//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
