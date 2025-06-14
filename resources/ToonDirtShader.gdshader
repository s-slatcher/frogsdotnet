// NOTE: Shader automatically converted from Godot Engine 4.4.stable.mono's StandardMaterial3D.

shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_disabled, diffuse_burley, specular_toon;

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

void vertex() {
	vec3 normal = NORMAL;
	vertex_pos = VERTEX.xyz;

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
	//ALBEDO = edge_color.rgb;
	//ALBEDO = noise_mask_color.rgb;
	//elements, grass texture, grass noise mask, and edge texture
	// grass noise mask decides how much grass texture shines through based on black threshold
	// black threshold lowers (down to 0) based on the edge texture

	float grass_noise_threshold = pow(1.0f - smoothstep(0, 0.25, edge_color.r),0.5);
	float grass_color_step = step(grass_noise_threshold, noise_mask_color.r);
	vec4 grass_tex = triplanar_texture(texture_grass, uv1_power_normal, uv1_triplanar_pos);
	ALBEDO = mix(ALBEDO, grass_tex.rgb, grass_color_step);

	//ALBEDO = edge_color.rgb;
	//ALBEDO = vec3(UV, 1);
	//ALBEDO = debug_color.rgb;

	float metallic_tex = dot(triplanar_texture(texture_metallic, uv1_power_normal, uv1_triplanar_pos), metallic_texture_channel);
	METALLIC = metallic_tex * metallic;
	SPECULAR = specular;

	vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	float roughness_tex = dot(triplanar_texture(texture_roughness, uv1_power_normal, uv1_triplanar_pos), roughness_texture_channel);
	ROUGHNESS = roughness_tex * roughness;
}
