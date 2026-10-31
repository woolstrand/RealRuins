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

        /// <summary>
        /// Height of this page's content in pixels. Used by the scroll view.
        /// Override in subclasses to avoid unnecessary scrollbars.
        /// </summary>
        public virtual float ContentHeight => 900f;

        public abstract void Draw(Rect rect);
        
        // synced is the value the buffer was last filled from. Pages outlive settings import and reset, so a changed value
        // means it was set elsewhere and the buffer has to be refilled (vanilla fills it only when it is null).
        protected void ReadableLabeledTextInput(Rect rect, string title, ref int value, ref string buffer, ref int synced)
        {
            if (buffer == null || value != synced)
            {
                buffer = value.ToString();
            }
            Rect rect2 = rect.LeftHalf().Rounded();
            Rect rect3 = rect.RightPartPixels(100);
            TextAnchor anchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect2, title);
            Text.Anchor = anchor;
            Widgets.TextFieldNumeric(rect3, ref value, ref buffer);
            synced = value;
        }
    }
}
