using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;

public class Tree : MonoBehaviour {
	
	public TextField FieldPrefab;
	public List<TextField> Fields = new List<TextField>();

	Line rootLine_;
	Line focusedLine_;

	// Use this for initialization
	void Awake () {
		// test
		rootLine_ = new Line("CategoryName");
		rootLine_.Add(new Line("Hello World"));
		rootLine_.Add(new Line("Hello1"));
		rootLine_.Add(new Line("Hello2"));
		rootLine_.Add(new Line("Hello3"));
		rootLine_.Add(new Line("Hello4"));
		rootLine_.Add(new Line("Hello5"));
		rootLine_.Add(new Line("Hello6"));
		rootLine_.Bind(this.gameObject);

		foreach( Line line in rootLine_.VisibleTree )
		{
			line.Bind(Instantiate(FieldPrefab.gameObject));
			Fields.Add(line.Field);
		}

		Fields[0].IsFocused = true;

		KeyCode[] throttleKeys = new KeyCode[]
		{
			KeyCode.UpArrow,
			KeyCode.DownArrow,
			KeyCode.RightArrow,
			KeyCode.LeftArrow,
			KeyCode.Return,
			KeyCode.Backspace,
			KeyCode.Delete
		};

		foreach(KeyCode key in throttleKeys )
		{
			// 最初の入力
			this.UpdateAsObservable()
				.Where(x => Input.GetKeyDown(key))
				.Subscribe(_ => OnThrottleInput(key));

			// 押しっぱなしにした時の自動連打
			this.UpdateAsObservable()
				.Where(x => Input.GetKey(key))
				.Delay(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamDelayTime))
				.ThrottleFirst(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamIntervalTime))
				.TakeUntil(this.UpdateAsObservable().Where(x => Input.GetKeyUp(key)))
				.RepeatUntilDestroy(this)
				.Subscribe(_ => OnThrottleInput(key));
		}
	}
	
	// Update is called once per frame
	void Update ()
	{
		if( focusedLine_ == null )
		{
			return;
		}

		if( Input.GetKeyDown(KeyCode.Tab) )
		{
			if( Input.GetKey(KeyCode.LeftShift) )
			{
				if( focusedLine_.Parent != null && focusedLine_.Parent.Parent != null )
				{
					focusedLine_.Parent.Parent.Insert(focusedLine_.Parent.IndexInParent + 1, focusedLine_);
				}
			}
			else
			{
				int IndexInParent = focusedLine_.IndexInParent;
				if( IndexInParent > 0 )
				{
					focusedLine_.Parent[IndexInParent - 1].Add(focusedLine_);
				}
			}
		}
	}

	// 長押しで連続入力可能なもの
	protected void OnThrottleInput(KeyCode arrow)
	{
		if( focusedLine_ == null ) return;

		int caretPos = focusedLine_.Field.CaretPosision;

		switch( arrow )
		{
		case KeyCode.Return:
			{
				Line line = new Line();
				if( caretPos == 0 )
				{
					focusedLine_.Parent.Insert(focusedLine_.IndexInParent, line);
				}
				else
				{
					string subString = focusedLine_.Text.Substring(0, caretPos);
					string newString = focusedLine_.Text.Substring(caretPos, focusedLine_.TextLength - caretPos);
					focusedLine_.Text = subString;
					line.Text = newString;
					if( focusedLine_.HasVisibleChild )
					{
						focusedLine_.Insert(0, line);
					}
					else
					{
						focusedLine_.Parent.Insert(focusedLine_.IndexInParent + 1, line);
					}
				}
				line.Bind(Instantiate(FieldPrefab.gameObject));
				Fields.Add(line.Field);
				if( caretPos > 0 )
				{
					line.Field.IsFocused = true;
					focusedLine_ = line;
				}
				else
				{
					focusedLine_.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.Backspace:
			if( caretPos == 0 )
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.CaretPosision = prev.TextLength;
					prev.Text += focusedLine_.Text;
					prev.Field.IsFocused = true;

					List<Line> children = new List<Line>(focusedLine_);

					prev.EnableRecursiveLayout = false;
					foreach( Line child in children )
					{
						prev.Insert(0, child);
					}
					prev.EnableRecursiveLayout = true;

					focusedLine_.Parent.Remove(focusedLine_);

					focusedLine_ = prev;
				}
			}
			break;
		case KeyCode.Delete:
			if( caretPos == focusedLine_.TextLength )
			{
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					focusedLine_.Text += next.Text;

					List<Line> children = new List<Line>(next);

					focusedLine_.EnableRecursiveLayout = false;
					foreach( Line child in children )
					{
						focusedLine_.Insert(0, child);
					}
					focusedLine_.EnableRecursiveLayout = true;

					next.Parent.Remove(next);
				}
			}
			break;
		case KeyCode.DownArrow:
			{
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					next.Field.IsFocused = true;
					focusedLine_ = next;
				}
			}
			break;
		case KeyCode.UpArrow:
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.IsFocused = true;
					focusedLine_ = prev;
				}
			}
			break;
		case KeyCode.RightArrow:
			if( caretPos >= focusedLine_.TextLength )
			{
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					next.Field.CaretPosision = 0;
					next.Field.IsFocused = true;
					focusedLine_ = next;
				}
			}
			break;
		case KeyCode.LeftArrow:
			if( caretPos <= 0 )
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.CaretPosision = prev.TextLength;
					prev.Field.IsFocused = true;
					focusedLine_ = prev;
				}
			}
			break;
		}
	}

	public void OnFocused(Line line)
	{
		focusedLine_ = line;
	}
}
