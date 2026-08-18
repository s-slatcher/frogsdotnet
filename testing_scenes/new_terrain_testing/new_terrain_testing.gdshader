shader_type spatial;

void vertex() {
	vec3 world_position = (MODELVIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	if (world_position.z == 0.) VERTEX.z = 10.;
}

void fragment() {
	// Called for every pixel the material is visible on.
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
