using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShortLine : Selectable {

	public Line BindedLine { get; private set; }

	public string Text { get { return textComponent_.text; } set { textComponent_.text = value; } }
	Text textComponent_;

	CheckMark checkMark_;

	protected override void Awake()
	{
		base.Awake();
		textComponent_ = GetComponentInChildren<Text>();
		checkMark_ = GetComponentInChildren<CheckMark>(includeInactive: true);
	}

	// Update is called once per frame
	void Update () {
		//if( currentSelectionState == SelectionState.Highlighted )
		//{

		//}
	}

	public void Bind(Line line)
	{
		BindedLine = line;
		Text = line.Text;
	}
}
