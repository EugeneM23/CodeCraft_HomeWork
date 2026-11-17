using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AIKit.Mecanim.Editor
{
    /// <summary>
    /// Reads a character's climbing and jumping numbers off its clips (handbook §20.3: the numbers of a move come from
    /// its clip; an editor utility takes them out, as FFXV did), for its body to climb and jump exactly so (step 90,
    /// C20: the climb and the jump are the animation's; the fall is gravity's). The clips are the states of the AI
    /// Animator's controller (its Climb Up, Climb Down, Jump Start, Jump Land), played on a copy of the character's own
    /// model with its avatar, stepped by hand: how long the climb moves the body (the standing tail left out), how high
    /// it climbs and how far forward it goes, the part done at each moment (curves), how far from the wall it starts
    /// (nothing of the body below the top ever inside the wall), when the feet leave the ground in the jump's start and
    /// come down in its landing. Moved here from the lab's ClimbSetup (it measured the swordsman alone).
    /// </summary>
    public static class TraversalMeasure
    {
        /// <summary>Samples of a clip read for the curves.</summary>
        public const int SAMPLES = 96;

        /// <summary>Metres within which the body is taken to stand still at the end of the climb.</summary>
        private const float SETTLED = 0.01f;

        /// <summary>Metres of flesh and armour in front of a bone.</summary>
        private const float SKIN = 0.1f;

        /// <summary>Metres a foot rises off the ground (or comes down to it) by which the take-off (touch) is told.</summary>
        private const float FOOT_OFF = 0.05f;

        /// <summary>The parts of the body that stay off the wall's face while they are below its top (not the arms).</summary>
        private static readonly HumanBodyBones[] Body =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
            HumanBodyBones.Head, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes,
            HumanBodyBones.RightToes
        };

        /// <summary>
        /// Measures the clips of this AI Animator's controller on its own model — the clips it plays (a character
        /// variant's own, under an override controller). False — no controller (or no clip to measure at all); report
        /// says what was read and what was not. Fresh — measured now, never taken from what this editor session measured
        /// before (what the user asks for: a re-import that keeps the clips' names and lengths changes nothing else).
        /// </summary>
        public static bool Measure(AIAnimator animator, out MeasuredTraversal traversal, List<string> report, bool fresh = false)
        {
            traversal = new MeasuredTraversal();
            Animator unity = animator != null ? animator.Animator : null;
            AnimatorController controller = ControllerOf(unity);
            if (controller == null)
            {
                report?.Add("no animator controller to measure");
                return false;
            }

            // The same clips on the same rig at the same size measure the same: a scene of many climbers is measured once
            string key = Fingerprint(animator) + "|" + RootMotionOf(unity, controller, animator.States) + "|" + SizeOf(unity);
            if (!fresh && Measured.TryGetValue(key, out MeasuredTraversal known))
            {
                traversal = known.Clone();
                report?.Add($"as measured before from {known.MeasuredFrom}");
                return traversal.IsMeasured;
            }

            traversal.MeasuredFrom = controller.name;
            traversal.Fingerprint = Fingerprint(animator);
            MeasureClips(animator, unity, controller, traversal, report);
            if (traversal.IsMeasured)
                Measured[key] = traversal.Clone();
            else
                Measured.Remove(key);
            return traversal.IsMeasured;
        }

        // What was measured in this editor session, by the clips and the rig it came from
        private static readonly Dictionary<string, MeasuredTraversal> Measured = new();

        // How the measured clips move their root (an import change of the root motion changes it, the clips' names not)
        private static string RootMotionOf(Animator unity, AnimatorController controller, AnimatorStates states)
        {
            string Of(string state)
            {
                AnimationClip clip = ClipOf(unity, controller, state);
                return clip != null ? Invariant(clip.averageSpeed) : "-";
            }

            return $"{Of(states.ClimbUp)}|{Of(states.JumpStart)}|{Of(states.JumpLand)}";
        }

        // The model's size: the copy measured keeps it, and the bones of a bigger model are further from the wall
        private static string SizeOf(Animator unity)
        {
            Vector3 scale = unity != null ? unity.transform.localScale : Vector3.one;
            return Invariant(scale);
        }

        private static string Invariant(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", v.x, v.y, v.z);

        private static void MeasureClips(AIAnimator animator, Animator unity, AnimatorController controller,
            MeasuredTraversal traversal, List<string> report)
        {
            AnimationClip up = ClipOf(unity, controller, animator.States.ClimbUp);
            traversal.HasClimbDown = ClipOf(unity, controller, animator.States.ClimbDown) != null;
            if (up != null)
            {
                if (MeasureClimb(unity.gameObject, unity.avatar, up, out ClimbMotion climb, report))
                {
                    traversal.HasClimb = true;
                    traversal.Climb = climb;
                }
            }
            else
            {
                report?.Add($"no climbing clip (state '{animator.States.ClimbUp}')");
            }

            AnimationClip start = ClipOf(unity, controller, animator.States.JumpStart);
            AnimationClip land = ClipOf(unity, controller, animator.States.JumpLand);
            if (start != null && land != null)
            {
                if (MeasureJump(unity.gameObject, unity.avatar, start, land, out JumpMotion jump, report))
                {
                    traversal.HasJump = true;
                    traversal.Jump = jump;
                }
            }
            else
            {
                report?.Add($"no jumping clips (states '{animator.States.JumpStart}', '{animator.States.JumpLand}')");
            }
        }

        /// <summary>
        /// What the measured numbers depend on, in one line: the controller and the clips of the measured states (the
        /// ones it plays: a character variant's own) with their lengths — written the same in every culture. A different
        /// line — the clips changed since: measure again.
        /// </summary>
        public static string Fingerprint(AIAnimator animator) => Fingerprint(animator, CultureInfo.InvariantCulture);

        // The fingerprint as a culture writes it (the lines kept before step 90's recheck were written in the editor's own)
        private static string Fingerprint(AIAnimator animator, CultureInfo culture)
        {
            Animator unity = animator != null ? animator.Animator : null;
            AnimatorController controller = ControllerOf(unity);
            if (controller == null)
                return "";

            AnimatorStates states = animator.States;
            string Of(string state)
            {
                AnimationClip clip = ClipOf(unity, controller, state);
                return clip != null ? clip.name + ":" + clip.length.ToString("0.000", culture) : "-";
            }

            return $"{controller.name}|{Of(states.ClimbUp)}|{Of(states.ClimbDown)}|{Of(states.JumpStart)}|{Of(states.JumpLand)}" +
                   $"|{(unity.avatar != null ? unity.avatar.name : "-")}";
        }

        /// <summary>The controller (under its override, if it has one) of an Animator; null — none.</summary>
        public static AnimatorController ControllerOf(Animator animator)
        {
            RuntimeAnimatorController controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller is AnimatorOverrideController given)
                controller = given.runtimeAnimatorController;
            return controller as AnimatorController;
        }

        /// <summary>
        /// The clip the Animator plays for the state of this name: the controller's, or what the override controller on the
        /// Animator (a character variant) puts in its place.
        /// </summary>
        public static AnimationClip ClipOf(Animator animator, AnimatorController controller, string state)
        {
            AnimationClip clip = ClipOf(controller, state);
            if (clip != null && animator != null && animator.runtimeAnimatorController is AnimatorOverrideController variant)
            {
                AnimationClip played = variant[clip];
                if (played != null)
                    clip = played;
            }

            return clip;
        }

        /// <summary>The clip of the state of this name (any layer, any sub-machine; of a blend tree — its first clip).</summary>
        public static AnimationClip ClipOf(AnimatorController controller, string state)
        {
            if (controller == null || string.IsNullOrEmpty(state))
                return null;

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                AnimatorState found = Find(layer.stateMachine, state);
                if (found != null)
                    return ClipIn(found.motion);
            }

            return null;
        }

        private static AnimatorState Find(AnimatorStateMachine machine, string state)
        {
            if (machine == null)
                return null;

            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null && child.state.name == state)
                    return child.state;
            }

            foreach (ChildAnimatorStateMachine child in machine.stateMachines)
            {
                AnimatorState found = Find(child.stateMachine, state);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static AnimationClip ClipIn(Motion motion)
        {
            if (motion is AnimationClip clip)
                return clip;
            if (motion is BlendTree tree)
            {
                foreach (ChildMotion child in tree.children)
                {
                    AnimationClip found = ClipIn(child.motion);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        /// <summary>
        /// The climbing clip played on the model with its root moving (an Animator with root motion, stepped by hand):
        /// where the root is at each moment, over the ground forward and up. The rise and the way forward become curves
        /// of the part done (0…1 at each 0…1 of the time).
        /// </summary>
        public static bool MeasureClimb(GameObject model, Avatar avatar, AnimationClip clip, out ClimbMotion climb,
            List<string> report)
        {
            climb = default;
            if (model == null || clip == null)
                return false;

            var heights = new float[SAMPLES + 1];
            var ways = new float[SAMPLES + 1];
            var parts = new List<(Vector3 At, HumanBodyBones Bone, int Sample)>();
            if (!Play(model, avatar, clip, true, animator =>
                {
                    for (int i = 0; i <= SAMPLES; i++)
                    {
                        if (i > 0)
                            animator.Update(clip.length / SAMPLES);
                        heights[i] = animator.transform.position.y;
                        ways[i] = animator.transform.position.z;
                        if (!animator.isHuman)
                            continue;

                        foreach (HumanBodyBones bone in Body)
                        {
                            Transform part = animator.GetBoneTransform(bone);
                            if (part != null)
                                parts.Add((part.position, bone, i));
                        }
                    }
                }))
                return false;

            // The climb ends where the body stops: the clip may stand on the top a while after it, and a climb timed by
            // the whole clip held the body there, waiting, before it walked on
            int settled = SAMPLES;
            while (settled > 1 && Mathf.Abs(heights[settled - 1] - heights[SAMPLES]) < SETTLED &&
                   Mathf.Abs(ways[settled - 1] - ways[SAMPLES]) < SETTLED)
                settled--;
            System.Array.Resize(ref heights, settled + 1);
            System.Array.Resize(ref ways, settled + 1);
            float time = clip.length * settled / SAMPLES;
            float height = heights[settled] - heights[0];
            float reach = ways[settled] - ways[0];
            if (height < 0.5f || reach < 0.05f)
            {
                report?.Add($"the climbing clip '{clip.name}' climbs {height:0.00} m and goes {reach:0.00} m forward — " +
                            "not a climb onto a wall (is its root motion imported?)");
                return false;
            }

            // The wall's face: nothing of the body below the top is ever inside it — the furthest forward any part below
            // the edge comes, and the flesh around its bone (the hands reach over the edge, so they are not asked)
            float edge = heights[0] + height;
            var below = parts.Where(part => part.At.y < edge - 0.05f).OrderByDescending(part => part.At.z).ToArray();
            float fromWall = below.Length > 0 ? below[0].At.z + SKIN - ways[0] : 0f;
            climb = new ClimbMotion
            {
                Time = time,
                Height = height,
                Reach = reach,
                FromWall = fromWall,
                Rise = Curve(heights, height),
                Advance = Curve(ways, reach)
            };
            report?.Add($"climb ({clip.name}): {time:0.00} s of {clip.length:0.00}, {height:0.00} m up, {reach:0.00} m " +
                        $"forward, starts {fromWall:0.00} m off the wall");
            return true;
        }

        /// <summary>The jump: when the feet leave the ground in its start, when they come down in its landing.</summary>
        public static bool MeasureJump(GameObject model, Avatar avatar, AnimationClip start, AnimationClip land,
            out JumpMotion jump, List<string> report)
        {
            jump = default;
            float[] up = LowestFoot(model, avatar, start);
            float[] down = LowestFoot(model, avatar, land);
            if (up == null || down == null)
            {
                report?.Add("the jumping clips cannot be read on this model (no feet: not a humanoid?)");
                return false;
            }

            int off = System.Array.FindIndex(up, foot => foot > up[0] + FOOT_OFF);
            int touch = System.Array.FindIndex(down, foot => foot < down[^1] + FOOT_OFF);
            if (off <= 0 || touch < 0)
            {
                report?.Add($"the jump's feet never leave the ground in '{start.name}' or never touch it in '{land.name}'");
                return false;
            }

            jump = new JumpMotion
            {
                TakeOff = start.length * off / SAMPLES,
                Touch = land.length * touch / SAMPLES,
                Landing = land.length - land.length * touch / SAMPLES
            };
            report?.Add($"jump ({start.name}, {land.name}): takes off {jump.TakeOff:0.00} s in, the feet touch " +
                        $"{jump.Touch:0.00} s into the landing, {jump.Landing:0.00} s more to stand");
            return true;
        }

        // The height of the lower foot at each sample of the clip, played on the model in place; null — no feet
        private static float[] LowestFoot(GameObject model, Avatar avatar, AnimationClip clip)
        {
            float[] feet = null;
            Play(model, avatar, clip, false, animator =>
            {
                Transform left = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                Transform right = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightFoot) : null;
                if (left == null || right == null)
                    return;

                feet = new float[SAMPLES + 1];
                for (int i = 0; i <= SAMPLES; i++)
                {
                    // The last sample a hair short of the end: a looped clip wraps to its start at its length
                    if (i > 0)
                        animator.Update(clip.length / SAMPLES * (i == SAMPLES ? 0.98f : 1f));
                    feet[i] = Mathf.Min(left.position.y, right.position.y);
                }
            });
            return feet;
        }

        // A hidden copy of the model, with a controller of this clip alone, at the origin facing +Z: read, then gone. Its
        // scripts go first — one taking the root's motion would keep the animator from moving the copy
        private static bool Play(GameObject model, Avatar avatar, AnimationClip clip, bool rootMotion,
            System.Action<Animator> read)
        {
            var copy = Object.Instantiate(model);
            copy.hideFlags = HideFlags.HideAndDontSave;
            var controller = new AnimatorController();
            try
            {
                foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                    Object.DestroyImmediate(script);

                controller.AddLayer("Base");
                controller.layers[0].stateMachine.AddState(clip.name).motion = clip;
                if (!copy.TryGetComponent(out Animator animator))
                    animator = copy.AddComponent<Animator>();
                if (avatar != null)
                    animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = rootMotion;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                animator.Rebind();
                animator.Update(0f);
                read(animator);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(controller);
            }
        }

        // The part of the whole done at each sample, as a curve over 0…1 of the time
        private static AnimationCurve Curve(float[] values, float whole)
        {
            var keys = new Keyframe[values.Length];
            for (int i = 0; i < values.Length; i++)
                keys[i] = new Keyframe((float)i / (values.Length - 1), (values[i] - values[0]) / whole);
            keys[0].value = 0f;
            keys[^1].value = 1f;
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            for (int i = 0; i < keys.Length; i++)
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            return curve;
        }

        // ---------- into the body ----------

        /// <summary>
        /// Measures the clips of the character's AI Animator into its body (with Undo). What was done, in words; null —
        /// nothing to measure with (no body that traverses, no AI Animator, no controller). Fresh — measured now (the
        /// user's Measure again), not taken from this session's earlier measures.
        /// </summary>
        public static string MeasureInto(AICharacter character, bool fresh = false)
        {
            if (character == null || character.Body == null || !character.Body.Traverses)
                return null;

            AIAnimator animator = character.GetComponentInChildren<AIAnimator>(true);
            if (animator == null)
                return null;

            var report = new List<string>();
            if (!Measure(animator, out MeasuredTraversal traversal, report, fresh))
                return "nothing measured: " + string.Join("; ", report);

            Undo.RecordObject(character, "Measure Climbing And Jumping");
            character.Body.Traversal = traversal;
            EditorUtility.SetDirty(character);
            if (PrefabUtility.IsPartOfPrefabInstance(character))
                PrefabUtility.RecordPrefabInstancePropertyModifications(character);
            return "measured: " + string.Join("; ", report);
        }

        /// <summary>
        /// The body's numbers were measured from other clips than the AI Animator's now. A line written in another
        /// culture (a comma for the decimal point) is the same line.
        /// </summary>
        public static bool IsStale(AICharacter character)
        {
            MeasuredTraversal traversal = character != null && character.Body != null ? character.Body.Traversal : null;
            if (traversal == null || !traversal.IsMeasured || string.IsNullOrEmpty(traversal.Fingerprint))
                return false;

            AIAnimator animator = character.GetComponentInChildren<AIAnimator>(true);
            return animator != null && Plain(Fingerprint(animator)) != Plain(traversal.Fingerprint);
        }

        // A fingerprint with its decimal commas written as points (only the clips' lengths have decimals)
        private static string Plain(string fingerprint) => fingerprint?.Replace(',', '.');

        /// <summary>
        /// The AI Animator's controller has a state the body's climb or jump is measured from (Climb Up; Jump Start and
        /// Jump Land) — something to measure. None — the plain way over edges is all there is: measuring changes nothing.
        /// </summary>
        public static bool HasClipsToMeasure(AIAnimator animator)
        {
            Animator unity = animator != null ? animator.Animator : null;
            AnimatorController controller = ControllerOf(unity);
            if (controller == null)
                return false;

            AnimatorStates states = animator.States;
            return ClipOf(unity, controller, states.ClimbUp) != null ||
                   ClipOf(unity, controller, states.JumpStart) != null && ClipOf(unity, controller, states.JumpLand) != null;
        }
    }
}
