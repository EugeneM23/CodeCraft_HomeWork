namespace AIKit
{
    /// <summary>
    /// A navigator that can walk its path slower than at full speed: a node that takes the agent somewhere with no
    /// hurry (to work, home) sets the pace and puts 1 back when it is over. A navigator without it always runs.
    /// </summary>
    public interface INavigationPace
    {
        /// <summary>A part of the body's full speed, 0…1 (1 — full speed).</summary>
        float Pace { get; set; }
    }
}
