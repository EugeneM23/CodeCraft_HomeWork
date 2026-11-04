# Regax (Rapax) visual port — design

Date: 2026-11-04
Branch: atomic-upgrade

## Goal

Give the enemy the look of the Rapax creature from the SPrototype project
(`C:\!!!Work\SPrototype`) — mesh, skeleton, animations, hit and death effects —
while every piece of gameplay logic stays the one this project already has.

The source of the look is `_Project/Prefabs/Character/Enemy_Regax.prefab`, which the
request calls "regex". Nothing of its logic is ported: it is a Zenject/`GameObjectContext`
enemy with its own AI, loot, health bar and damage numbers, none of which has a place here.

## Scope decisions

| Decision | Choice |
|---|---|
| Relationship to `Zombie.prefab` | A new `Regax.prefab` alongside it. `Zombie.prefab` is left untouched, so `GameScene` keeps working and the two can be compared. |
| Effects | Only the two prefabs `Enemy_Regax` actually references, with their own dependencies — not the whole 190 MB of asset-store packs. |
| Ragdoll | Not ported. `DeathAnimBehaviour` plays a `Death` state; nothing in this project ever enables a ragdoll, so the ten `CharacterJoint` bones would be dead weight. |
| Colour variant | Red — the one `Enemy_Regax` uses. |

## Assets to copy

Copied with their `.meta` files so GUIDs survive and internal references keep resolving.

**`Assets/3rdParty/Rapax/`** — from `_Project/Content/Characters/Rapax`:

- `SK_Rapax.fbx` (rig: Humanoid, `animationType: 3`, avatar created from the model)
- `Materials/M_Rapax.mat` and the four textures it samples — `T_Rapax_TintRed_BaseColor`,
  `T_Rapax_Normal`, `T_Rapax_Occlusion`, `T_Rapax_Green_Emissive`. Only the red variant is used,
  so the other three materials and seven textures stay behind, as does `Rapax_Mask.mask`:
  `AC_Zombie`'s extra layer uses this project's own `AvatarMask_Body`.
- Animation FBX files, **excluding every `_RM` duplicate** — those carry root motion, and
  movement here is driven by `CharacterMoveBehaviour` moving the transform, not by the clip:
  `IdleBreathe`, `IdleLookAround`, `Walk`, `Run`, `Death`, `GetHitFront/Back/Left/Right`,
  `LeftClawsAttack`, `RightClawsAttack`, `2HitComboClawsAttack`, `JumpClawsAttack`,
  `JumpForward`, `JumpBackwards`, `StrafeLeft`, `StrafeRight`, `Turn90Left`, `Turn90Right`, `Roar`

Not copied: `Rapax_Controller.controller`, `AC_Repax.overrideController`, the four source
variant prefabs. All of them are shaped around the other project's animator graph.

**`Assets/3rdParty/RPG_VFXPack/`** — `HitImpact_VFX_Zombie.prefab`
(guid `39253c93f65e94d32ba771c74993304b`, the `_hitEffect` of `Enemy_Regax`) and only the
materials, textures and shaders it references.

**`Assets/3rdParty/VolumetricBloodFX/`** — `Blood7_02.prefab`
(guid `9b5d74eecd3234c0096fd5605afa4056`, the single entry of `_deathEffects`) and its
own dependencies.

## Animator

At runtime the controller does not come from the prefab: `SwitchAnimatorBehaviour` replaces
it with `weapon.GetAnimationController()` on every weapon change, and the enemy's weapon
`Zombie_Hand.prefab` carries `AC_Zombie`. So the animator work happens on the weapon side.

- `Assets/Game/Animations/Rapax/AC_Rapax.overrideController`, based on `AC_Zombie`, keeping
  its parameters (`IsMoving`, `MoveX`, `MoveZ`, `Attack`, `TakeDamage`, `Death`) and its
  states, with the clips overridden:

  | State | Clip |
  |---|---|
  | Idle | `Rapax@IdleBreathe` |
  | Move | `Rapax@Run` |
  | Attack | `Rapax@RightClawsAttack` |
  | TakeDamage | `Rapax@GetHitFront` |
  | Death | `Rapax@Death` |

- `Assets/Game/Prefabs/Weapons/Regax_Hand.prefab`, a copy of `Zombie_Hand.prefab` whose
  `_animController` points at `AC_Rapax`.

Both the model and the clips are Humanoid, so the clips retarget onto the new avatar and the
graph stays ours.

## Prefab

