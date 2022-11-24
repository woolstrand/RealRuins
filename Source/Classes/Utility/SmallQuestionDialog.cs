using System;
using UnityEngine;
using Verse;
using RimWorld;

namespace RealRuins {
    public class SmallQuestionDialog: Window {

        private string title;
        private string text;
        private string[] actions;
        private Action<int> completion;

        public override Vector2 InitialSize => new Vector2(500, 290);

        public SmallQuestionDialog(string title, string text, string[] actions, Action<int> completion) {
            this.title = title;
            this.text = text;
            this.actions = actions;
            this.completion = completion;
        }

        public override void DoWindowContents(Rect rect) {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);

            Text.Font = GameFont.Medium;
            list.Label(title);
            Text.Font = GameFont.Small;
            list.Label(text);

            int count = actions.Length;
            Listing_Standard bottomList = new Listing_Standard();
            bottomList.Begin(rect.BottomPartPixels(count * 30 + 10));
            for (int i = 0; i < count; i ++) {
                if (bottomList.ButtonText(actions[i])) {
                    completion(i);
                    this.Close();
                }
            }
            bottomList.End();
            list.End();
        }
    }
}
