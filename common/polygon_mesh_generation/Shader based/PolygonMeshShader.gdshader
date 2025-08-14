// NOTE: Shader automatically converted from Godot Engine 4.4.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode world_vertex_coords, blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

// terrain colors
uniform vec4 dirt_color : source_color;
uniform vec4 dirt_color_alt: source_color;
uniform vec4 stone_color : source_color;
uniform vec4 grass_color : source_color;

// grass updating viewport texture
uniform sampler2D grass_texture;
uniform sampler2D grass_noise_texture;

// dirt crack normal texture
uniform sampler2D dirt_normal;

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

varying float side_effect;

varying vec3 vertex_pos;
varying vec3 original_normal;
varying vec3 original_vertex;
varying float max_depth;

const int MAX_EXPLOSIONS = 500;
const float MAX_RADIUS = 12.;
uniform vec4 explosion_array[MAX_EXPLOSIONS];

uniform float edge_smooth = 0.;
uniform float depth_mulitplier = 1.;

// used for pseudo random number
const vec3 magic = vec3(0.06711056f, 0.00583715f, 52.9829189f);


int explosion_start_binary_search(float x_pos)
{

	int left = 0;
	int right = MAX_EXPLOSIONS;
	
	while(left < right)
	{
		int mid = left + (right - left) / 2;
		vec4 mid_explode = explosion_array[mid];
		if (mid_explode == vec4(0.)) mid_explode.w = 99999.;
		float right_edge = mid_explode.x + mid_explode.w;
		if (right_edge > x_pos) right = mid;
		else left = mid + 1;
	}
	return left;
	
}

void vertex() {
	
	// CUSTOM0: one of the face normals for vertex, used to compare angle to edge-smoothed normal to identify edges
	// CUSTOM1:
	//		 r :  Z level (times -1) that this vertex can be pushed back to, begin discarding pixels beyond this point
	//	   g,b:  undefined 
	
	original_normal = NORMAL;
	max_depth = CUSTOM1.r * -1.;
	
	
	// EDGE BUBBLING 
	// ----------------
	vec3 faceNormColor = CUSTOM0.rgb;
	vec3 norm = (faceNormColor - vec3(0.5, 0.5, 0.5)) * 2.;
	float angle = acos(dot(norm, NORMAL));
	float dotProd = dot(norm,NORMAL);
	float dotMax = cos(PI/4.);
	float angle_intensity = smoothstep(dotMax, 1., dotProd);
	//if (VERTEX.z > -0.01) angle_intensity = 0.;
	
	// disable is faceNormColor wasnt set 
	if (faceNormColor != vec3(0,0,0) ) VERTEX += NORMAL * (edge_smooth * (angle_intensity));
	
	original_vertex = VERTEX;
	// EXPLOSION DISTORT
	// ----------------
	
	int explode_start = explosion_start_binary_search(VERTEX.x);
	
	for (int i = explode_start; i < MAX_EXPLOSIONS; i++ )
	{
		
		vec4 explosion = explosion_array[i];
		vec3 pos = explosion.xyz;
		float radius = explosion.w;
		
		if (explosion == vec4(0.)) break;
		if ((pos.x - MAX_RADIUS) > VERTEX.x) break;
		
		vec2 dist_vec = VERTEX.xy - pos.xy;
		if (abs(dist_vec.x) > radius || abs(dist_vec.y) > radius) continue;
		
		float dist = length(dist_vec);
		if (dist >= radius) continue;
		float depth_change = sqrt( (radius * radius) - (dist * dist) ) * depth_mulitplier;
		
		//float depth_smooth = smoothstep(0, 2, abs(depth_change) );
		
		//float edgeSmooth = clamp( (radius - dist) / 1., 0., 1.);
		
		float new_depth = original_vertex.z - depth_change;
		
		if (new_depth < VERTEX.z)
		{
			float difference = abs(new_depth - VERTEX.z);
			float depth_smooth = smoothstep(0, 3, difference);
			vec3 new_normal = normalize(vec3(pos.xy, 0) - VERTEX);
			VERTEX.z = mix(VERTEX.z, new_depth, depth_smooth);
			NORMAL = normalize(mix(NORMAL, new_normal, depth_smooth));
		}
	}
	
	// updated vertex pos for frag shader
	vertex_pos = VERTEX;
	
	
	
	
	
	
	// Triplanar UV
	vec3 normal = NORMAL;
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
	
	vec3 dirt_noise_tex = texture(dirt_normal, UV).rgb;
	
	float dirt_noise_thresh = 0.1;

	float dirt_noise_mult = step(dirt_noise_thresh, dirt_noise_tex.r);
	ALBEDO = mix(dirt_color.rgb, dirt_color_alt.rgb, 1.0 - dirt_noise_mult);
	// Applies stone color based on current depth 
	// canceled out when original normal coorspsonds to an edge (small Z contribution)
	float stone_start = original_vertex.z - 1.;
	
	float max_stone_delta = 6.;
	float min_stone_delta = 1.;
	float z_delta = abs(original_vertex.z - vertex_pos.z);
	
	float stone_level = smoothstep(min_stone_delta, max_stone_delta, z_delta);
	
	float stoneiness = 1. - smoothstep(stone_start-2., stone_start, vertex_pos.z);	
	float is_edge = step( 0.5 , abs(original_normal.z));
	//ALBEDO = mix(ALBEDO, stone_color.rgb, stoneiness * is_edge);
	ALBEDO = mix(ALBEDO, vec3(0.1, 0.1, 0.1), sqrt(stone_level) * is_edge);
	
	float is_exploded = step( 0.01, abs(original_vertex.z - vertex_pos.z));
	float edge_color = texture(grass_texture, UV).r - is_exploded; 
	float noise_mask_color = texture(grass_noise_texture, UV).r;
	float grass_noise_threshold = pow(1.0f - smoothstep(0, 0.75, edge_color),1);
	float grass_color_step = step(grass_noise_threshold, noise_mask_color);
	vec4 grass_tex = grass_color;
	
	
	ALBEDO = mix(ALBEDO, grass_tex.rgb, grass_color_step);
	
	// lerp from min to max terrain depth comparing vertex height to max height
	// discard pixels that go beyond max depth
	float depth_fade = smoothstep(-0.15 + max_depth, -0.01 + max_depth, vertex_pos.z);
	if (depth_fade < 0.001 || depth_fade < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
		discard;
	}
	
	// Triplanar texturing (disabled)
	//vec4 albedo_tex = triplanar_texture(texture_albedo, uv1_power_normal, uv1_triplanar_pos);
	//ALBEDO = albedo.rgb * albedo_tex.rgb;

	// Metalic texturing (disabled)
	//float metallic_tex = dot(triplanar_texture(texture_metallic, uv1_power_normal, uv1_triplanar_pos), metallic_texture_channel);
	//METALLIC = metallic_tex * metallic;
	SPECULAR = specular;

	vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	float roughness_tex = dot(triplanar_texture(texture_roughness, uv1_power_normal, uv1_triplanar_pos), roughness_texture_channel);
	ROUGHNESS = roughness_tex * roughness;
}
