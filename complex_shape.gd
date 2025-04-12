class_name ComplexShape extends Resource

var polygon : Array
var negative_polygons: Array

func _init(_polygon: Array, _negative_polygons: Array) -> void:
	polygon = _polygon
	negative_polygons = _negative_polygons
