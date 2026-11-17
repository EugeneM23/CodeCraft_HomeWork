using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace AIKit.Editor
{
    /// <summary>One row of the Character section of the blackboard panel: a number or a state of the character.</summary>
    public sealed class CharacterRow
    {
        /// <summary>The row's name in plain words ("Health %", "Is Winding Up").</summary>
        public string Name;

        /// <summary>A state (a yes / no: the Is condition), not a number (the Get node).</summary>
        public bool IsState;

        public CharacterStat Stat;
        public CharacterState State;

        /// <summary>The kind as the panel writes the kind of a variable: its C# type ("bool", "float", "int").</summary>
        public string Kind => IsState ? "bool" : Stat == CharacterStat.UsesLeft ? "int" : "float";

        public override string ToString() => Name;
    }

    /// <summary>
    /// The Character section of the blackboard panel (step 90, C11 / C13; spec 4.9): the numbers and the states of a
    /// character, read where they live — never copies on the blackboard (handbook §8.2; the numbers mirror of step 34 is
    /// gone). Read-only rows: with a character open the value its body and settings give before Play, in Play the live
    /// value of the agent the window follows (its ICharacterStatus), with no character just the names. A row dropped on
    /// a node that reads it is wired in: a number — a Get into a Compare (A, then B), a Set / Add… or a Debug Log; a
    /// state — an Is into a Branch's question or a decision's Condition (joined by an AND to what is there).
    /// </summary>
    public static class CharacterSection
    {
        public const string TITLE = "Character";

        private static List<CharacterRow> _rows;

        /// <summary>Every number of the status, then every state — the module's list, the same for every character.</summary>
        public static IReadOnlyList<CharacterRow> Rows => _rows ??= MakeRows();

        private static List<CharacterRow> MakeRows()
        {
            var rows = new List<CharacterRow>();
            foreach (CharacterStat stat in Enum.GetValues(typeof(CharacterStat)))
                rows.Add(new CharacterRow { Name = NameOf(stat), Stat = stat });
            foreach (CharacterState state in Enum.GetValues(typeof(CharacterState)))
                rows.Add(new CharacterRow { Name = NameOf(state), IsState = true, State = state });
            return rows;
        }

        /// <summary>
        /// A state in plain words, named as a yes / no is (15.11.2026, the user's word): "Is Moving", "Is In Cover" — a state
        /// said by a verb keeps it ("Weapon Shoots", "Holds Picked Weapon").
        /// </summary>
        public static string NameOf(CharacterState state)
        {
            string words = ObjectNames.NicifyVariableName(state.ToString());
            return state is CharacterState.WeaponShoots or CharacterState.HoldsPickedWeapon ? words : "Is " + words;
        }

        /// <summary>A number of the status in plain words (Health %, Run Speed).</summary>
        public static string NameOf(CharacterStat stat) =>
            stat == CharacterStat.HealthPercent ? "Health %" : ObjectNames.NicifyVariableName(stat.ToString());

        /// <summary>
        /// What a row says before Play, from the character's body and settings without waking it up (its body's own
        /// description of its numbers): "—" where nothing tells it.
        /// </summary>
        public static string DesignValue(CharacterRow row, AICharacter character)
        {
            if (row == null || character == null || row.IsState)
                return "—";

            string number = row.Stat switch
            {
                CharacterStat.RunSpeed => BodyNumbers.RUN_SPEED,
                CharacterStat.TurnSpeed => BodyNumbers.TURN_SPEED,
                CharacterStat.Acceleration => BodyNumbers.ACCELERATION,
                _ => null
            };
            if (number == null)
                return "—";

            var numbers = new List<(string Name, float Value)>();
            character.Body?.DescribeNumbers(character.transform, numbers);
            foreach ((string name, float value) in numbers)
            {
                if (name == number)
                    return Text(value);
            }

            return "—";
        }

        /// <summary>What a row says while the game runs: the live value the character's status gives, or "no data".</summary>
        public static string LiveValue(CharacterRow row, ICharacterStatus status)
        {
            if (row == null || status == null)
                return "no data";

            if (row.IsState)
                return status.TryGetState(row.State, out bool on) ? on ? "yes" : "no" : "no data";

            return status.TryGetStat(row.Stat, out float value) ? Text(value) : "no data";
        }

        /// <summary>
        /// The node a row becomes when it is wired into a reader: a Get (a number) or an Is (a state), about Me — the
        /// character the graph runs on; switch Of to Wired Object on the node to ask about another.
        /// </summary>
        public static AgentNode NodeOf(CharacterRow row) => row.IsState
            ? new IsCondition { Of = Whose.Me, State = row.State }
            : new GetValue { Of = Whose.Me, Parameter = (GetParameter)(int)row.Stat };

        /// <summary>
        /// The row is wired into the element that reads it (one of the graph's): a number — a Get into a Compare (A, then
        /// B), a Set / Add… (Number) or a Debug Log (Number); a state — an Is into a Branch's question or a decision's
        /// Condition, joined by an AND to what is there. The same wiring as a wire drawn by hand (GraphDocument): saved
        /// with Undo, what came into a single pin before stays on the canvas unread, a Get in a Compare's A makes it a
        /// number comparison; the new node stands next to the reader (at) and waits for a free place. Null — the element
        /// takes no such row.
        /// </summary>
        public static AgentNode Wire(GraphDocument document, CharacterRow row, object reader, Vector2 at)
        {
            if (document == null || row == null || reader == null || document.IsReadOnly)
                return null;

            if (reader is AgentState state && state.Tasks.Count > 0)
                reader = state.Tasks[0];

            AgentNode node = NodeOf(row);
            if (row.IsState)
                return reader is BranchTask or ReasonerTask ? document.JoinCondition(reader, (AgentCondition)node, at) : null;

            string pin = reader switch
            {
                CompareCondition compare => compare.A == null ? nameof(CompareCondition.A) : nameof(CompareCondition.B),
                NumberTask => nameof(NumberTask.Number),
                DebugLogTask => nameof(DebugLogTask.Number),
                _ => null
            };
            return pin != null ? document.WireNewValue((AgentNode)reader, pin, (AgentValue)node, at) : null;
        }

        private static string Text(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
