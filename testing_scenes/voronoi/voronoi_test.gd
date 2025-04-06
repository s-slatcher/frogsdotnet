extends Node2D
@onready var polygon_2d: Polygon2D = $Polygon2D

func _ready() -> void:
	var polygon = polygon_2d.polygon
	var voronoi : VoronoiSweepline = VoronoiSweepline.new()
	voronoi.generate(polygon, [0,750,0,750])
	voronoi.relax()
	var cell_polygons = [];
	
	for cell in voronoi.cells:
		print(cell[0])
		
