#if SAVE_ANIMATION
using System.Collections.Generic;
using UnityEngine;

namespace Saving.Objects
{
    /// <summary>
    /// Built in: speed; per layer the state, the time in it and the weight; float, int and bool parameters by name
    /// (triggers cannot be read). A transition in the middle of a blend is not kept. Without a controller, or on a
    /// switched-off object (not initialized), only the speed is saved.
    /// </summary>
    public sealed class AnimatorSerializer : ComponentSerializer<Animator, AnimatorSerializer.Data>
    {
        public sealed class Layer
        {
            public int State;
            public float Time;
            public float Weight = 1f;
        }

        public sealed class Data
        {
            public float Speed = 1f;
            public List<Layer> Layers = new();
            public Dictionary<string, float> Floats = new();
            public Dictionary<string, int> Ints = new();
            public Dictionary<string, bool> Bools = new();
        }

        protected override Data Serialize(Animator animator)
        {
            var data = new Data { Speed = animator.speed };
            if (animator.runtimeAnimatorController == null || !animator.isInitialized)
                return data;

            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                data.Layers.Add(new Layer
                {
                    State = state.fullPathHash,
                    Time = state.normalizedTime,
                    Weight = layer == 0 ? 1f : animator.GetLayerWeight(layer)
                });
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        data.Floats[parameter.name] = animator.GetFloat(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Int:
                        data.Ints[parameter.name] = animator.GetInteger(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        data.Bools[parameter.name] = animator.GetBool(parameter.nameHash);
                        break;
                }
            }

            return data;
        }

        protected override void Deserialize(Animator animator, Data data)
        {
            animator.speed = data.Speed;
            if (animator.runtimeAnimatorController == null || !animator.isInitialized)
                return;

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float when data.Floats.TryGetValue(parameter.name, out float value):
                        animator.SetFloat(parameter.nameHash, value);
                        break;
                    case AnimatorControllerParameterType.Int when data.Ints.TryGetValue(parameter.name, out int value):
                        animator.SetInteger(parameter.nameHash, value);
                        break;
                    case AnimatorControllerParameterType.Bool when data.Bools.TryGetValue(parameter.name, out bool value):
                        animator.SetBool(parameter.nameHash, value);
                        break;
                }
            }

            for (int layer = 0; layer < Mathf.Min(data.Layers.Count, animator.layerCount); layer++)
            {
                if (layer > 0)
                    animator.SetLayerWeight(layer, data.Layers[layer].Weight);

                animator.Play(data.Layers[layer].State, layer, data.Layers[layer].Time);
            }

            // Play takes effect on the next update: apply it now, so the state is right before anything reads it
            animator.Update(0f);
        }
    }
}
#endif
