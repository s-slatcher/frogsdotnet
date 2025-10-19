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


varying vec3 updated_vertex;
varying vec3 default_normal;
varying vec3 default_vertex;
varying float max_depth;

const int MAX_EXPLOSIONS = 500;
const float MAX_RADIUS = 12.;
uniform vec4 explosion_array[MAX_EXPLOSIONS];

uniform float edge_smooth = 0.;
uniform float depth_mulitplier = 1.;

uniform float rockiness = 1.;
varying float rock_level;
uniform float rock_scale = 0.5;

varying vec3 debug_color;

// pseudo random number
const vec3 magic = vec3(0.06711056f, 0.00583715f, 52.9829189f);

// random 0-1 vector offset
vec3 hash3(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p * fract(p.x * p.y * p.z));
}

// voronoi cell defined by position and distance from vertex
struct vorSite {
	float d;
	vec3 pos;
};

// finds array position to begin search for explosions that affect vertex 
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

vec3 norm_from_color(vec3 color)
{
	return (color - vec3(0.5, 0.5, 0.5)) * 2.;
}

void voronoi_sites(in vec3 position, out vorSite s1, out vorSite s2)
{
	float local_cell_scale = rock_scale;
	vec3 cellCorner = floor(position * local_cell_scale);
	vec3 s_base = cellCorner + hash3(cellCorner);
	
	s1 = vorSite(1e9, vec3(0));
	s2 = vorSite(1e9, vec3(0));
	
	for(int x = -1; x <= 1; x++)
	{
	for (int y = -1; y <= 1; y++)
	{
	for (int z = 0; z <= 1; z++)
	{
		vec3 adjCorner = cellCorner + vec3(float(x),float(y),float(z));
		vec3 adjSite = (hash3(adjCorner) + adjCorner) / local_cell_scale;
		
		// random skipping of some sites to generate bigger cells
		//if (fract(adjSite.x) < 0.3) continue;
		
		float dist = distance(adjSite, position);
		if (dist < s1.d) 
		{
			//s3 = s2;
			s2 = s1;
			s1 = vorSite(dist, adjSite); 
      } else if (dist < s2.d) {
         //s3 = s2;
			s2 = vorSite(dist, adjSite);	
		} 
		//else if (dist < s3.d)
		//{
			////s3 = vorSite(dist, adjSite);
		//}
	}}}
}

void voronoi_projection(in vec3 position, in vorSite site1, in vorSite site2, 
								out vec3 proj_position, out vec3 proj_normal) {
	
	proj_normal = normalize(site2.pos - site1.pos); 
	float d = 0.5 * dot(proj_normal, site1.pos + site2.pos);
	float planeDist1 = dot(proj_normal, position) - d;
	proj_position = position - planeDist1 * proj_normal;
	
}

