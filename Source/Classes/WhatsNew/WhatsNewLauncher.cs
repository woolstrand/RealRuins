using System;
using System.Collections.Generic;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealRuins.WhatsNew
{
    /// <summary>
    /// Opens the what's new window once per launch, on the main menu. To stay out of the way of other mods' startup
    /// popups (update news, load error reports, mod list warnings), it waits until loading is over, gives them a moment to
    /// open, and then until no other dialog is open, so it never lands on top of another window.
    /// </summary>
    public static class WhatsNewLauncher
    {
        private const float MenuSettleSeconds = 1.5f;

        private static bool done;
        private static float menuSeenAt = -1f;

        public static void OnMainMenuGUI()
        {
            if (done || Event.current.type != EventType.Repaint) return;
            try
            {
                if (LongEventHandler.AnyEventNowOrWaiting) return;
                float now = Time.realtimeSinceStartup;
                if (menuSeenAt < 0f) menuSeenAt = now;
                if (now - menuSeenAt < MenuSettleSeconds || OtherDialogOpen()) return;

                done = true;
                List<NewsArticle> unread = NewsArticles.Unread();
                if (unread.Count > 0) Find.WindowStack.Add(new Dialog_WhatsNew(unread));
            }
            catch (Exception e)
            {
                done = true; //once, not every frame
                Log.Error("[RealRuins] Could not show what's new: " + e);
            }
        }

        // Dev tools (the debug log opens by itself on errors) don't count: they aren't popups the player has to deal with
        private static bool OtherDialogOpen()
        {
            foreach (Window window in Find.WindowStack.Windows)
            {
                if (window.layer == WindowLayer.Dialog && !(window is Window_Dev) && !window.IsDebug) return true;
            }
            return false;
        }

        [HarmonyPatch(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI))]
        private static class MainMenuDrawer_MainMenuOnGUI_Patch
        {
            private static void Postfix() => OnMainMenuGUI();
        }
    }
}
