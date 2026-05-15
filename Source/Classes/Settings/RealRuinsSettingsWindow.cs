using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    /// <summary>
    /// Main settings window that manages tabbed UI for all settings pages
    /// </summary>
    public class RealRuinsSettingsWindow
    {
        private List<SettingsPage> pages;
        private int selectedTabIndex = 0;
        private Vector2 scrollPosition = Vector2.zero;

        // Tab styling colors - each tab has a distinct color
        private static readonly Color[] TabAccentColors = new Color[]
        {
            new Color(0.2f, 0.35f, 0.5f),   // Network & Cache - Blue
            new Color(0.2f, 0.45f, 0.3f),   // Map Generation - Green
            new Color(0.4f, 0.3f, 0.45f),   // Events & Advanced - Purple
            new Color(0.45f, 0.35f, 0.2f),  // Planetary Ruins - Orange
            new Color(0.55f, 0.2f, 0.2f)    // Debug - Red
        };

        private static readonly Color TabBackgroundColor = new Color(0.09f, 0.09f, 0.09f);
        private static readonly Color TabBorderColor = new Color(0.2f, 0.2f, 0.2f);

        public RealRuinsSettingsWindow()
        {
            InitializePages();
        }

        private void InitializePages()
        {
            pages = new List<SettingsPage>
            {
                new NetworkCacheSettingsPage(),
                new MapGenerationSettingsPage(),
                new AdvancedSettingsPage(),
                new PlanetaryRuinsSettingsPage(),
                new DebugSettingsPage()
            };
        }

        public void DoSettingsWindowContents(Rect rect)
        {
            // Draw tabs
            Rect tabRect = rect.TopPartPixels(50f);
            DrawTabs(tabRect);

            // Draw padding/spacing between tabs and content
            Rect spacingRect = rect.TopPartPixels(60f).BottomPartPixels(10f);
            
            // Draw current page content
            Rect contentRect = rect.BottomPartPixels(rect.height - 60f);
            DrawPageContent(contentRect);
        }

        private void DrawTabs(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, TabBackgroundColor);

            float tabSpacing = 2f;
            float tabWidth = (rect.width - tabSpacing * (pages.Count - 1)) / pages.Count;
            
            for (int i = 0; i < pages.Count; i++)
            {
                float xOffset = i * tabWidth + (i * tabSpacing);
                float adjustedWidth = tabWidth - tabSpacing;
                Rect tabRect = new Rect(rect.x + xOffset, rect.y, adjustedWidth, rect.height);
                DrawTab(tabRect, i);
            }
        }

        private void DrawTab(Rect rect, int tabIndex)
        {
            bool isSelected = selectedTabIndex == tabIndex;
            
            // Get accent color for this tab
            Color accentColor = TabAccentColors[tabIndex % TabAccentColors.Length];

            // Background - darker version of accent color when unselected, brighter when selected
            Color bgColor;
            if (isSelected)
            {
                bgColor = accentColor; // Full brightness when selected
            }
            else
            {
                bgColor = accentColor * 0.4f; // Darker tint when unselected
                if (Mouse.IsOver(rect))
                {
                    bgColor = accentColor * 0.6f; // Medium tint on hover
                }
            }
            
            Widgets.DrawBoxSolid(rect, bgColor);

            // Border
            Widgets.DrawBox(rect, 1);

            // Label
            TextAnchor anchor = Text.Anchor;
            GameFont gameFont = Text.Font;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(rect.ContractedBy(5f), pages[tabIndex].TabLabel);
            Text.Anchor = anchor;
            Text.Font = gameFont;

            // Handle click
            if (Widgets.ButtonInvisible(rect))
            {
                if (selectedTabIndex != tabIndex)
                {
                    selectedTabIndex = tabIndex;
                    scrollPosition = Vector2.zero;
                }
            }
        }

        private void DrawPageContent(Rect rect)
        {
            float innerHeight = pages[selectedTabIndex].ContentHeight;
            float viewportHeight = rect.height;
            float scrollHeight = Mathf.Max(innerHeight, viewportHeight);
            
            Rect innerRect = new Rect(0, 0, rect.width - 20f, scrollHeight);
            
            Widgets.BeginScrollView(
                rect,
                ref scrollPosition,
                innerRect);

            pages[selectedTabIndex].Draw(innerRect);

            Widgets.EndScrollView();
        }
    }
}