`Assets/Game/Prefabs/Characters/Regax.prefab`, built from `Zombie.prefab`. The whole Atomic
layer is kept as it stands — `MonoEntity` plus `EnemyCoreInstaller`, `EnemyAnimInstaller`,
`EnemyVfxInstaller`, `EnemyAudioInstaller` and `CharacterBuffInstaller`, with their serialized
tuning. `Zombie.prefab` already groups its children as `<Skeleton>`, `<Skin>`, `<Collider>`,
`<Audio>`, `<Weapons>`, `<VFX>`, `<PatrolPoints>`, and that layout is preserved.

What changes:

- `<Skeleton>` — Rapax bone hierarchy; the `Animator` lives here, and gets the `SK_Rapax`
  avatar and `AC_Rapax`
- `<Skin>` — Rapax `SkinnedMeshRenderer`, material `M_Rapax` (red)
- `<Collider>` — the capsule on `<Collider>` and the one on the root both go from radius 0.3,
  height 1.8 to radius 0.71, height 4.28, centre `(0, 2, 0)`, matching `Enemy_Regax`
- `<VFX>` — `BulletDamageBlood` and `MeleeDamageBlood` become `HitImpact_VFX_Zombie`;
  `DeathBlood` and `DeadBlood` become `Blood7_02`
- `<Weapons>` — holds `Regax_Hand` instead of `Zombie_Hand`
- `<Audio>` and `<PatrolPoints>` — unchanged
- every installer `[SerializeField]` is re-pointed at the new objects

The `EntityID` the pool keys on is derived from the GameObject name
(`EnemyCoreInstaller` strips `(Clone)`), so the prefab name `Regax` is what identifies it.

### How it is built

By an editor script, `Assets/Game/Scripts/Editor/RegaxPrefabBuilder.cs`, exposed as
`Tools/Regax/Build Prefab`. This follows the precedent already set by
`Assets/Game/Scripts/Editor/PlaygroundBuilder.cs`: content that would otherwise be tens of
thousands of lines of hand-edited YAML is described as readable code instead. `Zombie.prefab`
alone is 55 000 lines; editing it by hand is how prefabs get corrupted.

The script is run from the open editor. The generated prefab is then inspected as YAML.

## Scene

`PlaygroundBuilder.BuildEnemies()` switches to `Regax.prefab` for both the standing enemy and
the `SpawnButton` spawn source, and `Playground.unity` is rebuilt from it. `GameScene` is not
touched; the zombie stays there.

## Out of scope

`AC_Zombie` declares a float parameter `MoveZ`, while `CharacterMoveAnimBehaviour` writes
`MoveY`. The second blend-tree axis therefore never receives a value. This predates the port
and belongs to the logic, which stays as it is.

## Assumption: Rapax keeps its native size

Rapax is roughly 4.3 units tall against the zombie's 1.8, and it is ported at scale 1 — shrinking
it to human height would throw away the silhouette the port is for. Two consequences are
expected and are not treated as defects of the port: the creature towers over the 4-unit
playground walls, and melee reach tuned for the zombie will read as short against it. Both live
in the logic and the arena, not in the visuals, so neither is changed here.

---

# What the implementation changed

The sections above were written before the two projects were compared in detail. What follows
is what the port actually ran into, kept for whoever reads this next.

## The projects are on different render pipelines

SPrototype is URP 17; this project is Built-in. Nothing URP could be copied as it stood.

- `M_Rapax` arrived on `OmniShade URP`. This project has the same vendor's built-in variant at
  `Assets/3rdParty/OmniShade/Shaders/OmniShade.shader`, with identical property names, so the
  material is repointed at it and nothing is lost. Worth knowing: this project's copy of
  `OmniShade URP.shader` carries the same GUID as SPrototype's, so the material resolved
  without complaint and would simply have rendered nothing.
- The hit effect's two materials sat on Shader Graphs targeting `UniversalTarget`. Both graphs
  turned out to be three multiplies — the texture's red channel as a mask, the particle's vertex
  colour as tint, an `_Emissive` scale — alpha blended, unlit, double sided. They are rewritten
  as built-in shaders in `Assets/3rdParty/RPG_VFXPack/Shaders/`, declaring the same property
  names (including Shader Graph's generated name for Wave's inline second texture), so the
  materials bind to them unchanged.
- The KriptoFX blood needed nothing: `BFX_Blood` and `BFX_Decal` were hand-written built-in CG
  all along.

## Animation events carry gameplay, and had to be ported too

