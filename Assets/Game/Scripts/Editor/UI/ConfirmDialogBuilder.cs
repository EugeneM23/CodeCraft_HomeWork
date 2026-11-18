using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>Builds the Yes/No dialog (the pack's window, the question's title on its plate) inside a canvas; MenuUiBuilder and BattleUiBuilder call it.</summary>
    public static class ConfirmDialogBuilder
    {
        public static ConfirmDialog Create(Transform parent)
        {
            ConfirmDialog dialog = UiFactory.Screen<ConfirmDialog>("ConfirmDialog", parent, overlay: true, dim: true);

            Image box = UiFactory.Window("Box", dialog.transform);
            UiFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680, 340));
            UiFactory.WindowLayout(box.rectTransform, UiStyle.SPACING, true);

            // Title, text and both labels are set by ConfirmDialog.Setup on every question: no UiLocalizedText.
            TextMeshProUGUI title = UiFactory.WindowTitle(box, "-");
            TextMeshProUGUI text = UiFactory.Text("Text", box.transform, "-", UiStyle.FONT_BODY,
                TextAlignmentOptions.Center, color: UiStyle.TextMuted);
            UiFactory.Size(text, 120, -1, -1, 1);

            RectTransform row = UiFactory.Rect("Buttons", box.transform);
            UiFactory.Horizontal(row, UiStyle.SPACING * 2, 0, TextAnchor.MiddleCenter);
            UiFactory.Size(row, UiStyle.BUTTON_HEIGHT);
            Button no = UiFactory.Button("No", row, UiStrings.Get("common.no"));
            UiFactory.Size(no, UiStyle.BUTTON_HEIGHT, 220);
            no.GetComponent<UiButtonSounds>().Click = UiSound.Back;
            Button yes = UiFactory.Button("Yes", row, UiStrings.Get("common.yes"), accent: true);
            UiFactory.Size(yes, UiStyle.BUTTON_HEIGHT, 220);

            UiFactory.SetField(dialog, "_title", title);
            UiFactory.SetField(dialog, "_text", text);
            UiFactory.SetField(dialog, "_yes", yes);
            UiFactory.SetField(dialog, "_no", no);
            UiFactory.SetField(dialog, "_yesLabel", yes.GetComponentInChildren<TMP_Text>());
            UiFactory.SetField(dialog, "_noLabel", no.GetComponentInChildren<TMP_Text>());
            UiFactory.SetField(dialog, "_firstSelected", no);
            return dialog;
        }
    }
}
