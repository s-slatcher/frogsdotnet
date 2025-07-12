// NOTE: Shader automatically converted from Godot Engine 4.4.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_toon;

uniform sampler2D texture_edge : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D texture_debug_test : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D texture_grass : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D texture_noise_mask : source_color, filter_linear_mipmap, repeat_enable;

uniform vec4 albedo : source_color;
uniform sampler2D texture_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform ivec2 albedo_texture_size;
uniform float point_size : hint_range(0.1, 128.0, 0.1);

uniform float roughness : hint_range(0.0, 1.0);
uniform sampler2D texture_metallic : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec4 metallic_texture_channel;
uniform sampler2D texture_roughness : hint_roughness_r, filter_linear_mipmap, repeat_enable;

uniform float specular : hint_range(0.0, 1.0, 0.01);
uniform float metallic : hint_range(0.0, 1.0, 0.01);
varying vec3 uv1_triplanar_pos;

uniform float uv1_blend_sharpness : hint_range(0.0, 150.0, 0.001);
varying vec3 uv1_power_normal;


uniform vec3 uv1_scale;
uniform vec3 uv1_offset;
uniform vec3 uv2_scale;
uniform vec3 uv2_offset;

varying vec3 vertex_pos;

// 
uniform float edge_depth;

// circle for wireframe reveal
uniform vec2 circle_center;
uniform float circle_radius;


// WIREFRAME && BARY
const vec3 vectors[3] = {
	vec3(1.0, 0.0 ,0.0),
	vec3(0.0, 1.0 ,0.0),
	vec3(0.0, 0.0 ,1.0)
};
varying vec3 baryCoord;
varying vec4 vertexCustom0;

uniform vec4 modelColor : source_color;
uniform vec4 wireframeColor : source_color;
uniform float width : hint_range(0.0, 15.) = 1.;
uniform float modelOpacity : hint_range(0.0, 1.0) = 1.;


// WIREFRAME ANIMATION
uniform vec2 ripple_center;
uniform float time_start;
const float radius_per_second = 12.0;
const float ripple_width_per_second = 2.;

// dithering constant
const vec3 magic = vec3(0.06711056f, 0.00583715f, 52.9829189f);

void vertex() {
	baryCoord = vectors[VERTEX_ID % 3];
	vec3 normal = NORMAL;
	vertex_pos = VERTEX.xyz;
	vertexCustom0 = CUSTOM0;
	TANGENT = vec3(0.0, 0.0, -1.0) * abs(normal.x);
	TANGENT += vec3(1.0, 0.0, 0.0) * abs(normal.y);
	TANGENT += vec3(1.0, 0.0, 0.0) * abs(normal.z);
	TANGENT = normalize(TANGENT);

	BINORMAL = vec3(0.0, 1.0, 0.0) * abs(normal.x);
	BINORMAL += vec3(0.0, 0.0, -1.0) * abs(normal.y);
	BINORMAL += vec3(0.0, 1.0, 0.0) * abs(normal.z);
	BINORMAL = normalize(BINORMAL);

	// UV1 Triplanar: Enabled
	uv1_power_normal = pow(abs(NORMAL), vec3(uv1_blend_sharpness));
	uv1_triplanar_pos = VERTEX * uv1_scale + uv1_offset;
	uv1_power_normal /= dot(uv1_power_normal, vec3(1.0));
	uv1_triplanar_pos *= vec3(1.0, -1.0, 1.0);
}

vec4 triplanar_texture(sampler2D p_sampler, vec3 p_weights, vec3 p_triplanar_pos) {
	vec4 samp = vec4(0.0);
	samp += texture(p_sampler, p_triplanar_pos.xy) * p_weights.z;
	samp += texture(p_sampler, p_triplanar_pos.xz) * p_weights.y;
	samp += texture(p_sampler, p_triplanar_pos.zy * vec2(-1.0, 1.0)) * p_weights.x;
	return samp;
}

