// NOTE: Shader automatically converted from Godot Engine 4.4.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode world_vertex_coords,blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

uniform vec4 dirt_color : source_color;
uniform vec4 stone_color : source_color;
uniform vec4 grass_color : source_color;
uniform sampler2D texture_albedo : source_color, filter_linear_mipmap, repeat_enable;
uniform ivec2 albedo_texture_size;
uniform float point_size : hint_range(0.1, 128.0, 0.1);

uniform float roughness : hint_range(0.0, 1.0);
uniform sampler2D texture_metallic : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec4 metallic_texture_channel;
uniform sampler2D texture_roughness : hint_roughness_r, filter_linear_mipmap, repeat_enable;

uniform float specular : hint_range(0.0, 1.0, 0.01);
uniform float metallic : hint_range(0.0, 1.0, 0.01);

uniform vec3 uv1_scale;
uniform vec3 uv1_offset;
uniform vec3 uv2_scale;
uniform vec3 uv2_offset;

const int MAX_EXPLOSIONS = 256;
uniform int explosion_count = 0;
uniform float highest_radius = 10;
uniform vec4 explosion_array[MAX_EXPLOSIONS];

// terrain depth for dithering
uniform float ledge_depth_min;
uniform float ledge_depth_max;
uniform float ledge_depth_height_max;


const vec3 magic = vec3(0.06711056f, 0.00583715f, 52.9829189f);

varying vec3 vertex_pos;
varying vec3 original_normal;


void vertex() {
	UV = UV * uv1_scale.xy + uv1_offset.xy;

	// edge smooothing
	vec3 faceNormColor = CUSTOM0.rgb;
	vec3 norm = (faceNormColor - vec3(0.5, 0.5, 0.5)) * 2.;
	float angle = acos(dot(norm, NORMAL));
	float dotProd = dot(norm,NORMAL);
	float dotMax = cos(PI/4.);
	float angle_intensity = smoothstep(dotMax, 1., dotProd);
	float edgeSmooth = 0.2 ;
	//if (VERTEX.z > -0.01) angle_intensity = 0.;
	VERTEX += NORMAL * (edgeSmooth * (angle_intensity));

	float max_depth = ledge_depth_min + (ledge_depth_max - ledge_depth_min) * (1.0 - (VERTEX.y / ledge_depth_height_max));
	vec3 default_pos = VERTEX;
	VERTEX.z += max_depth;


	original_normal = NORMAL;
	//explosion distort
	for (int i = 0; i < MAX_EXPLOSIONS; i++ )
	{

		vec4 explosion = explosion_array[i];
		if (explosion == vec4(0.)) break;
		
		vec3 pos = explosion.xyz;
		float radius = explosion.w;
		if (radius + pos.x > VERTEX.x + 5.) break;
		vec2 dist_vec = VERTEX.xy - pos.xy;
		if (abs(dist_vec.x) > radius || abs(dist_vec.y) > radius) continue;
		float dist = length(dist_vec);
		if (dist >= radius) continue;
		float depth_change = sqrt( (radius * radius) - (dist * dist) );
		float edgeSmooth = clamp( (radius - dist) / 1., 0., 1.);
      depth_change *= edgeSmooth;

		float new_depth = VERTEX.z - depth_change; //* (1. - abs((default_pos.z / ledge_depth)));
		VERTEX.z = min(VERTEX.z, new_depth);
		if (VERTEX.z == new_depth)
		{
			vec3 new_normal = normalize(vec3(pos.xy, 0) - VERTEX);
			NORMAL = normalize(NORMAL * (1. - edgeSmooth) + new_normal * edgeSmooth);
		}
	}
	vertex_pos = VERTEX;
}

bool binary_condition(int index)
{
	vec4 explosion = explosion_array[index];
	float x_reach = explosion.x + highest_radius;
	return true;
}

int binary_explosion_search(vec3 vertex_position)
{
	//explosion centers sorted by low-to-high X coordinate, find closest
	//to (VERTEX.x - MAX_RADIUS) without getting closer than max radius
	float x_pos = vertex_position.x;
	int left = 0; int right = explosion_count;

	while (left < right)
	{
		int mid = left + (right - left);
		if (binary_condition(mid)) right = mid;
		else left = mid + 1;
	}

	return left;
}



void fragment() {
	vec2 base_uv = UV;

	//vec4 albedo_tex = texture(texture_albedo, base_uv);

	// apply grass based on original normal, canceled out when explosions alter normal
	float is_grass = step(0.35, original_normal.y) * ( 1.0 - step(0.5, abs(original_normal.x)));
	is_grass *= 1.0- step(abs(original_normal.z - NORMAL.z), 0.01) ;
	ALBEDO = mix(dirt_color.rgb, grass_color.rgb, is_grass );


	// apply stone color based on current depth, canceled out when original normal coorspsonds to an edge
	float max_depth = ledge_depth_min + (ledge_depth_max - ledge_depth_min) * (1.0 - (vertex_pos.y / ledge_depth_height_max));
	float stoneiness = 1. - smoothstep(-2. + max_depth, -0.25 + max_depth, vertex_pos.z);
	float is_edge = step( 0.05 , abs(original_normal.z));
	ALBEDO = mix(ALBEDO, stone_color.rgb, stoneiness * is_edge);



	float metallic_tex = dot(texture(texture_metallic, base_uv), metallic_texture_channel);
	METALLIC = metallic_tex * metallic;
	SPECULAR = specular;

	vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	float roughness_tex = dot(texture(texture_roughness, base_uv), roughness_texture_channel);
	ROUGHNESS = roughness_tex * roughness;

	// lerp from min to max terrain depth comparing vertex height to max height
	// discard pixels that go beyond max depth
	float depth_fade = smoothstep(-1.15, -1., vertex_pos.z);
	if (depth_fade < 0.001 || depth_fade < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
		discard;
	}

	//if (NORMAL.y > 0.5) ALBEDO = vec3(0.2,0.7,0);

	//float depth_recolor = smoothstep(-0.5, -0.2, vertex_pos.z);
	//if (depth_recolor < 0.001 || depth_recolor < fract(magic.z * fract(dot(FRAGCOORD.xy, magic.xy)))) {
		//ALBEDO = stone_color.rgb;
	//}

}
