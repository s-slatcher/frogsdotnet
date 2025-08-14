extends MeshInstance3D

 
@export var pos_source : Node3D
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pass # Replace with function body.


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if (pos_source != null):
		position.x = pos_source.position.x
		position.z = pos_source.position.z
	pass
