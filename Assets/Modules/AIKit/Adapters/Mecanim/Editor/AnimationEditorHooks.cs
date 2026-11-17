using AIKit.Editor;
using UnityEditor;
using UnityEngine;

namespace AIKit.Mecanim.Editor
{
    /// <summary>
    /// What the character's inspector asks of the AI Animator's editor: measure the climbing and jumping clips into the
    /// body, say when they changed since, say what is wrong with how the character is shown.
    /// </summary>
    public sealed class AnimationEditorHook : ICharacterPictureHook
    {
        // The inspector's Measure again: measured now, never what this session measured before (a re-import that keeps the
        // clips' names and lengths is seen too)
        public string MeasureTraversal(AICharacter character) => TraversalMeasure.MeasureInto(character, fresh: true);

        public bool TraversalIsStale(AICharacter character) => TraversalMeasure.IsStale(character);

        public string Problem(AICharacter character)
        {
            if (character == null)
                return null;

            Animator animator = character.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.GetComponent<AIAnimator>() == null &&
                character.GetComponentInChildren<AIAnimator>(true) == null)
            {
                return $"The character has an Animator ({animator.name}) and no AI Animator on it: nothing it does is " +
                       "shown (add one there, or run Tools → AI Kit → Setup Agent).";
            }

            // Never measured, and something to measure: say so. A controller with no climbing or jumping states is not
            // told to measure again — measuring changes nothing, the plain way over edges is a valid one
            AgentBody body = character.Body;
            AIAnimator shown = character.GetComponentInChildren<AIAnimator>(true);
            if (body != null && body.Traverses && body.Traversal is { IsMeasured: false } && shown != null &&
                TraversalMeasure.HasClipsToMeasure(shown))
            {
                return "The body climbs or jumps, and its clips were never measured: Measure again (Body section).";
            }

            return null;
        }
    }

    /// <summary>
    /// Setup Agent's step: a character with an Animator below it gets an AI Animator on the Animator's object (the
    /// object Unity hands the root's motion to).
    /// </summary>
    public sealed class AIAnimatorSetupStep : IAgentSetupStep
    {
        public int Order => 500;

        public void Apply(AgentSetupContext setup)
        {
            Animator animator = setup.Root.GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;

            setup.Ensure<AIAnimator>(animator.transform);
        }
    }
}
