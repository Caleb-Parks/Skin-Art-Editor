extends Sprite2D
## Static shop sprite; offsets applied from C#.

func apply_skin(texture: Texture2D, sprite_offset: Vector2, sprite_scale: float) -> void:
	self.texture = texture
	self.offset = sprite_offset
	self.scale = Vector2(sprite_scale, sprite_scale)
	self.visible = true
