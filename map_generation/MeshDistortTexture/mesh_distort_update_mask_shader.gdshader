shader_type canvas_item;

uniform sampler2D viewport_coords;
uniform sampler2D viewport_radius;

uniform vec4 distort_coordinates;
uniform vec2 distort_radius_data;

uniform vec2 meter_size;

struct distort_data {
	vec2 center1; 
	vec2 center2; 
	float xy_rad; 
	float depth_rad; 
};

// CHATGPT WRITTEN FUNCTION
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
	//vec4 coords_color = texture(viewport_coords, uv);
	//vec4 distort_radius_color = texture(viewport_radius, uv);
	
	//vec2 center1 = coords_color.rg;
	//vec2 center2 = coords_color.ba;
	//float xy_radius = distort_radius_color.r;
	//float depth_radius = distort_radius_color.g;  // FOR NOW IGNORING "SQUISH" FACTOR
	
	vec2 meter_pos = uv * meter_size;
	vec2 relative_center = closestPointOnSegment(meter_pos, center1, center2);
	float dist_from_cent = distance(meter_pos, relative_center);
	
	if (dist_from_cent >= xy_rad) return 0.;
	float depth_from_explosion = sqrt( (xy_rad * xy_rad) - (dist_from_cent * dist_from_cent ) );
	return depth_from_explosion;
}




void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	
	vec4 coords_color = texture(viewport_coords, UV);
	vec4 distort_radius_color = texture(viewport_radius, UV);
	float prev_depth = get_depth_at_frag(
		UV, coords_color.xy, coords_color.zw, distort_radius_color.x, distort_radius_color.y
	);
	
	float new_depth = get_depth_at_frag(
		UV, distort_coordinates.xy, distort_coordinates.zw, distort_radius_data.x, distort_radius_data.y
	);
	
	float update_mix = step(prev_depth, new_depth - 0.001); // small epsilon to prevent updates at 0 depth change
	COLOR = vec4( vec3(update_mix), 1.);
}



//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
