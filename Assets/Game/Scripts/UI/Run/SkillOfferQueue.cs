using System.Collections.Generic;

namespace Game
{
    /// <summary>
    /// The battle UI's slot for a skill offer that cannot be shown yet: the pause or the current skill choice is
    /// on top (the next level-up is offered from inside ChooseSkill, before the current choice has left).
    /// The sources reuse one list for their current offer, so a later offer replaces the waiting one instead of
    /// queueing behind it, and on taking it the source's pending offer has the last word: nothing is shown when
    /// the source no longer has one. No allocations.
    /// </summary>
    public sealed class SkillOfferQueue
    {
        private IReadOnlyList<SkillOffer> _waiting;

        public bool HasWaiting => _waiting != null;

        /// <summary>
        /// An offer came. True: show it now (nothing covers the HUD). False: it waits (or was empty).
        /// </summary>
        public bool Arrive(IReadOnlyList<SkillOffer> offers, bool canShowNow)
        {
            if (offers == null || offers.Count == 0)
                return false;

            if (canShowNow)
            {
                _waiting = null;
                return true;
            }

            _waiting = offers;
            return false;
        }

        /// <summary>
        /// Back on the HUD: the offer to show now, if one waited. The source's pending offer wins over the kept
        /// list; when the source has none pending, the kept one is dropped. The slot is empty afterwards.
        /// </summary>
        public bool TryTake(IReadOnlyList<SkillOffer> pending, out IReadOnlyList<SkillOffer> offers)
        {
            bool waited = _waiting != null;
            _waiting = null;
            offers = waited && pending != null && pending.Count > 0 ? pending : null;
            return offers != null;
        }

        public void Clear() => _waiting = null;
    }
}
