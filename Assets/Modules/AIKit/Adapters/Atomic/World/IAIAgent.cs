using AIKit;

namespace AIKit.Entities
{
    /// <summary>An agent the level steps (its AICharacter is in Manual simulation): the game's side of it and the character.</summary>
    public interface IAIAgent
    {
        AICharacter Character { get; }

        /// <summary>One step: what the game owes the agent first (hits, blasts), then <c>Character.Step(dt)</c>.</summary>
        void Step(float dt);
    }
}