void fragment() {
	vec4 albedo_tex = triplanar_texture(texture_albedo, uv1_power_normal, uv1_triplanar_pos);
	//ALBEDO = albedo.rgb * albedo_tex.rgb;
	ALBEDO = albedo.rgb;
	vec4 edge_color = texture(texture_edge, UV);
	vec4 debug_color = texture(texture_debug_test, UV);
	float noise_uv_scaler = 0.05f; // at 0.05, texture repeats every 20 meters
	vec4 noise_mask_color = texture(texture_noise_mask, vertex_pos.xy * noise_uv_scaler);



	float grass_noise_threshold = pow(1.0f - smoothstep(0, 0.25, edge_color.r),0.5);
	float grass_color_step = step(grass_noise_threshold, noise_mask_color.r);
	vec4 grass_tex = triplanar_texture(texture_grass, uv1_power_normal, uv1_triplanar_pos);
	ALBEDO = mix(ALBEDO, grass_tex.rgb, grass_color_step);

	// BARY COORDS AND WIREFRAME
	vec3 dBaryCoordX = dFdx(baryCoord);
	vec3 dBaryCoordY = dFdy(baryCoord);
	vec3 dBaryCoord  = sqrt(dBaryCoordX*dBaryCoordX + dBaryCoordY*dBaryCoordY);

	// non derivative bary coord
	float d = distance(baryCoord, vec3(1./3.));

	// distance 'd' is 0 closest to center
	// smoothstep 0-0.1 range gives <1 very close to center
	// 1.0 minus that val gives rating a zero val for anything beyond close-to-center
	float strength = 1.0 - smoothstep(0.0, 0.1, d);


	vec3 remap = smoothstep(
		vec3(0.0),
		dBaryCoord * width,
		baryCoord
	);

	 //WIREFRAME RIPPLE ANIMATION
	//float anim_time_elapsed = TIME - time_start;
	//float current_rad = (anim_time_elapsed) * radius_per_second;
	//vec2 triangle_center = COLOR.xy * 100.;
	//float current_ripple_width = (anim_time_elapsed) * ripple_width_per_second + 5.;
	//float dist_from_ripple_center = length(triangle_center - ripple_center);
	//float ripple_step_out = 1.0 - step(current_rad + current_ripple_width, dist_from_ripple_center); // comes out to zero if greater than max rad
	//float ripple_step_in = step(current_rad, dist_from_ripple_center); // comes out to zero if less than min rad
	//float ripple_step = ripple_step_in * ripple_step_out;
	
	vec2 triangle_center = vertexCustom0.xy;
	//vec2 triangle_center = COLOR.xy * 100.; // tri center coords divided by 1000 in mesh gen
	float dist_to_center = length(circle_center - triangle_center);
	float dither_edge = circle_radius;
	float dither_full = circle_radius * 0.0;
	float wire_full = circle_radius * 0.95;
	float dist_smooth = pow(smoothstep(dither_full, dither_edge, dist_to_center), 10.);
	

	//ripple_step = 1.;

	float closestEdge = min(min(remap.x, remap.y), remap.z);
//
	vec3 wireframe_albedo = mix(wireframeColor.xyz, ALBEDO, closestEdge);
	float wireframe_alpha = mix(1., modelOpacity, closestEdge);
	//

	ALBEDO = mix(ALBEDO, wireframe_albedo, 1.0 - dist_smooth);

	float fade_depth_mult = smoothstep(edge_depth-0.25, edge_depth, vertex_pos.z);
	float fade = ( (dist_smooth ) + (1. - closestEdge) )* fade_depth_mult;
	
	if (fade < 0.001 || fade < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
		discard;
	}
	//ALPHA = mix(1., 0., strength);

	//METALIC AND ROUGHNESS
	float metallic_tex = dot(triplanar_texture(texture_metallic, uv1_power_normal, uv1_triplanar_pos), metallic_texture_channel);
	METALLIC = metallic_tex * metallic;
	SPECULAR = specular;

	vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	float roughness_tex = dot(triplanar_texture(texture_roughness, uv1_power_normal, uv1_triplanar_pos), roughness_texture_channel);
	ROUGHNESS = roughness_tex * roughness;

	//ALPHA_SCISSOR_THRESHOLD = 0.1;
	ALPHA_ANTIALIASING_EDGE = 0.5;



}
