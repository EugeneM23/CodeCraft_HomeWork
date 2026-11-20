using UnityEngine;

namespace Game.AIKit.Adapters
{
    /// <summary>
    /// A debug counter of the live sandbox horde (the user): «Зомби: N» in the top right corner while the sandbox horde
    /// is up. The text is rebuilt only when the count changes.
    /// </summary>
    public sealed class SandboxHordeCounter : MonoBehaviour
    {
        private const float WIDTH = 220f;
        private const float HEIGHT = 34f;
        private const float MARGIN = 12f;

        private int _shown = -1;
        private string _text = string.Empty;
        private GUIStyle _style;

        private void OnGUI()
        {
            SandboxHordeBehaviour horde = SandboxHorde.Active;
            if (horde == null)
                return;

            int alive = horde.Alive;
            if (alive != _shown)
            {
                _shown = alive;
                _text = "Зомби: " + alive;
            }

            _style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            GUI.Box(new Rect(Screen.width - WIDTH - MARGIN, MARGIN + 60f, WIDTH, HEIGHT), _text, _style);
        }
    }
}
