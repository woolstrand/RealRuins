using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRuins.WhatsNew
{
    /// <summary>
    /// Shows what's new articles in a scrollable list. They count as read once the player has scrolled or kept the window
    /// open for ReadAfterSeconds. If the list has an unread important article, closing it earlier, in any way (button, X,
    /// Escape), asks for confirmation: players of big mod lists close a lot of load-time popups in a row, and the notes would
    /// be skipped by habit. Without one, the window closes like any other.
    /// </summary>
    public class Dialog_WhatsNew : Window
    {
        private const float ReadAfterSeconds = 3f;
        private const float TitleHeight = 42f;
        private const float ButtonHeight = 38f;
        private const float CloseButtonWidth = 160f;
        private const float SettingsButtonWidth = 260f;
        private const float ArticleGap = 18f;

        private readonly List<NewsArticle> articles;
        private readonly bool protectClose;
        private Vector2 scrollPosition = Vector2.zero;
        private float openedAt;
        private bool read;
        private bool closeConfirmed;
        private float viewHeight;

        // Where the Close button is on the screen; the confirmation puts its harmless button over it
        public Rect CloseButtonScreenRect { get; private set; }

        public override Vector2 InitialSize => new Vector2(680f, Mathf.Min(640f, UI.screenHeight - 60f));

        public Dialog_WhatsNew(List<NewsArticle> articles)
        {
            this.articles = articles;
            protectClose = articles.Any(a => a.important && !NewsArticles.IsRead(a));
            doCloseX = true;
            doCloseButton = false;
            closeOnAccept = false;
            closeOnCancel = true; //goes through OnCloseRequest like every other way of closing
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            openedAt = Time.realtimeSinceStartup;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!read && Time.realtimeSinceStartup - openedAt >= ReadAfterSeconds) read = true;

            GameFont font = Text.Font;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, TitleHeight), "RealRuins.WhatsNew.Title".Translate());
            Text.Font = font;

            Rect outRect = new Rect(0f, TitleHeight, inRect.width, inRect.height - TitleHeight - ButtonHeight - 14f);
            DrawArticles(outRect);
            if (scrollPosition.y > 0f) read = true;

            float buttonsY = inRect.height - ButtonHeight;
            Rect settingsRect = new Rect(0f, buttonsY, SettingsButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(settingsRect, "RealRuins.WhatsNew.OpenSettings".Translate()))
            {
                OpenModSettings();
            }

            Rect closeRect = new Rect(inRect.width - CloseButtonWidth, buttonsY, CloseButtonWidth, ButtonHeight);
            CloseButtonScreenRect = new Rect(windowRect.x + Margin + closeRect.x, windowRect.y + Margin + closeRect.y, closeRect.width, closeRect.height);
            if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
            {
                Close();
            }
        }

        private void DrawArticles(Rect outRect)
        {
            Widgets.DrawMenuSection(outRect);
            Rect inner = outRect.ContractedBy(12f);
            float width = inner.width - 16f; //scrollbar
            Rect viewRect = new Rect(0f, 0f, width, Mathf.Max(viewHeight, inner.height));

            Widgets.BeginScrollView(inner, ref scrollPosition, viewRect);
            float y = 0f;
            GameFont font = Text.Font;
            foreach (NewsArticle article in articles)
            {
                if (!article.title.NullOrEmpty())
                {
                    Text.Font = GameFont.Medium;
                    float h = Text.CalcHeight(article.title, width);
                    Widgets.Label(new Rect(0f, y, width, h), article.title);
                    y += h + 6f;
                }
                Text.Font = GameFont.Small;
                if (!article.text.NullOrEmpty())
                {
                    float h = Text.CalcHeight(article.text, width);
                    Widgets.Label(new Rect(0f, y, width, h), article.text);
                    y += h;
                }
                y += ArticleGap;
            }
            Text.Font = font;
            viewHeight = y;
            Widgets.EndScrollView();
        }

        private void OpenModSettings()
        {
            read = true; //a deliberate click, not a reflex
            Close();
            Mod mod = LoadedModManager.GetMod<RealRuins_Mod>();
            if (mod != null) Find.WindowStack.Add(new Dialog_ModSettings(mod));
        }

        public override bool OnCloseRequest()
        {
            if (read || closeConfirmed || !protectClose)
            {
                NewsArticles.MarkRead(articles);
                return true;
            }
            Find.WindowStack.Add(new Dialog_SkipWhatsNew(this));
            return false;
        }

        /// <summary>The player confirmed closing unread notes. They are not shown again: skipping them was a choice.</summary>
        public void ConfirmClose()
        {
            closeConfirmed = true;
            Close();
        }
    }

    /// <summary>
    /// Asks before closing unread notes. Its "back" button lies right over the notes' Close button, and the button which
    /// really closes is far from it, so clicking twice at the same spot never skips the notes.
    /// </summary>
    public class Dialog_SkipWhatsNew : Window
    {
        private const float BackButtonWidth = 220f;
        private const float ConfirmButtonWidth = 200f;
        private const float ButtonHeight = 38f;

        private readonly Dialog_WhatsNew notes;

        public override Vector2 InitialSize => new Vector2(580f, 190f);

        public Dialog_SkipWhatsNew(Dialog_WhatsNew notes)
        {
            this.notes = notes;
            doCloseX = false;
            closeOnAccept = false; //Enter must not skip the notes either
            closeOnCancel = true;  //Escape goes back to the notes
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            forcePause = true;
        }

        // Bottom right corner lined up with the notes' Close button, so the back button covers it
        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            Rect anchor = notes.CloseButtonScreenRect;
            float x = anchor.width > 0f ? anchor.xMax + Margin - size.x : (UI.screenWidth - size.x) / 2f;
            float y = anchor.height > 0f ? anchor.yMax + Margin - size.y : (UI.screenHeight - size.y) / 2f;
            x = Mathf.Clamp(x, 0f, UI.screenWidth - size.x);
            y = Mathf.Clamp(y, 0f, UI.screenHeight - size.y);
            windowRect = new Rect(x, y, size.x, size.y).Rounded();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Widgets.Label(new Rect(0f, 0f, inRect.width, inRect.height - ButtonHeight - 10f), "RealRuins.WhatsNew.SkipText".Translate());

            float buttonsY = inRect.height - ButtonHeight;
            if (Widgets.ButtonText(new Rect(inRect.width - BackButtonWidth, buttonsY, BackButtonWidth, ButtonHeight), "RealRuins.WhatsNew.SkipBack".Translate()))
            {
                Close();
            }

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (Widgets.ButtonText(new Rect(0f, buttonsY, ConfirmButtonWidth, ButtonHeight), "RealRuins.WhatsNew.SkipConfirm".Translate()))
            {
                Close();
                notes.ConfirmClose();
            }
            GUI.color = prevColor;
        }
    }
}
