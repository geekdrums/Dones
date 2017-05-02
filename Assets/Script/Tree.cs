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
	Line selectionStartLine_, selectionEndLine_;

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
			InstantiateLine(line);
		}

		Fields[0].IsFocused = true;

		SubscribeKeyInput();
	}

	protected void SubscribeKeyInput()
	{
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

		foreach( KeyCode key in throttleKeys )
		{
			// 最初の入力
			this.UpdateAsObservable()
				.Where(x => Input.GetKeyDown(key))
				.Merge(
			// 押しっぱなしにした時の自動連打
			this.UpdateAsObservable()
				.Where(x => Input.GetKey(key))
				.Delay(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamDelayTime))
				.ThrottleFirst(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamIntervalTime)))
				.TakeUntil(this.UpdateAsObservable().Where(x => Input.GetKeyUp(key)))
				.RepeatUntilDestroy(this)
				.Subscribe(_ => OnThrottleInput(key));
		}
	}
	
	// Update is called once per frame
	void Update()
	{
		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( Input.GetKeyDown(KeyCode.V) && ctrlOnly )
		{
			Paste();
		}
		else if( Input.GetKeyDown(KeyCode.Tab) )
		{
			if( shift == false )
			{
				int IndexInParent = focusedLine_.IndexInParent;
				if( IndexInParent > 0 )
				{
					focusedLine_.Parent[IndexInParent - 1].Add(focusedLine_);
				}
			}
			else
			{
				if( focusedLine_.Parent != null && focusedLine_.Parent.Parent != null )
				{
					focusedLine_.Parent.Parent.Insert(focusedLine_.Parent.IndexInParent + 1, focusedLine_);
				}
			}
		}

		if( Input.GetMouseButtonDown(0) )
		{
			if( selectionStartLine_ != null && selectionEndLine_ != null )
			{
				foreach( Line line in selectionStartLine_.GetBetween(selectionEndLine_) )
				{
					line.Field.IsSelected = false;
				}
				selectionStartLine_ = selectionEndLine_ = null;
			}

			if( focusedLine_ != null && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
			{
				selectionStartLine_ = focusedLine_;
			}
		}
		else if( Input.GetMouseButton(0) && selectionStartLine_ != null )
		{
			if( selectionEndLine_ == null )
				selectionEndLine_ = selectionStartLine_;

			int sign = 0;
			Rect rect = selectionEndLine_.Field.Rect;
			if( rect.yMin > Input.mousePosition.y )
			{
				sign = -1;
			}
			else if( rect.yMax < Input.mousePosition.y )
			{
				sign = 1;
			}

			if( sign != 0 )
			{
				int selectionSign = selectionStartLine_.Field.Rect.y < selectionEndLine_.Field.Rect.y ? 1 : -1;
				selectionEndLine_.Field.IsSelected = sign * selectionSign > 0;

				Line next = sign > 0 ? selectionEndLine_.PrevVisibleLine : selectionEndLine_.NextVisibleLine;
				while( next != null && (Input.mousePosition.y < next.Field.Rect.yMin || next.Field.Rect.yMax < Input.mousePosition.y) )
				{
					next.Field.IsSelected = sign * selectionSign > 0;
					next = sign > 0 ? next.PrevVisibleLine : next.NextVisibleLine;
				}

				if( next != null )
				{
					selectionEndLine_ = next;
					selectionEndLine_.Field.IsSelected = true;
				}
				selectionStartLine_.Field.IsSelected = true;
			}
		}
	}

	protected void InstantiateLine(Line line)
	{
		line.Bind(Instantiate(FieldPrefab.gameObject));
		Fields.Add(line.Field);
	}

	protected void OnThrottleInput(KeyCode key)
	{
		if( focusedLine_ == null ) return;

		int caretPos = focusedLine_.Field.CaretPosision;

		switch( key )
		{
		case KeyCode.Return:
			{
				Line line = new Line();
				if( caretPos == 0 && focusedLine_.TextLength > 0 )
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
				InstantiateLine(line);
				line.Field.IsFocused = (caretPos == 0 && focusedLine_.TextLength > 0) == false;
			}
			break;
		
		// backspace & delete
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
		
		// arrow keys
		case KeyCode.DownArrow:
			{
				Line next = focusedLine_.NextVisibleLine;
				if( next != null )
				{
					next.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.UpArrow:
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					prev.Field.IsFocused = true;
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
				}
			}
			break;
		}
	}

	protected static string Clipboard
	{
		get
		{
			return GUIUtility.systemCopyBuffer;
		}
		set
		{
			GUIUtility.systemCopyBuffer = value;
		}
	}
	protected static string[] separator = new string[] { System.Environment.NewLine };
	protected static string[] tabstrings = new string[] { "	", "    " };
	protected void Paste()
	{
		string[] cilpboardLines = Clipboard.Split(separator, System.StringSplitOptions.None);
		focusedLine_.Field.Paste(cilpboardLines[0]);

		int oldLevel = 0;
		int currentLevel = 0;
		Line parent = focusedLine_.Parent;
		Line brother = focusedLine_;
		for( int i = 1; i < cilpboardLines.Length; ++i )
		{
			string text = cilpboardLines[i];
			currentLevel = 0;
			while( text.StartsWith("	") )
			{
				++currentLevel;
				text = text.Remove(0, 1);
			}

			Line line = new Line(text);
			if( currentLevel > oldLevel )
			{
				brother.Add(line);
				parent = brother;
			}
			else if( currentLevel == oldLevel )
			{
				parent.Insert(brother.IndexInParent + 1, line);
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;

					brother = parent;
					parent = parent.Parent;
				}
				parent.Insert(brother.IndexInParent + 1, line);
			}
			InstantiateLine(line);
			brother = line;
			oldLevel = currentLevel;
		}
	}
	

	public void OnFocused(Line line)
	{
		focusedLine_ = line;
		selectionStartLine_ = line;
	}
}
