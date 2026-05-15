using RimWorld;
using UnityEngine;
using Verse;

namespace RealRuins.Settings
{
    public class Dialog_ImportExportSettings : Window
    {
        private readonly bool isExport;
        private string textContent;
        private Vector2 scrollPos = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(560f, 320f);

        public Dialog_ImportExportSettings(bool export)
        {
            isExport = export;
            doCloseX = true;
            forcePause = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            textContent = export ? SettingsSerializer.Export() : "";
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Title
            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Medium;
            string title = isExport
                ? "RealRuins.Settings.ExportTitle".Translate()
                : "RealRuins.Settings.ImportTitle".Translate();
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 32f);
            Widgets.Label(titleRect, title);
            Text.Font = prevFont;

            // Text area
            float buttonAreaHeight = 38f;
            float textAreaTop = inRect.y + 36f;
            float textAreaHeight = inRect.height - 36f - buttonAreaHeight - 8f;
            Rect textAreaRect = new Rect(inRect.x, textAreaTop, inRect.width, textAreaHeight);

            if (isExport)
            {
                // Read-only display with scroll
                Rect innerText = new Rect(0, 0, textAreaRect.width - 20f, Text.CalcHeight(textContent, textAreaRect.width - 24f) + 10f);
                float viewHeight = Mathf.Max(innerText.height, textAreaRect.height);
                innerText.height = viewHeight;
                Widgets.BeginScrollView(textAreaRect, ref scrollPos, innerText);
                GUI.SetNextControlName("ExportTextField");
                textContent = GUI.TextArea(new Rect(0, 0, innerText.width, innerText.height), textContent);
                Widgets.EndScrollView();
            }
            else
            {
                // Editable input
                Rect innerText = new Rect(0, 0, textAreaRect.width - 20f, Mathf.Max(200f, Text.CalcHeight(textContent, textAreaRect.width - 24f) + 10f));
                Widgets.BeginScrollView(textAreaRect, ref scrollPos, innerText);
                textContent = GUI.TextArea(new Rect(0, 0, innerText.width, innerText.height), textContent);
                Widgets.EndScrollView();
            }

            // Buttons
            float buttonY = inRect.yMax - buttonAreaHeight;
            float buttonWidth = 140f;
            float buttonHeight = 32f;
            float spacing = 10f;

            if (isExport)
            {
                // Copy button
                Rect copyRect = new Rect(inRect.x, buttonY, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(copyRect, "RealRuins.Settings.CopyToClipboard".Translate()))
                {
                    GUIUtility.systemCopyBuffer = textContent;
                    Messages.Message("RealRuins.Settings.CopiedToClipboard".Translate(), MessageTypeDefOf.NeutralEvent);
                }

                // Close button
                Rect closeRect = new Rect(inRect.x + buttonWidth + spacing, buttonY, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
                {
                    Close();
                }
            }
            else
            {
                // Import button
                Rect importRect = new Rect(inRect.x, buttonY, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(importRect, "RealRuins.Settings.ImportApply".Translate()))
                {
                    if (SettingsSerializer.TryImport(textContent))
                    {
                        Messages.Message("RealRuins.Settings.ImportSuccess".Translate(), MessageTypeDefOf.NeutralEvent);
                        Close();
                    }
                    else
                    {
                        Messages.Message("RealRuins.Settings.ImportFailed".Translate(), MessageTypeDefOf.RejectInput);
                    }
                }

                // Close button
                Rect closeRect = new Rect(inRect.x + buttonWidth + spacing, buttonY, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
                {
                    Close();
                }
            }
        }
    }
}