void vertex() {

	// CUSTOM data from vertex
	max_depth = CUSTOM1.r * -1.;
	vec3 faceNormal = norm_from_color(CUSTOM0.rgb);
	
	// store initial vertex
	default_normal = NORMAL;
	default_vertex = VERTEX;
	debug_color = vec3(1.);
	
	// EDGE BUBBLING (BROKEN)
	// ----------------
	//float angle = acos(dot(faceNormal, NORMAL));
	//float dotProd = dot(faceNormal, NORMAL);
	//float dotMax = cos(PI/4.);
	//float angle_intensity = smoothstep(dotMax, 1., dotProd);
	//COLOR = vec4(1,1,angle_intensity,1);
	//if (VERTEX.z > -0.01) angle_intensity = 0.;
	// disable is faceNormColor wasnt set 
	//if (faceNormColor != vec3(0,0,0) ) VERTEX += NORMAL * (edge_smooth * (angle_intensity));
	
	
	
	// EXPLOSION DISTORT
	// ----------------
	int explode_start = explosion_start_binary_search(VERTEX.x);
	
	for (int i = explode_start; i < MAX_EXPLOSIONS; i++ )
	{
		vec4 explosion = explosion_array[i];
		vec3 ex_pos = explosion.xyz;
		float ex_radius = explosion.w;
		float rad_buffer = -0.1;
		vec2 dist_vec = VERTEX.xy - ex_pos.xy;
		
		// break after reaching end of explosions list
		if (explosion == vec4(0.)) break; 
		
		// explosions sorted left to right
		// when explosion found beyond the max radius in +x dir., no explosions further down list can reach
		if ((ex_pos.x - MAX_RADIUS) > VERTEX.x) break;
		
		// continue if vertex not affected by explosion
		if (abs(dist_vec.x) > ex_radius || abs(dist_vec.y) > ex_radius) continue;
		
		float dist = length(dist_vec);
		vec3 new_vert_pos = default_vertex;
		
		// snap to explosion edge within a small buffer to round edges (no depth change)
		// else - calculate depth like normal
		if (dist - ex_radius > rad_buffer) {
			float diff = dist - ex_radius;
			vec3 snap_vec = (ex_radius - 2.* rad_buffer) * normalize( vec3(VERTEX - ex_pos) );
			vec3 snap_pos = snap_vec + ex_pos;
		}
		else {
			float depth_change = sqrt( (ex_radius * ex_radius) - (dist * dist) ) * depth_mulitplier;
			new_vert_pos.z -= depth_change;
		}
		
		if (new_vert_pos.z <= VERTEX.z)
		{
			float difference = abs(new_vert_pos.z - VERTEX.z);
			float depth_smooth = smoothstep(0, 3, difference);
			vec3 new_normal = normalize(vec3(ex_pos.xy, 0) - VERTEX);
			VERTEX = mix(VERTEX, new_vert_pos, depth_smooth);
			NORMAL = normalize(mix(NORMAL, new_normal, depth_smooth));
		}
	}
	
	
	
	// Voronoi distort
	vorSite s1;
	vorSite s2;
	vec3 voronoi_proj_pos;
	vec3 voronoi_proj_norm;
	
	
	float depth = default_vertex.z - VERTEX.z;
	
	float rock_start = 2.5;
	float rock_max = 4.;
	rock_level = clamp( smoothstep(rock_start, rock_max, depth ), 0.2, 1 );
	float local_rockiness = rock_level * rockiness;
	//local_rockiness = 1.;
	//if (depth < 0.) debug_color = vec3(0);
	
	if (local_rockiness > 0.01)
	{
		voronoi_sites(VERTEX, s1, s2);
		voronoi_projection(VERTEX, s1, s2, voronoi_proj_pos, voronoi_proj_norm);
		VERTEX = mix(VERTEX, voronoi_proj_pos, local_rockiness);
		NORMAL = mix(NORMAL, voronoi_proj_norm, local_rockiness);
	}
	
	
	
	// updated vertex pos for frag shader
	updated_vertex = VERTEX;
	
	
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

vec3 get_flat_normal(vec3 position)
{
	vec3 dpdx = dFdx(position);
   vec3 dpdy = dFdy(position);
   return normalize(cross(dpdx, dpdy)) * -1.;
}

void fragment() {
	
	// Applies stone color based on current depth 
	// canceled out when original normal coorspsonds to an edge (small Z contribution)
	float stone_start = default_vertex.z - 1.;
	float max_stone_delta = 6.;
	float min_stone_delta = 1.;
	
	float z_delta = abs(default_vertex.z - updated_vertex.z);
	float stone_level = smoothstep(min_stone_delta, max_stone_delta, z_delta);
	
	float stoneiness = 1. - smoothstep(stone_start-2., stone_start, updated_vertex.z);	
	float is_edge = step( 0.5 , abs(default_normal.z));
	//ALBEDO = mix(ALBEDO, stone_color.rgb, stoneiness * is_edge);
	ALBEDO = mix(dirt_color.rgb, stone_color.rgb, sqrt(stone_level) * is_edge);
	
	float is_exploded = step( 0.01, abs(default_normal.z - updated_vertex.z));
	float edge_color = texture(grass_texture, UV).r - is_exploded; 
	float noise_mask_color = texture(grass_noise_texture, UV).r;
	float grass_noise_threshold = pow(1.0f - smoothstep(0, 0.75, edge_color),1);
	float grass_color_step = step(grass_noise_threshold, noise_mask_color);
	vec4 grass_tex = grass_color;
	
	ALBEDO = mix(ALBEDO, grass_tex.rgb, grass_color_step);
	
	// flat normal calc 
	NORMAL = mix(NORMAL, get_flat_normal(updated_vertex), rock_level);
	
	// discard pixels that go beyond max depth
	float depth_fade = smoothstep(-0.15 + max_depth, -0.01 + max_depth, updated_vertex.z);
	if (depth_fade < 0.001 || depth_fade < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
		discard;
	}
	

	////Metalic texturing (disabled)
	//float metallic_tex = dot(triplanar_texture(texture_metallic, uv1_power_normal, uv1_triplanar_pos), metallic_texture_channel);
	//METALLIC = metallic_tex * metallic;
	//SPECULAR = specular;
//
	//vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	//float roughness_tex = dot(triplanar_texture(texture_roughness, uv1_power_normal, uv1_triplanar_pos), roughness_texture_channel);
	//ROUGHNESS = roughness_tex * roughness;
}
