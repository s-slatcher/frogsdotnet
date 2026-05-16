shader_type canvas_item;

// one piece of the distort data, the two center coordinates defining a tunnel
uniform vec4 distort_coordinates;

// mask for when to draw new explosion data or keep previous frames'
uniform sampler2D update_mask; 

// the copy of this texture holding last frames data 
uniform sampler2D sister_texture; 

uniform vec2 meter_size;


void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	vec2 meter_pos = UV * meter_size; 
	float mask = texture(update_mask, UV).r;
	
	vec4 prev_color = texture(sister_texture, UV);
	COLOR = mix(prev_color, distort_coordinates, mask);	
}



//void light() {
//	// Called for every pixel for every light affecting the CanvasItem.
//	// Uncomment to replace the default light processing function with this one.
//}
