extends Node
static var total_merges : int = 0

func area_of_poly(poly: Array) -> float:
	var area: float = 0
	var tris : Array = Geometry2D.triangulate_polygon(poly)
	for i:int in range(2, len(tris), 3):
		var tri_arr: Array = [
			poly[tris[i]],
			poly[tris[i-1]],
			poly[tris[i-2]]
		]
		area += area_of_triangle(tri_arr)
	
	return area

func area_of_triangle(tri:Array) -> float:
	var a : float = (tri[0] - tri[1]).length()
	var b : float = (tri[1] - tri[2]).length()
	var c : float = (tri[2] - tri[0]).length()
	var s : float = ( a + b + c )/2
	return sqrt( s * (s-a) * (s-b) * (s-c) )
	
func rect_containing_points(poly: Array) -> Rect2:
	if poly == []: poly.append(Vector2(0,0))
	var base_val_x : float = poly[0].x
	var base_val_y : float = poly[0].y
	var max_x := base_val_x
	var max_y := base_val_y
	var min_x := base_val_x
	var min_y := base_val_y
	for v:Vector2 in poly:
		if v.x > max_x: max_x = v.x
		if v.y > max_y: max_y = v.y
		if v.x < min_x: min_x = v.x
		if v.y < min_y: min_y = v.y
	
	return Rect2(Vector2(min_x, min_y), Vector2(max_x - min_x, max_y - min_y))

# from a polygon, returns array containing the polys line segments
func line_segments_from_poly(poly: Array) -> Array:
	var line_seg := []
	
	for i:int in range(len(poly)):
		var p1 : Vector2 = poly[i]
		var p2 : Vector2 = poly[i+1] if i+1 < len(poly) else poly[0]
		line_seg.append([p1,p2])
	
	return line_seg
		
func encode_normal_to_color(vec : Vector3, alpha: float) -> Color:
	vec = vec.normalized()
	vec += Vector3(1,1,1)
	vec *= 0.5
	return Color(vec.x, vec.y, vec.z, alpha)
	
func decode_normal_from_color(color: Color) -> Vector3:
	var vec := Vector3(color.r, color.g, color.b)
	vec *= 2
	vec -= Vector3(1,1,1)
	return vec

func get_circle_poly(pos: Vector2, radius: float, sides: int) -> Array:
	var rad_per_side: float = (2*PI) / sides
	var circle_poly := []
	for i in sides:
		circle_poly.append((Vector2(radius, 0).rotated(i * rad_per_side) + pos))
	return circle_poly

func add_depth(vec2: Vector2, depth: float = 0) -> Vector3:
	return Vector3(vec2.x, vec2.y, depth)

func strip_depth(vec3: Vector3) -> Vector2:
	return Vector2(vec3.x, vec3.y)

func translate_poly(poly: Array, translation: Vector2) -> Array:
	var new_poly := []
	for p : Vector2 in poly:
		new_poly.append(p + translation)
	return new_poly

func rotate_poly(poly: Array, radians: float, pivot_point_offset: Vector2 = Vector2.ZERO) -> Array:
	var new_poly := []
	for p: Vector2 in poly:
		new_poly.append( (p - pivot_point_offset).rotated(radians) + pivot_point_offset)
	return new_poly
	
func scale_poly(poly: Array, scaler: Vector2 = Vector2(1,1)) -> Array:
	var new_poly := []
	for p: Vector2 in poly:
		new_poly.append(p * scaler)
	return new_poly

func carve_poly_sets(base_polys: Array, carve_polys: Array) -> Array:
	var carved_poly_total := base_polys.duplicate()
	for carve_poly: Array in carve_polys:
		var new_carve_poly_total := []
		for poly: Array in carved_poly_total:
			var clip_result: Array = clip_polygons_recursive(poly, carve_poly)
			new_carve_poly_total += clip_result
		carved_poly_total = new_carve_poly_total
	return carved_poly_total
	
## returns array of complex shapes -- only one if merge suceeds, or returns both original shapes on failure
func merge_complex_shapes(shape_1: ComplexShape, shape_2: ComplexShape) -> Array:
	
	var merge_result : Array = Geometry2D.merge_polygons(shape_1.polygon, shape_2.polygon)
	
	if len(merge_result) == 1 or Geometry2D.is_polygon_clockwise( merge_result[1] ):
		var merged_poly : Array = merge_result[0]
		var combined_carve_polys: Array = []
		
		# The combined shapes clip away at each others negative-space polygons
		for neg_poly: Array in shape_1.negative_polygons:
			combined_carve_polys += clip_polygons_recursive(neg_poly, shape_2.polygon)
		for neg_poly: Array in shape_2.negative_polygons:
			combined_carve_polys += clip_polygons_recursive(neg_poly, shape_1.polygon)
		
		# if merge resulted in one or more holes, all are added to carve polys 
		if len(merge_result) > 1:
			merge_result.pop_at(0)
			combined_carve_polys += merge_result
		
		for poly:Array in combined_carve_polys:
			if Geometry2D.is_polygon_clockwise(poly): poly.reverse()
			
		var new_complex_shape := ComplexShape.new(merged_poly, combined_carve_polys)
		return [ new_complex_shape ]
		
	return [shape_1, shape_2]

func get_complex_shapes_from_polygons( polygon_list: Array ) -> Array[ComplexShape]:
	#convert each polygon to a complex shape
	var base_complex_shapes: Array[ComplexShape] = []
	for poly:Array in polygon_list: base_complex_shapes.append( ComplexShape.new(poly, []) )
	return recursive_complex_shape_merge(base_complex_shapes)

