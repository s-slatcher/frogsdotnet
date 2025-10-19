// NOTE: Shader automatically converted from Godot Engine 4.4.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode world_vertex_coords, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

// terrain colors
uniform vec4 stone_color : source_color;

// grass updating viewport texture

// dirt crack normal texture

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

uniform vec4 explosion;
uniform float maxDepth;

uniform float rock_intensity;
uniform float voronoiCellScale;
varying float distFromSite;
varying vec3 worldPos;
varying vec3 testColor;

// pseudo random number
const vec3 magic = vec3(0.06711056f, 0.00583715f, 52.9829189f);

vec3 hash3(vec3 p) {
    p = fract(p * 0.3183099 + 0.1);
    p *= 17.0;
    return fract(p * fract(p.x * p.y * p.z));
}

struct vorSite {
	float d;
	vec3 pos;
};

void voronoi_sites(in vec3 position, out vorSite s1, out vorSite s2)
{
	float local_cell_scale = voronoiCellScale;
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
	float rand = fract(magic.z * fract(dot(VERTEX.xy, magic.xy)));
	vec3 pos = explosion.xyz;
	float radius = explosion.w;
	
	vec2 dist_vec = VERTEX.xy - pos.xy;
	
	float dist = length(dist_vec);
	
	if (dist < radius)
	{
		float depth_change = sqrt( (radius * radius) - (dist * dist) );
		float new_depth = VERTEX.z - depth_change;
		vec3 new_normal = normalize(vec3(pos.xy, 0) - VERTEX);
		VERTEX.z = new_depth;
		NORMAL = new_normal;
	}
	
	vorSite s1;
	vorSite s2;
	vec3 voronoi_proj_pos;
	vec3 voronoi_proj_norm;
	
	voronoi_sites(VERTEX, s1, s2);
	voronoi_projection(VERTEX, s1, s2, voronoi_proj_pos, voronoi_proj_norm);
	
	VERTEX = mix(VERTEX, voronoi_proj_pos, rock_intensity);
	NORMAL = mix(NORMAL, voronoi_proj_norm, rock_intensity);
	
	worldPos = VERTEX;
	
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
	
	NORMAL = get_flat_normal(worldPos);
	
	ALBEDO = albedo.rgb + testColor;
	
	float depth_fade = smoothstep(-0.15 + maxDepth, -0.01 + maxDepth, worldPos.z);
	if (depth_fade < 0.001 || depth_fade < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
		discard;
	}
	
	METALLIC = metallic;
	SPECULAR = specular;
	ROUGHNESS = roughness;
}
