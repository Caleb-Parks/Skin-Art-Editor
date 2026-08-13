extends Control
## Static character-select background filled from a runtime texture.

@onready var _bg: TextureRect = $Bg


func apply_skin(texture: Texture2D) -> void:
	if _bg == null:
		_bg = get_node_or_null("Bg") as TextureRect
	if _bg == null or texture == null:
		return
	_bg.texture = texture