This was the port's real trap: the swap of clips silently broke things no YAML check would show.

- The enemy's melee damage does not come from the animator. `DamageCastBehaviour` opens a 0.1s
  damage window when it hears `damage_cast_event`, and that event lives inside
  `Zombie_Attack.anim`. A Rapax clip in its place would leave the creature swinging without ever
  dealing damage. The swing is therefore copied out of its FBX into
  `Assets/Game/Animations/Rapax/Rapax_Attack.anim` and the event added there — at the time the
  pack's own animators marked with `CallEvent("DamageCast")`, 0.379s into a 1.333s clip.
- The run clip carried two `CallEvent("Step")` events of the source project's own. Nothing here
  implements `CallEvent`, so Unity logged an error on every stride. They are cleared in the
  clip's import settings; footstep sound here is driven by a timer, not by the animation.
- `Zombie_Death_Handgun`'s `body_fall_event` and `Zombie_Move`'s `move_step_event` have no
  listener anywhere, so nothing was needed for those.

## The death effect needed a driver

`EnemyVfxInstaller` carried two ParticleSystem fields, `_deadBlood` and `_deathBloodVfx`, that
were serialized and wired on `Zombie.prefab` but never read by `Install` — so no character in
this project has ever shown a death effect. The spec said no logic would change; this is the one
place it did, with the user's agreement. The two dead fields are replaced by one `GameObject`
field and `DeathEffectBehaviour`, which switches it on when the death event fires. A GameObject
rather than a ParticleSystem because the volumetric blood is a mesh driven by its own scripts,
and because enabling it is the whole effect — which is also why it sits on the prefab disabled.

## The skin has to live under the Animator

The zombie keeps `<Skin>` beside `<Skeleton>`, and copying that layout left the creature frozen:
the state machine ran, `Run` played, `normalizedTime` advanced, and no bone moved. An Animator
decides whether to cull itself by looking for renderers **in its own hierarchy**; with the skin a
sibling it finds none, concludes it is never on screen, and on `CullUpdateTransforms` stops
writing the bones. On Regax the skin is nested under `<Skeleton>`, as the source project had it.

`Zombie.prefab` has the same arrangement and the same frozen locomotion — confirmed by
instantiating it and measuring a thigh bone across frames. It is left alone, since this port does
not touch that prefab.

## Smaller things the port had to get right

- Unpacking the zombie `Completely` also unpacks its nested weapon instance. `OutermostRoot`
  keeps the nesting, and the weapon and both effects go in as nested prefab instances, so later
  edits to them still reach the enemy.
- The hit effect's asset sits at `(15, 1, 5)` — authored at a position in the other project's
  scene. Left alone it plays the blood off to one side. The effects are placed at the zombie's
  own blood heights scaled by 4.28/1.8.
- The art and the effects arrive on layer 0; the whole prefab is put back on the enemy layer.
- Both effect prefabs carried the source project's `Entity` MonoBehaviour, its Zenject lifetime
  handling. Stripped, along with the `m_Component` entries pointing at it.

## The scene was edited, not rebuilt

`Playground.unity` held hand-made changes — a `Cube` marker on the spawn button and the button
moved out of its corner — that `Tools/Playground/Build` would have discarded. The zombie was
swapped for the creature in place instead, and both `_enemyPrefab` fields repointed: the spawn
button's, and `GameContextInstaller`'s, which drives the automatic spawner. `PlaygroundBuilder`
is updated too, so a future rebuild agrees with the scene.

## The MoveZ / MoveY mismatch, fixed after all

Out of scope twice over — it predates the port and is shared with the zombie — until play showed
its size. `CharacterMoveAnimBehaviour` calls `SetFloat("MoveY")` every frame an enemy moves while
`AC_Zombie` declared `MoveZ`, so Unity logged a warning per enemy per frame: roughly nine thousand
in twelve seconds. The parameter is renamed to `MoveY` in both `AC_Zombie` and `AC_Rapax`. Nothing
read it — no transition condition, no blend tree, checked before renaming — so the rename only
lets the value arrive where the code was already sending it. Play now logs four warnings in all,
none from this.

## Two warnings that are not the port's

Worth writing down so they are not chased again. `Assets/Game/Materials/PolygonParticleFX_Blood.mat`
points at a shader GUID that does not resolve and renders magenta; it was last touched by the
commit that rearranged the project. `[CameraRoot]/[Camera]/cm`, cloned into the playground from
GameScene, carries three missing scripts. Neither is touched by this work.
