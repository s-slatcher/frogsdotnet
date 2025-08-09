shader_type spatial;
render_mode depth_draw_always;


uniform float uv_scale = 1.0;
uniform float roughness: hint_range(0.0, 1.0, 0.1) = 0.2;

group_uniforms Colors;
uniform vec4 primary_color : source_color;
uniform vec4 secondary_color : source_color;
group_uniforms;

group_uniforms WaveNormalMap;
uniform sampler2D normalmap_a : hint_normal;
uniform sampler2D normalmap_b : hint_normal;
uniform float normalmap_a_strength : hint_range(0.0, 1.0, 0.01) = 0.5;
uniform float normalmap_b_strength : hint_range(0.0, 1.0, 0.01) = 0.25;
uniform vec2 movement_direction = vec2(0.2, 0.1);
uniform float movement_strength = 0.12;
group_uniforms;

group_uniforms MetaTextures;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_nearest;
uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
group_uniforms;

group_uniforms DepthParameters;
uniform float depth_distance : hint_range(0.0, 20.0, 0.1) = 4.0;
uniform float beers_law: hint_range(0.0, 20.0, 0.1) = 12.0;
uniform float water_transparency : hint_range(0.0, 1.0, 0.01) = 0.05;
group_uniforms;

group_uniforms EdgeDetection;
uniform float edge_scale = 0.1;
uniform float near = 0.5;
uniform float far = 100.0;
uniform vec3 edge_color: source_color;
group_uniforms;

varying vec3 world_pos;

float fresnel(float amount, vec3 normal, vec3 view) {
	return pow(1.0 - clamp(dot( normalize(normal) , normalize(view) ), 0.0, 1.0), amount);
}

float edge(float depth) {
	depth = 2.0 * depth - 1.0;
	return near * far / (far + depth * (near - far));
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	//sample depth and screen textures
	float depth = textureLod(depth_texture, SCREEN_UV, 0.0).r;
	vec3 screen = textureLod(screen_texture, SCREEN_UV, 0.0).rgb;

	//get depth in world pos
	vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth);
	vec4 world = INV_VIEW_MATRIX * INV_PROJECTION_MATRIX * vec4(ndc, 1.0);
	float depth_texture_y = world.y / world.w;
	float vertex_y = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).y;

	//set depth blend
	float depth_blend = clamp((vertex_y - depth_texture_y) / depth_distance, 0.0, 1.0);
	depth_blend = exp(-depth_blend * beers_law);

	//get edge depth
	float z_depth = edge(texture(depth_texture, SCREEN_UV).x);
	float z_pos = edge(FRAGCOORD.z);
	float z_dif = z_depth - z_pos;

	float basic_fresnel = fresnel(3.0, NORMAL, VIEW);

	vec2 uv = UV * uv_scale;
	vec2 uv_movement = movement_direction * TIME * movement_strength;

	vec3 normalmap = texture(normalmap_a, uv + uv_movement ).rgb * normalmap_a_strength;
	normalmap += texture(normalmap_b, uv - uv_movement).rgb * normalmap_b_strength;

	vec3 depth_color = mix(secondary_color, primary_color, depth_blend).rgb;
	vec3 final_color = mix(depth_color.rgb, screen, water_transparency);

	ALBEDO = mix(final_color, screen, depth_blend * (1.0 - basic_fresnel));
	ROUGHNESS = roughness * (1.0 - basic_fresnel);
	NORMAL_MAP = normalmap;
}

//void light() {
	// Called for every pixel for every light affecting the material.
	// Uncomment to replace the default light processing function with this one.
//}
