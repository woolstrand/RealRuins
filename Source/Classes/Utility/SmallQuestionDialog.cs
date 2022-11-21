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

        public override Vector2 InitialSize => new Vector2(500, 240);

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
            int buttonWidth = (((int)(rect.width) - 20 - 10 * (count - 1)) / count);
            for (int i = 0; i < count; i ++) {
                if (list.ButtonText(actions[i])) {
                    completion(i);
                    this.Close();
                }
            }
        }
    }
}
