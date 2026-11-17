using UnityEngine;

namespace AIKit
{
    /// <summary>
    /// What the character holds in its hands — a box of ammo to carry: one thing at a time, held in front of the chest. A
    /// fact of the character, like the weapon in its hand: the status says it (Carrying), the scenarios of the objects
    /// put things in and take them out (a depot hands a box, a turret takes it), Drop puts it down. Hands that hold
    /// something do not strike (the Attack block fails at once: put the load down first).
    /// </summary>
    public interface IHands
    {
        /// <summary>The thing held now; null — the hands are empty.</summary>
        Transform Item { get; }

        /// <summary>Takes the thing into the hands (in front of the chest); false — the hands are full already.</summary>
        bool Hold(Transform item);

        /// <summary>
        /// Lets go of the thing held — it stays where it is, out of the character: the caller puts it down, throws it or
        /// uses it up. Null — nothing was held.
        /// </summary>
        Transform LetGo();
    }

    /// <summary>What the blocks do with a thing that leaves the hands: put it on the ground, use it up.</summary>
    public static class Items
    {
        /// <summary>The thing lies at this point from now on, free to be picked up again (a rock after a throw).</summary>
        public static void PutDown(Transform item, Vector3 at)
        {
            if (item == null)
                return;

            item.SetParent(null, true);
            item.position = at;
            item.rotation = Quaternion.Euler(0f, item.eulerAngles.y, 0f);
            if (item.TryGetComponent(out PickableItem pickable))
                pickable.Release();
        }

        /// <summary>The thing is used up (a box of ammo loaded into a turret): it goes from the world.</summary>
        public static void UseUp(Transform item)
        {
            if (item == null)
                return;

            item.SetParent(null, true);
            if (Application.isPlaying)
                Object.Destroy(item.gameObject);
            else
                Object.DestroyImmediate(item.gameObject);
        }
    }
}
