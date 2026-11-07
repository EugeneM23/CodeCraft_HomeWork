# Regax gibs — the creature comes apart on the killing hit

## Goal

When a Regax dies, its body splits into seven ragdolled pieces that fly away from whatever killed
it in random clusters, starting from the pose the creature was in. A tank shell kills it in one
hit and throws the pieces a little harder than a bullet does.

The seven pieces: the head, the left and right halves of the torso (cut vertically along the
spine, tail included), the left and right arms, and the left and right legs.

## Art: cut in Maya

`Tools/Maya/RegaxGibs/` (see its README) holds four scripts. They run in Maya through
commandPort `:7005` and turn `SK_Rapax.fbx` into `Assets/Game/Models/Regax/SK_Rapax_Gibs.fbx`.

- **Split.** Each face goes to the group that holds most of its skin weight: head (Neck, Head,
  jaw), an arm (UpperArm → fingers), a leg (Thigh → toes), or torso (everything else, the
  clavicles included). Along these borders the source mesh has clean single loops of 21–29 edges.
- **Torso halves.** The torso is cut by plane X = 0, which runs along the model's centre edge
  loop. In Maya, +X is the creature's left.
- **Caps.**
  - Every hole is closed with `polyCloseBorder`.
  - Limb, neck and hip caps are fanned with `polyPoke`.
  - The torso's cross-section is triangulated in Maya; Unity otherwise drops it as a
    self-intersecting polygon.
  - Cap UVs are packed into a small, fleshy red patch of the base colour map.
- **Skeletons.** Each piece gets a copy of only the bones it uses, under their original names.
  The rest are deleted. The copies first have their scale set to exactly 1: the FBX leaves
  0.9999999, which makes Maya slip a compensating transform above the root when it is re-parented.

  | Piece | Bones |
  | --- | --- |
  | `Gib_Head` | Neck, Head, JawLeft/Middle/Right |
  | `Gib_Torso_L` / `_R` | Pelvis, Spine, Spine1, Spine2, Clavicle of its side, Tail, Tail1, Tail2 |
  | `Gib_Arm_L` / `_R` | UpperArm, Forearm, Hand, Finger0–2 |
  | `Gib_Leg_L` / `_R` | Thigh, Calf, Foot, Toe0–2 |

- **Weights.** For each vertex, the source weights are interpolated at the closest point on the
  source mesh. A bone the piece doesn't have hands its weight to its nearest ancestor that the
  piece has, or to the piece's root. The four strongest are kept.

Maya's command port runs every command it receives twice. `run_in_maya.ps1` tags each run so the
second pass does nothing.

## Unity: prefab

**Tools/Regax/Build Gibs** (`Editor/RegaxGibsBuilder.cs`):

- **Import settings for the FBX:** Generic, no avatar, no clips, no materials.
- **`Regax_Gibs.prefab`.**
  - Bodies on Neck (the head is one body); UpperArm/Forearm/Hand; Thigh/Calf/Foot; and
    Pelvis/Spine2/Tail in each torso half. 19 bodies in all.
  - Each body has a box fitted to the bind-pose vertices it carries, trimmed to 85% because the
    spikes stick out.
  - Mass is 400 kg/m³ of box. Bodies are joined by CharacterJoints (twist ±30°, swing 40°/25°).
  - The layer is Debris: bullets, blasts and the floor hit the pieces, while characters and the
    tank pass through them (`PassThroughLoosePiecesBehaviour`).
  - The material is whatever `Regax.prefab`'s `<Skin>` wears (`M_Rapax 1`).
- **Wiring `Regax.prefab`.** Adds `CreatureGibsInstaller` (gibs, `<Skeleton>`, `<Skin>` renderer)
  to the entity's installer list. **Tools/Regax/Build** rebuilds the creature from the zombie and
  then wires the existing gibs back on.

## Health

`Regax.prefab` health is 60, also set by `RegaxPrefabBuilder` (`HEALTH`). The tank cannon deals
150 at the centre of its 5 m blast, falling to 25% (37) at the edge, so a shell kills anything
within about 4 m of where it lands. A rifle (10 per bullet) needs six hits instead of ten.

## Runtime

`GibsOnDeathBehaviour` remembers the last `DamageTakenEvent` and acts on `DeathTakenEvent`. The
damage event fires inside `Health.Reduce`, before death, so the killing hit is always known by
then.

1. The creature's skin is hidden, and `Regax_Gibs` is instantiated.
2. `Gibs.Burst` switches the gibs off and copies each bone's world pose from the creature bone of
   the same name. It then switches them back on, so the joints take the death pose as their rest
   rather than the T-pose.
3. The seams between pieces are rolled. A seam that holds is jointed and keeps its two pieces in
   one cluster. The torso halves are welded with a FixedJoint; limbs and the head hang on a
   CharacterJoint (twist ±25°, swing 35°/25°). If every seam holds, random seams are released
   until there are at least two clusters.

   | Seam | Bodies | Holds |
   | --- | --- | --- |
   | Torso L – Torso R | Spine2 – Spine2, welded | 50% |
   | Torso – Arm, each side | Spine2 – UpperArm | 40% |
   | Torso – Leg, each side | Pelvis – Thigh | 35% |
   | Torso – Head, each side | Spine2 – Neck | 20% |

   This gives, among others: the chest with both arms (spine and both arm seams hold), one half
   with its arm and leg, the whole body without its head, or seven loose pieces. Four kills in a
   row in Play came out as four different splits.
4. Each cluster gets one velocity away from the killing hit's source position (the bullet, the
   shell where it went off), plus lift, scatter and a random spin. The speed is mass-independent
   and is scaled per cluster by a random factor of 0.6–1.4. The scatter is up to 40% of the
   speed. Spin is slower the more bodies a cluster has.

   | Killed by | Speed | Lift | Spin (lone piece) |
   | --- | --- | --- | --- |
   | Bullet, melee | 1.5 m/s rising to 4 m/s as the hit's damage approaches 100 | 0.35 | 3 rad/s |
   | Explosion (tank shell, rocket) | 2 m/s at the blast's edge to 4 m/s at its centre | 0.4 | 5 rad/s |

   A source counts as an explosion when it has `ExplosionRadius`. How close the creature stood is
   read back from the hit's `damageFactor`.
5. After 10 s the bodies turn kinematic, their colliders switch off, and the gibs sink 1 m over
   2 s and are destroyed.

## Checked in Play

- **Rifle kill:** the pieces separate and land 1–5 m from the body. Blood plays, no errors.
- **Tank shell 2.9 m away:** Regax (HP 60) dies to one shell. The pieces leave at 14–18 m/s and
  land across the arena.
- **Speeds then cut to a quarter** at the user's request, and clusters added (the values above).
  Four rifle kills: clusters left at 1–2.7 m/s and settled in a heap a metre or two around each
  body, with no errors.

## Known gaps

- Unity still drops one polygon of `Gib_Torso_R_Geo` as self-intersecting on import. If it
  shows, it is a single triangle on the cross-section.
- Not done: tearing limbs off while the creature is alive, pooling the gibs, a separate meat
  material on the caps. `Regax_Mount.prefab` picks the gibs up only when it is rebuilt.
