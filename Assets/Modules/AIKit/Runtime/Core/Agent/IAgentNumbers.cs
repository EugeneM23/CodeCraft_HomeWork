using System.Collections.Generic;

namespace AIKit
{
    /// <summary>
    /// A part of a character that keeps numbers about the character: the body (how fast it runs and turns, how far it
    /// takes to get up to speed), an ability (its damage, its rest), a game's component (its morale, its stamina). The
    /// part is their one owner (invariant 10): nothing copies them anywhere (step 90 — the blackboard mirror of step 34
    /// is gone; handbook §8.2: no copies of settings on the blackboard). The character's status reads them where they
    /// live (AICharacter: Run Speed, Turn Speed, Acceleration under the names of BodyNumbers; any other by its name — the
    /// Get node's custom number), and the Set Parameter block changes the body's for the length of a decision, through
    /// SetNumber — so a behavior still reads and changes them, as in "enraged: runs faster", visibly.
    /// </summary>
    public interface IAgentNumbers
    {
        /// <summary>The names of the numbers ("Speed", "Turn Speed", "Sword Damage").</summary>
        void NamesOf(List<string> names);

        float GetNumber(int index);

        void SetNumber(int index, float value);
    }
}