func recursive_complex_shape_merge(complex_shapes: Array[ComplexShape]) -> Array[ComplexShape]:
	var num_of_shapes: int = len(complex_shapes)
	var combinations_checked := []
	for i:int in num_of_shapes:
		combinations_checked.append([i])
	
	for i:int in num_of_shapes:
		for j:int in num_of_shapes:
			if combinations_checked[j].has(i): continue
			combinations_checked[i].append(j)
			
			var merge_result : Array = merge_complex_shapes(complex_shapes[i], complex_shapes[j])
			
			#if not overlapping both returned unmerged, check next combination
			if len(merge_result) == 2: continue
			
			#else was overlapping, remove both shapes, insert merged shape
			#push new merged shape to front of reduced list and recurse
			var reduced_shape_list : Array[ComplexShape]
			for s:int in len(complex_shapes):
				if s != j and s != i: reduced_shape_list.append(complex_shapes[s])
			
			reduced_shape_list.push_front(merge_result[0])
			return recursive_complex_shape_merge(reduced_shape_list)
	
	# reached when total shapes reduced to one (or more) non-overlaping shapes
	return complex_shapes
				

func complex_shape_merge(poly_list: Array, carve_poly_list: Array = [] ) -> Dictionary:
	
	var polygons: Array = poly_list.duplicate()
	
	for i:int in range(1, len(poly_list), 1):
		var base_poly : Array = polygons[0]
		var merge_candidate : Array = polygons[i]
		var merge : Array = Geometry2D.merge_polygons(base_poly, merge_candidate)
		
		total_merges += 1
		polygons[0] = Array(merge[0])
		
		if len(merge) == 1:
			var new_carve_poly_list := []
			for carve_poly: Array in carve_poly_list:
				new_carve_poly_list += clip_polygons_recursive(carve_poly, merge_candidate)
			polygons.pop_at(i)
			return complex_shape_merge(polygons, new_carve_poly_list)
		else:
			if Geometry2D.is_polygon_clockwise(merge[1]):
				merge.pop_at(0)
				carve_poly_list += Array(merge)
				polygons.pop_at(i)
				return complex_shape_merge(polygons, carve_poly_list)
			

	return {
		"base_poly": polygons[0],
		"carve_polys": carve_poly_list
	}

func clip_polygons_recursive(base_poly: Array, clipping_poly: Array) -> Array:
	var clipped_polygons: Array = Geometry2D.clip_polygons(base_poly, clipping_poly)
		
	if len(clipped_polygons) <= 1: return clipped_polygons
	if Geometry2D.is_polygon_clockwise(clipped_polygons[1]):
		clipped_polygons.resize(0)
		for split_poly: Array in _split_polygon(base_poly, clipping_poly):
			clipped_polygons += clip_polygons_recursive(split_poly, clipping_poly)
	return clipped_polygons

func _split_polygon(base_poly: Array, clip_poly: Array) -> Array:
	var mid : Vector2 = _avg_position(clip_poly)
	var size : Vector2 = rect_containing_points(base_poly).size
	
	var split_poly_1 : Array = [ Vector2(mid.x - size.x, mid.y - size.y), Vector2(mid.x - size.x, mid.y + size.y),
		Vector2(mid.x, mid.y + size.y), Vector2(mid.x, mid.y - size.y)]
	var split_poly_2 : Array = translate_poly(split_poly_1, Vector2(size.x, 0))
	
	return clip_polygons_recursive(base_poly, split_poly_1) + clip_polygons_recursive(base_poly, split_poly_2)

func get_grid_cell_poly(corner_pos: Vector2, cell_size: Vector2) -> Array:
	
	var grid_poly: Array = [
		corner_pos,
		corner_pos + Vector2(cell_size.x, 0) ,
		corner_pos + cell_size,
		corner_pos + Vector2(0, cell_size.y),
	]
	return grid_poly

func _avg_position(array: Array) -> Vector2:
	var sum := Vector2()
	for p:Vector2 in array:
		sum += p
	return sum/len(array)

func subdivide_rect(rect : Rect2) -> Array:
	assert(rect.size.x == rect.size.y, "subdivide_rect() assumes square rect2's")
	var subdivided_rects := []
	var sub_size: float = rect.size.x / 2
	for i:int in 2:
		for j: int in 2:
			var size_vec := Vector2(sub_size, sub_size)
			var quad_corner := rect.position + Vector2(i * sub_size, j * sub_size) 
			subdivided_rects.append( Rect2(quad_corner, size_vec) )
	return subdivided_rects
	
func circle_intersects_rectangle(rect: Rect2, circle_center: Vector2, circle_rad: float ) -> bool:
	var clamped_center := Vector2(
		clamp(circle_center.x, rect.position.x, rect.end.x),
		clamp(circle_center.y, rect.position.y, rect.end.y)
	)
	return circle_rad > (circle_center - clamped_center).length()

# averages the normal vector of the two polygon faces the point is part of


# returns an array of points with wrapping indices (for 1d arrays like polygon point lists)
func get_adjacent_indices(arr: Array, index: int) -> Array:
	var adj_points: Array = []
	adj_points.append( arr[index-1 if index-1 > -1 else arr.size() - 1] )
	adj_points.append( arr[index+1 if index+1 < arr.size() else 0] )
	return adj_points

	
	
	
