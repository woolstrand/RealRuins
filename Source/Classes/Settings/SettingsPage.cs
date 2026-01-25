using System;
using Verse;
using UnityEngine;

namespace RealRuins.Settings
{
    /// <summary>
    /// Base class for settings pages in the tabbed interface
    /// </summary>
    public abstract class SettingsPage
    {
        public abstract string TabLabel { get; }
        
        public abstract void Draw(Rect rect);
        
        protected void ReadableLabeledTextInput(Rect rect, string title, ref int value, ref string buffer)
        {
            Rect rect2 = rect.LeftHalf().Rounded();
            Rect rect3 = rect.RightPartPixels(100);
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect2, title);
            Text.Anchor = anchor;
            Widgets.TextFieldNumeric(rect3, ref value, ref buffer);
        }
    }
}
