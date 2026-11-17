using UnityEngine;

namespace Saving.Demo
{
    /// <summary>Spins its object: Save, wait, Load — the rotation snaps back, because its Saveable keeps the Transform.</summary>
    public sealed class DemoSpinner : MonoBehaviour
    {
        [SerializeField] private Vector3 _degreesPerSecond = new(20f, 45f, 0f);

        private void Update() => transform.Rotate(_degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
