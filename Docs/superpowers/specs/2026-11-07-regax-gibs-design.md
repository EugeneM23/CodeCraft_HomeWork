# Regax gibs — the creature comes apart on the killing hit

## Goal

When a Regax dies, its body splits into seven ragdolled pieces that fly away from whatever killed
it, starting from the pose the creature was in.

The seven pieces: the head, the left and right halves of the torso (cut vertically along the
spine, tail included), the left and right arms, and the left and right legs.

## Art: cut in Maya

The cut is made in Maya (through commandPort `:7005`) on `SK_Rapax.fbx`, and the result is
exported to `Assets/Game/Models/Regax/SK_Rapax_Gibs.fbx`. That file holds seven skinned meshes,
each with its own skeleton:

| Piece | Cut | Bones kept |
| --- | --- | --- |
| `Gib_Head` | neck | Neck, Head, Jaw* |
| `Gib_Torso_L` / `_R` | plane X = 0, neck, shoulder, hip | Pelvis, Spine, Spine1, Spine2, Clavicle of its side, Tail, Tail1, Tail2 |
| `Gib_Arm_L` / `_R` | shoulder | UpperArm, Forearm, Hand, Finger0-2 |
| `Gib_Leg_L` / `_R` | hip | Thigh, Calf, Foot, Toe0-2 |

- Bones keep their original names, so a piece can copy the pose from the animated creature bone
  by bone.
- Every bone a piece doesn't use is deleted.
- The holes left by each cut are closed with caps. The caps take their skin weights from the
  nearest bone of the piece.
- Material: `M_Rapax`, the same one the creature uses.

## Unity: prefab

**Tools/Regax/Build Gibs** (`Editor/RegaxGibsBuilder.cs`) builds
`Assets/Game/Prefabs/Characters/Regax_Gibs.prefab` from the FBX:

- A Rigidbody and a capsule collider on every major bone: pelvis/spine, upper arm and forearm,
  thigh and calf, head. Fingers, toes and the jaw are not simulated.
- Neighbouring bodies are joined with CharacterJoints.
- Layer: `DestructiblePieces`. Characters, the tank and enemies pass through the pieces
  (`PassThroughLoosePiecesBehaviour`), while bullets, blasts and the floor still collide with them.

## Runtime

`RegaxGibsBehaviour` is added by the Regax installer (a component on `Regax.prefab`) and listens
for the death event. When it fires:

1. It spawns `Regax_Gibs` at the creature's position.
2. Each piece's bones copy the world pose of the creature bones with the same names.
3. The creature's skin is hidden.
4. Each piece gets an impulse away from the source of the killing hit (a bullet or an explosion)
   plus some lift and a random spin. The strength scales with `damageFactor`. When there is no
   source, the impulse points upward.
5. After 10 s the pieces turn kinematic, sink 1 m into the floor and are destroyed.

Out of scope: tearing limbs off while the creature is alive, pooling the gibs, separate meat
material on the caps.
