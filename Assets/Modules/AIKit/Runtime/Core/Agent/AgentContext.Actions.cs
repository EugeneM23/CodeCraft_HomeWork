namespace AIKit
{
    public sealed partial class AgentContext
    {
        /// <summary>
        /// What the character is doing on the spot (step 90, C22): nodes and runners ask for actions here, the presentation
        /// reads it. Always there; without a view nothing is shown.
        /// </summary>
        public ActionState Actions { get; } = new ActionState();
    }
}
