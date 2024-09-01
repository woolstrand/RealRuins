using System;
using UnityEngine;
using Verse;

namespace RealRuins;

public class SmallQuestionDialog : Window
{
	private string title;

	private string text;

	private string[] actions;

	private Action<int> completion;

	public override Vector2 InitialSize => new Vector2(500f, 290f);

	public SmallQuestionDialog(string title, string text, string[] actions, Action<int> completion)
	{
		this.title = title;
		this.text = text;
		this.actions = actions;
		this.completion = completion;
	}

	public override void DoWindowContents(Rect rect)
	{
		Listing_Standard listing_Standard = new Listing_Standard();
		listing_Standard.Begin(rect);
		Text.Font = GameFont.Medium;
		listing_Standard.Label(title);
		Text.Font = GameFont.Small;
		listing_Standard.Label(text);
		int num = actions.Length;
		Listing_Standard listing_Standard2 = new Listing_Standard();
		listing_Standard2.Begin(rect.BottomPartPixels(num * 30 + 10));
		for (int i = 0; i < num; i++)
		{
			if (listing_Standard2.ButtonText(actions[i]))
			{
				if (completion != null)
				{
					completion(i);
				}
				Close();
			}
		}
		listing_Standard2.End();
		listing_Standard.End();
	}
}
