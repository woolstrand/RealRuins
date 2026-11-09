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

        // Measured on every draw: greyed and expanding controls change the height, and the scroll view uses last frame's value
        private float contentHeight = 900f;

        /// <summary>
        /// Height of this page's content in pixels. Used by the scroll view.
        /// </summary>
        public float ContentHeight => contentHeight;

        public void Draw(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            // a page taller than the view must not wrap into a second (invisible) column
            listing.maxOneColumn = true;
            listing.Begin(rect);
            DrawContents(listing);
            contentHeight = listing.CurHeight + 10f;
            listing.End();
        }

        protected abstract void DrawContents(Listing_Standard listing);
    }
}
