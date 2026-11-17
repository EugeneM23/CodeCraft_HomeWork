using System.Collections.Generic;
using UnityEngine;

namespace AIKit.Tests.SensesCoverage
{
    /// <summary>
    /// The scene of the senses' coverage tests, stood in for: objects of a kind from a list (radius-checked, by tag when
    /// asked, every call counted per kind), walls that block the line to chosen objects, a ground plane no line may dip
    /// under, and hidden objects destroyed after each test.
    /// </summary>
    internal sealed class KindFinder : IObjectFinder
    {
        public readonly List<Transform> Objects = new();

        /// <summary>On — only objects whose tag is the kind asked for are listed (off — every object, as the sensors' tests do).</summary>
        public bool ByTag;

        /// <summary>FindAll calls per kind since the last Clear of the counts.</summary>
        public readonly Dictionary<string, int> Calls = new();

        public KindFinder(params Transform[] objects) => Objects.AddRange(objects);

        public Transform FindNearest(string kind, Vector3 center, float radius, Transform self)
        {
            Transform nearest = null;
            foreach (Transform found in Objects)
            {
                if (!Lists(found, kind, center, radius, self))
                    continue;

                if (nearest == null || (found.position - center).sqrMagnitude < (nearest.position - center).sqrMagnitude)
                    nearest = found;
            }

            return nearest;
        }

        public void FindAll(string kind, Vector3 center, float radius, Transform self, List<Transform> results)
        {
            Calls[kind ?? ""] = Calls.TryGetValue(kind ?? "", out int calls) ? calls + 1 : 1;
            results.Clear();
            foreach (Transform found in Objects)
            {
                if (Lists(found, kind, center, radius, self))
                    results.Add(found);
            }
        }

        private bool Lists(Transform found, string kind, Vector3 center, float radius, Transform self)
        {
            if (found == null || found == self || !found.gameObject.activeInHierarchy)
                return false;

            if (ByTag && !found.CompareTag(kind))
                return false;

            return radius >= float.MaxValue * 0.5f || (found.position - center).sqrMagnitude <= radius * radius;
        }
    }

    /// <summary>Walls: a line to one of these objects is blocked (the height of the line does not matter).</summary>
    internal sealed class WallProbe : ISightProbe
    {
        public readonly List<Transform> Blocked = new();

        public int Asked { get; private set; }

        public bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Asked++;
            foreach (Transform blocked in Blocked)
            {
                Vector3 offset = blocked.position - to;
                offset.y = 0f;
                if (offset.sqrMagnitude < 1e-4f)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Every line clear, or every line blocked: the test throws the switch.</summary>
    internal sealed class SwitchSight : ISightProbe
    {
        public bool Clear = true;

        public bool HasLineOfSight(Vector3 from, Vector3 to) => Clear;
    }

    /// <summary>An entity that says it can be perceived, of a faction (0 — none), aimed at where it stands.</summary>
    internal sealed class FactionMark : MonoBehaviour, IPerceivable
    {
        public int Side;

        public bool IsPerceivable => true;

        public int Faction => Side;

        public Vector3 AimPoint => transform.position;
    }

    /// <summary>The ground at y = 0: a line with an end under it ends in the ground (a ray from the eyes to the feet).</summary>
    internal sealed class GroundPlane : ISightProbe
    {
        public bool HasLineOfSight(Vector3 from, Vector3 to) => Mathf.Min(from.y, to.y) >= 0f;
    }

    /// <summary>A condition with a fixed answer — yes, no or no data — that counts how often it was really asked.</summary>
    internal sealed class AnswerCondition : AgentCondition
    {
        public Truth Answers;

        public int Asked { get; private set; }

        public AnswerCondition(Truth answers) => Answers = answers;

        protected override bool Evaluate(AgentContext context) => Answer(context) == Truth.Yes;

        protected override Truth Answer(AgentContext context)
        {
            Asked++;
            return Answers;
        }
    }

    /// <summary>Hidden objects of a test, gone after it.</summary>
    internal sealed class Things
    {
        private readonly List<Object> _made = new();

        public Transform Make(string name, Vector3 at, string tag = null)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            if (tag != null)
                go.tag = tag;
            go.transform.position = at;
            _made.Add(go);
            return go.transform;
        }

        public T Keep<T>(T made) where T : Object
        {
            _made.Add(made);
            return made;
        }

        public void DestroyAll()
        {
            foreach (Object made in _made)
            {
                if (made != null)
                    Object.DestroyImmediate(made);
            }

            _made.Clear();
        }
    }
}
