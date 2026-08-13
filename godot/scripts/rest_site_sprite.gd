extends Node2D
## Static rest/campfire sprite. One texture is reused for all acts/forms.

@export var rest_texture: Texture2D
@export var visible_bounds: Rect2 = Rect2(153.0, 125.0, 767.0, 1077.0)
@export var seat_anchor: Vector2 = Vector2(0.5, 0.6)
@export var display_offset: Vector2 = Vector2(-173.983, 150.047)
@export var sprite_scale: float = 0.792
@export var mirrored_inward_pct: float = -0.01

@onready var _sprite: Sprite2D = $Sprite


func _ready() -> void:
	call_deferred("_refresh_texture")


func _process(_delta: float) -> void:
	_sync_mirror_with_control_root()


func apply_skin(
	texture: Texture2D,
	bounds: Rect2,
	anchor: Vector2,
	offset: Vector2,
	scale_value: float
) -> void:
	rest_texture = texture
	visible_bounds = bounds
	seat_anchor = anchor
	display_offset = offset
	sprite_scale = scale_value
	_refresh_texture()


func _refresh_texture() -> void:
	if rest_texture == null or _sprite == null:
		return

	_sprite.texture = rest_texture
	_sprite.centered = true
	_sprite.offset = _texture_offset(rest_texture)
	_sprite.position = Vector2.ZERO
	_sprite.scale = Vector2.ONE
	_sprite.modulate = Color.WHITE
	_sync_mirror_with_control_root()


func _texture_offset(texture: Texture2D) -> Vector2:
	var texture_size := texture.get_size()
	var anchor_px := visible_bounds.position + visible_bounds.size * seat_anchor
	return texture_size * 0.5 - anchor_px


func _sync_mirror_with_control_root() -> void:
	var direction := 1.0
	var control_root := get_parent().get_node_or_null("ControlRoot") as Control
	if control_root != null and control_root.scale.x < 0.0:
		direction = -1.0

	scale = Vector2(sprite_scale * direction, sprite_scale)

	var x := display_offset.x * direction
	if direction < 0.0 and mirrored_inward_pct != 0.0:
		var height_px := 1024.0
		if rest_texture != null:
			height_px = float(rest_texture.get_height())
		x -= mirrored_inward_pct * height_px * sprite_scale

	position = Vector2(x, display_offset.y)
