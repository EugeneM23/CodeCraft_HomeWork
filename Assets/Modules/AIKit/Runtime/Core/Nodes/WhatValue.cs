using System;
using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What to search for (step 90, C15): a node of data wired into a Find — the kind of object (a component, or an AI
    /// Object's kind, and/or a tag), what it must offer the character now (Take, Use, Load, Occupy — the object's own say),
    /// whose it may be (mine → public → anyone, handbook §24), how far from the character it may be, and — for a place to
    /// fight from — how far its place must be from the Brain's target (a band: a cover at shooting distance; 0 — the reach
    /// of the weapon in the hand). One owner of these settings (invariant 10): the decision does not repeat them, it only
    /// says why to search.
    /// </summary>
    [Serializable]
    [AIKitNode("AI/Actions/Objects/What", "Value", "aikit.value.what", Simple = true)]
    public sealed class WhatValue : AgentValue
    {
        /// <summary>The kind of object by its component (Pickable Item, Cover, Turret, Perch, AI Object…); empty — any.</summary>
        [TypeName(typeof(WorldObject))] public string Component = "";

        /// <summary>An AI Object's kind ("Home", "Work"); empty — any (given, only AI Objects of that kind).</summary>
        [ObjectKind] public string ObjectKind = "";

        /// <summary>The objects' tag ("Rock"); empty — any.</summary>
        [TagName] public string Tag = "";

        /// <summary>What the object must offer the character now.</summary>
        public Offer Offer = Offer.Take;

        /// <summary>Whose objects may be taken (§24: an author's list never falls through to "something near").</summary>
        public Ownership Whose = Ownership.MineOrPublic;

        /// <summary>Metres from the character the object may be; 0 — anywhere.</summary>
        [Min(0f)] public float Radius = 30f;

        /// <summary>On — the object's place must be at a distance from the Brain's target (Min … Max).</summary>
        public bool FromTarget;

        /// <summary>The place is at least this far from the Brain's target.</summary>
        [Min(0f)] public float MinDistance;

        /// <summary>The place is at most this far from the Brain's target; 0 — the reach of the weapon in the hand.</summary>
        [Min(0f)] public float MaxDistance;

        /// <summary>
        /// Metres the band gives an object the character already holds or stands at (a cover it hides behind is not left
        /// for a target that stepped a little — handbook §18а.2 p.4).
        /// </summary>
        [Min(0f)] public float Slack = 2f;

        [NonSerialized] private Type _component;
        [NonSerialized] private string _componentOf;

        public override ValueKind Kind => ValueKind.Filter;

        /// <summary>The component type named by Component; null — any (or a name no loaded type has).</summary>
        public Type ComponentType
        {
            get
            {
                if (_componentOf == Component)
                    return _component;

                _componentOf = Component;
                _component = Find(Component);
                return _component;
            }
        }

        /// <summary>The registry query of the settings (component, kind, tag, the radius around the character).</summary>
        public ObjectQuery QueryFor(AgentContext context) => new()
        {
            Component = ComponentType,
            Kind = ObjectKind,
            Tag = Tag,
            Center = context != null ? context.Position : Vector3.zero,
            Radius = Radius
        };

        /// <summary>
        /// The object is one to take for the character now, apart from whose it is: it offers the Offer to the character,
        /// and its place is in the band from the threat (with Slack for one the character holds already). False with no
        /// threat while the band is on.
        /// </summary>
        public bool Fits(AgentContext context, WorldObject candidate, Transform threat, bool held)
        {
            if (candidate == null || !candidate.Offers(Offer, context))
                return false;

            if (!FromTarget)
                return true;

            if (threat == null)
                return false;

            Vector3 to = threat.position - candidate.PlaceFor(Offer, context, threat);
            to.y = 0f;
            float distance = to.magnitude;
            float max = MaxDistance > 0f ? MaxDistance : WeaponReach(context);
            float slack = held ? Slack : 0f;
            return distance >= MinDistance - slack && distance <= max + slack;
        }

        /// <summary>The band's far end when Max Distance is 0: the reach of the weapon in the hand (0 — none).</summary>
        public static float WeaponReach(AgentContext context)
        {
            WeaponAsset weapon = context?.Weapon;
            return weapon != null ? weapon.ShortestReach : 0f;
        }

        // The type by its full name in any loaded assembly (a game's own components too)
        private static Type Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            Type type = Type.GetType(name);
            if (type != null)
                return type;

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(name);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
