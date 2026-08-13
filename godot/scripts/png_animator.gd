extends Node2D
## Wishkeeper-style pose crossfade for combat (static PNG poses).

@export var idle_texture: Texture2D
@export var attack_texture: Texture2D
@export var cast_texture: Texture2D
@export var hurt_texture: Texture2D
@export var die_texture: Texture2D
@export var fade_duration: float = 0.1
@export var attack_duration: float = 0.42
@export var cast_duration: float = 0.38
@export var hurt_duration: float = 0.42
@export var bottom_padding_px: float = 32.0
@export var dead_scale: float = 1.0

@onready var _current_sprite: Sprite2D = $CurrentSprite
@onready var _next_sprite: Sprite2D = $NextSprite

var _active_sprite: Sprite2D
var _inactive_sprite: Sprite2D
var _fade_tween: Tween
var _state_name: String = ""
var _return_generation: int = 0
var _is_dead: bool = false
var _applied: bool = false


func _ready() -> void:
	_active_sprite = _current_sprite
	_inactive_sprite = _next_sprite
	_active_sprite.z_index = 0
	_inactive_sprite.z_index = 0
	if idle_texture != null:
		_set_sprite_state(_active_sprite, "Idle")
		_state_name = "Idle"
	_active_sprite.visible = true
	_active_sprite.modulate = Color.WHITE
	_inactive_sprite.visible = false
	_inactive_sprite.modulate = Color(1.0, 1.0, 1.0, 0.0)


## Called from C# after textures/offsets are assigned.
func apply_skin(
	idle: Texture2D,
	attack: Texture2D,
	cast: Texture2D,
	hurt: Texture2D,
	die: Texture2D,
	padding_px: float
) -> void:
	idle_texture = idle
	attack_texture = attack
	cast_texture = cast
	hurt_texture = hurt
	die_texture = die
	bottom_padding_px = padding_px
	_applied = true
	if _current_sprite == null:
		return
	_active_sprite = _current_sprite
	_inactive_sprite = _next_sprite
	_set_sprite_state(_active_sprite, "Idle")
	_active_sprite.visible = true
	_active_sprite.modulate = Color.WHITE
	_inactive_sprite.visible = false
	_inactive_sprite.modulate = Color(1.0, 1.0, 1.0, 0.0)
	_state_name = "Idle"
	_is_dead = false


func play_trigger(trigger: String) -> void:
	var state_name := _normalize_trigger(trigger)
	if state_name.is_empty():
		return

	_return_generation += 1
	var generation := _return_generation

	if _is_dead and state_name != "Idle" and state_name != "Dead":
		return

	if state_name == "Dead":
		_is_dead = true
	elif state_name == "Idle":
		_is_dead = false

	if state_name == "Attack" and _state_name == "Attack":
		_restart_attack()
	elif state_name != _state_name:
		_crossfade_to(state_name)

	_state_name = state_name
	_schedule_idle_return(state_name, generation)


func _restart_attack() -> void:
	if _fade_tween != null and _fade_tween.is_running():
		_fade_tween.kill()
		_settle_visible_sprite()

	_set_sprite_state(_active_sprite, "Idle")
	_active_sprite.visible = true
	_active_sprite.modulate = Color.WHITE
	_crossfade_to("Attack")


func _normalize_trigger(trigger: String) -> String:
	match trigger:
		"Idle":
			return "Idle"
		"Attack", "Attack_Sovereign", "attack_sovereign":
			return "Attack"
		"Cast", "PowerUp":
			return "Cast"
		"Hit":
			return "Hit"
		"Dead":
			return "Dead"
		_:
			return ""


func _crossfade_to(state_name: String) -> void:
	var texture := _get_texture(state_name)
	if texture == null:
		return
	_crossfade_to_texture(texture, _get_sprite_scale(state_name))


func _crossfade_to_texture(texture: Texture2D, scale_value: float) -> void:
	if _fade_tween != null and _fade_tween.is_running():
		_fade_tween.kill()
		_settle_visible_sprite()

	var outgoing := _active_sprite
	var incoming := _inactive_sprite
	_set_sprite_texture(incoming, texture, scale_value)

	outgoing.visible = true
	incoming.visible = true
	outgoing.z_index = 0
	incoming.z_index = 0
	move_child(incoming, get_child_count() - 1)
	incoming.modulate = Color(1.0, 1.0, 1.0, 0.0)

	_fade_tween = create_tween().set_parallel()
	_fade_tween.tween_property(outgoing, "modulate:a", 0.0, fade_duration)
	_fade_tween.tween_property(incoming, "modulate:a", 1.0, fade_duration)
	_fade_tween.finished.connect(func() -> void:
		outgoing.visible = false
		outgoing.z_index = 0
		outgoing.modulate = Color(1.0, 1.0, 1.0, 0.0)
		incoming.visible = true
		incoming.z_index = 0
		incoming.modulate = Color.WHITE
		_active_sprite = incoming
		_inactive_sprite = outgoing
	)


func _settle_visible_sprite() -> void:
	var current_alpha := _current_sprite.modulate.a
	var next_alpha := _next_sprite.modulate.a
	if next_alpha > current_alpha:
		_active_sprite = _next_sprite
		_inactive_sprite = _current_sprite
	else:
		_active_sprite = _current_sprite
		_inactive_sprite = _next_sprite

	_active_sprite.visible = true
	_active_sprite.z_index = 0
	_active_sprite.modulate = Color.WHITE
	_inactive_sprite.visible = false
	_inactive_sprite.z_index = 0
	_inactive_sprite.modulate = Color(1.0, 1.0, 1.0, 0.0)


func _set_sprite_state(sprite: Sprite2D, state_name: String) -> void:
	var texture := _get_texture(state_name)
	if texture == null:
		return
	_set_sprite_texture(sprite, texture, _get_sprite_scale(state_name))


func _set_sprite_texture(sprite: Sprite2D, texture: Texture2D, scale_value: float) -> void:
	sprite.texture = texture
	sprite.scale = Vector2(scale_value, scale_value)
	sprite.offset = Vector2(0.0, bottom_padding_px / scale_value - float(texture.get_height()) * 0.5)


func _get_texture(state_name: String) -> Texture2D:
	match state_name:
		"Idle":
			return idle_texture
		"Attack":
			return attack_texture
		"Cast":
			return cast_texture
		"Hit":
			return hurt_texture
		"Dead":
			return die_texture
		_:
			return null


func _get_sprite_scale(state_name: String) -> float:
	if state_name == "Dead":
		return dead_scale
	return 1.0


func _schedule_idle_return(state_name: String, generation: int) -> void:
	var duration := _get_return_duration(state_name)
	if duration <= 0.0:
		return
	_return_to_idle_after_delay(duration, generation)


func _get_return_duration(state_name: String) -> float:
	match state_name:
		"Attack":
			return attack_duration
		"Cast":
			return cast_duration
		"Hit":
			return hurt_duration
		_:
			return 0.0


func _return_to_idle_after_delay(duration: float, generation: int) -> void:
	await get_tree().create_timer(duration).timeout
	if generation != _return_generation or _is_dead:
		return
	play_trigger("Idle")
