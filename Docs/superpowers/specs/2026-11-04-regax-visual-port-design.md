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

**`Assets/Game/3rdParty/Rapax/`** — from `_Project/Content/Characters/Rapax`:

- `SK_Rapax.fbx` (rig: Humanoid, `animationType: 3`, avatar created from the model)
- `Materials/M_Rapax*.mat` (4), `Textures/T_Rapax_*.png` (11), `Rapax_Mask.mask`
- Animation FBX files, **excluding every `_RM` duplicate** — those carry root motion, and
  movement here is driven by the NavMesh agent and the transform, not by the clip:
  `IdleBreathe`, `IdleLookAround`, `Walk`, `Run`, `Death`, `GetHitFront/Back/Left/Right`,
  `LeftClawsAttack`, `RightClawsAttack`, `2HitComboClawsAttack`, `JumpClawsAttack`,
  `JumpForward`, `JumpBackwards`, `StrafeLeft`, `StrafeRight`, `Turn90Left`, `Turn90Right`, `Roar`

Not copied: `Rapax_Controller.controller`, `AC_Repax.overrideController`, the four source
variant prefabs. All of them are shaped around the other project's animator graph.

**`Assets/Game/3rdParty/VFX/HitImpact/`** — `HitImpact_VFX_Zombie.prefab`
(guid `39253c93f65e94d32ba771c74993304b`, the `_hitEffect` of `Enemy_Regax`) and only the
materials, textures and shaders it references.

**`Assets/Game/3rdParty/VFX/VolumetricBlood/`** — `Blood7_02.prefab`
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
- `<Collider>` — capsule resized to the Rapax silhouette
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
