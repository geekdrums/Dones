using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UniRx;
using UniRx.Triggers;

public class Tree : MonoBehaviour {
	
	public TextField FieldPrefab;


	#region params

	List<TextField> fields_ = new List<TextField>();
	Line rootLine_;
	Line focusedLine_;
	Line selectionStartLine_, selectionEndLine_;
	SortedList<int, Line> selectedLines_ = new SortedList<int, Line>();

	bool deleteKeyConsumed_ = false;

	#endregion


	#region unity functions

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

		OnFocused(fields_[0].BindedLine);

		SubscribeKeyInput();
	}
	
	// Update is called once per frame
	void Update()
	{
		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;
		
		// keyboard input
		if( Input.GetKeyDown(KeyCode.V) && ctrlOnly )
		{
			Paste();
		}
		else if( Input.GetKeyDown(KeyCode.Tab) )
		{
			if( shift )
			{
				OnShiftTabInput();
			}
			else
			{
				OnTabInput();
			}
		}

		// mouse input
		if( Input.GetMouseButtonDown(0) )
		{
			if( shift )
			{
				// ctrl + shift の場合は選択を追加する、ctrlが無ければ以前の選択は解除する
				if( ctrl == false )
				{
					ClearSelection(clearStartAndEnd: false);
				}
				// shift選択で、selectionStartLine_からクリックしたところまでを新たに選択する
				if( selectionStartLine_ != null )
				{
					foreach( Line line in selectionStartLine_.GetUntil(Input.mousePosition.y) )
					{
						UpdateSelection(line, true);
						selectionEndLine_ = line;
					}
				}
			}
			else if( ctrl )
			{
				// ctrl選択で、新たにクリックしたところだけを追加する
				Line line = null;
				TextField field = EventSystem.current.currentSelectedGameObject.GetComponent<TextField>();
				if( field != null ) line = field.BindedLine;
				if( line != null )
				{
					UpdateSelection(line, true);
					selectionStartLine_ = selectionEndLine_ = line;
				}
			}
			else
			{
				// ctrlもshiftもなしにクリックした場合は選択解除
				ClearSelection();
			}

			if( focusedLine_ != null && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
			{
				selectionStartLine_ = focusedLine_;
			}
		}
		else if( Input.GetMouseButton(0) && selectionStartLine_ != null )
		{
			// マウス動かし中の選択動作
			if( selectionEndLine_ == null )
				selectionEndLine_ = selectionStartLine_;

			// 現在の選択末尾から上に移動したか下に移動したかを見る
			int moveSign = 0;
			Rect rect = selectionEndLine_.Field.Rect;
			if( rect.yMin > Input.mousePosition.y )
			{
				moveSign = -1;
			}
			else if( rect.yMax < Input.mousePosition.y )
			{
				moveSign = 1;
			}

			// 移動していたら
			if( moveSign != 0 )
			{
				foreach( Line line in selectionEndLine_.GetUntil(Input.mousePosition.y) )
				{
					UpdateSelection(line, moveSign * SelectionSign >= 0/* 移動方向と選択方向が逆なら選択解除 */ || line == selectionStartLine_);
					selectionEndLine_ = line;
				}
			}
		}
	}

	#endregion


	#region selections

	/// <summary>
	/// startから見たendの方向。上(Prev)がプラス。下(Next)がマイナス。
	/// </summary>
	int SelectionSign
	{
		get
		{
			if( selectionStartLine_ == null || selectionEndLine_ == null || selectionStartLine_ == selectionEndLine_ ) return 0;
			else return selectionStartLine_.Field.RectY < selectionEndLine_.Field.RectY ? 1 : -1;
		}
	}

	bool HasSelection { get { return selectedLines_.Count > 0; } }

	protected void UpdateSelection(Line line, bool isSelected)
	{
		line.Field.IsSelected = isSelected;
		if( isSelected && selectedLines_.Values.Contains(line) == false )
		{
			selectedLines_.Add(-(int)line.Binding.transform.position.y, line);
		}
		else if( isSelected == false && selectedLines_.Values.Contains(line) )
		{
			selectedLines_.Remove(-(int)line.Binding.transform.position.y);
		}
	}

	protected void ClearSelection(bool clearStartAndEnd = true)
	{
		foreach( Line line in selectedLines_.Values )
		{
			line.Field.IsSelected = false;
		}
		selectedLines_.Clear();

		if( clearStartAndEnd ) selectionStartLine_ = selectionEndLine_ = null;
	}

	#endregion


	#region Input

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

	protected void OnThrottleInput(KeyCode key)
	{
		if( focusedLine_ == null ) return;
		
		switch( key )
		{
		case KeyCode.Return:
			OnEnterInput();
			break;
		case KeyCode.Backspace:
			OnBackspaceInput();
			break;
		case KeyCode.Delete:
			OnDeleteInput();
			break;
		case KeyCode.DownArrow:
		case KeyCode.UpArrow:
		case KeyCode.RightArrow:
		case KeyCode.LeftArrow:
			if( Input.GetKey(KeyCode.LeftShift) ) OnShiftArrowInput(key);
			else OnArrowInput(key);
			break;
		}
	}

	protected IEnumerable<Line> GetTargetLines(bool ascending = true)
	{
		if( HasSelection )
		{
			if( ascending )
			{
				foreach( Line line in selectedLines_.Values )
				{
					yield return line;
				}
			}
			else
			{
				for( int i = selectedLines_.Count - 1; i >= 0; --i )
				{
					yield return selectedLines_.Values[i];
				}
			}
		}
		else if( focusedLine_ != null )
		{
			yield return focusedLine_;
		}
	}

	protected void OnTabInput()
	{
		foreach( Line line in GetTargetLines() )
		{
			int IndexInParent = line.IndexInParent;
			if( IndexInParent > 0 )
			{
				Line newParent = line.Parent[IndexInParent - 1];
				newParent.Add(line);

				if( newParent.IsFolded )
				{
					newParent.IsFolded = false;
					newParent.AdjustLayoutRecursive();
				}
				else
				{
					line.AdjustLayout(Line.Direction.X);
				}
			}
			else
			{
				break;
			}
		}
	}

	protected void OnShiftTabInput()
	{
		foreach( Line line in GetTargetLines(ascending: false) )
		{
			if( line.Parent != null && line.Parent.Parent != null && ( line.Parent.Field.IsSelected == false || line.Parent.Level <= 1 ) )
			{
				Line nextLine = line.Parent[line.IndexInParent + 1];
				
				Line newParent = line.Parent.Parent;
				newParent.Insert(line.Parent.IndexInParent + 1, line);
				line.AdjustLayout(Line.Direction.XY);

				if( nextLine != null && nextLine.Field.IsSelected == false )
				{
					nextLine.Parent.AdjustLayoutRecursive(nextLine.IndexInParent);
				}
			}
		}
	}

	protected void OnEnterInput()
	{
		int caretPos = focusedLine_.Field.CaretPosision;
		Line line = new Line();
		if( caretPos == 0 && focusedLine_.TextLength > 0 )
		{
			focusedLine_.Parent.Insert(focusedLine_.IndexInParent, line);
			focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.IndexInParent);
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
				focusedLine_.AdjustLayoutRecursive();
			}
			else
			{
				focusedLine_.Parent.Insert(focusedLine_.IndexInParent + 1, line);
				focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.IndexInParent + 1);
			}
		}
		InstantiateLine(line);
		line.Field.IsFocused = (caretPos == 0 && focusedLine_.TextLength > 0) == false;
	}

	protected void OnBackspaceInput()
	{
		if( focusedLine_.Field.CaretPosision == 0 )
		{
			Line prev = focusedLine_.PrevVisibleLine;
			if( prev == null ) return;

			if( prev.Parent == focusedLine_.Parent && prev.TextLength == 0 )
			{
				prev.Parent.Remove(prev);
				focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.IndexInParent);
			}
			else
			{
				prev.Field.CaretPosision = prev.TextLength;
				prev.Text += focusedLine_.Text;
				prev.Field.IsFocused = true;

				List<Line> children = new List<Line>(focusedLine_);
				for( int i = 0; i < children.Count; ++i )
				{
					prev.Insert(i, children[i]);
					children[i].AdjustLayout();
				}

				Line layoutStart = focusedLine_.NextVisibleLine;
				focusedLine_.Parent.Remove(focusedLine_);
				if( layoutStart != null )
				{
					layoutStart.Parent.AdjustLayoutRecursive(layoutStart.IndexInParent);
				}
			}
		}
	}

	protected void OnDeleteInput()
	{
		if( deleteKeyConsumed_ )
		{
			deleteKeyConsumed_ = false;
			return;
		}

		if( focusedLine_.Field.CaretPosision == focusedLine_.TextLength )
		{
			Line next = focusedLine_.NextVisibleLine;
			if( next == null ) return;

			if( next.Parent == focusedLine_.Parent && next.TextLength == 0 )
			{
				next.Parent.Remove(next);
				focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.IndexInParent + 1);
			}
			else
			{
				focusedLine_.Text += next.Text;

				List<Line> children = new List<Line>(next);
				for( int i = 0; i < children.Count; ++i )
				{
					focusedLine_.Insert(i, children[i]);
					children[i].AdjustLayout();
				}

				Line layoutStart = next.NextVisibleLine;
				next.Parent.Remove(next);
				if( layoutStart != null )
				{
					layoutStart.Parent.AdjustLayoutRecursive(layoutStart.IndexInParent);
				}
			}
		}
	}

	protected void OnArrowInput(KeyCode key)
	{
		// 選択があれば解除
		if( HasSelection )
		{
			selectionEndLine_.Field.IsFocused = true;
			focusedLine_ = selectionEndLine_;
			ClearSelection();
		}

		switch( key )
		{
		case KeyCode.DownArrow:
		case KeyCode.UpArrow:
			// フォーカスを上下に移動
			Line line = (key == KeyCode.DownArrow ? focusedLine_.NextVisibleLine : focusedLine_.PrevVisibleLine);
			if( line != null )
			{
				line.Field.IsFocused = true;
			}
			break;
		case KeyCode.RightArrow:
			// カーソル位置が最後ならフォーカス移動
			if( focusedLine_.Field.CaretPosision >= focusedLine_.TextLength )
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
			// カーソル位置が最初ならフォーカス移動
			if( focusedLine_.Field.CaretPosision <= 0 )
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

	protected void OnShiftArrowInput(KeyCode key)
	{
		// とりあえず選択開始する
		if( selectionStartLine_ == null || selectionEndLine_ == null )
		{
			selectionStartLine_ = selectionEndLine_ = focusedLine_;
		}

		switch( key )
		{
		case KeyCode.DownArrow:
		case KeyCode.UpArrow:
			// 選択行を上下に追加または削除
			int sign = key == KeyCode.DownArrow ? 1 : -1;
			Line line = (sign > 0 ? selectionEndLine_.NextVisibleLine : selectionEndLine_.PrevVisibleLine);
			if( line != null )
			{
				if( SelectionSign * sign > 0 ) UpdateSelection(selectionEndLine_, false);
				selectionEndLine_ = line;
				if( SelectionSign * sign < 0 ) UpdateSelection(line, true);
			}
			UpdateSelection(selectionStartLine_, true);
			break;
		case KeyCode.RightArrow:
			// shift + →で今の行を選択
			if( focusedLine_.Field.IsSelected == false && focusedLine_.Field.CaretPosision >= focusedLine_.TextLength )
			{
				UpdateSelection(focusedLine_, true);
			}
			break;
		case KeyCode.LeftArrow:
			// shift + ←でも今の行を選択
			if( focusedLine_.Field.IsSelected == false && focusedLine_.Field.CaretPosision <= 0 )
			{
				UpdateSelection(focusedLine_, true);
			}
			break;
		}
	}

	#endregion


	#region clipboard

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
		Line layoutStart = focusedLine_.NextVisibleLine;
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

		if( layoutStart != null )
		{
			layoutStart.Parent.AdjustLayoutRecursive(layoutStart.IndexInParent);
		}
	}

	#endregion


	#region utils

	protected void InstantiateLine(Line line)
	{
		line.Bind(Instantiate(FieldPrefab.gameObject));
		fields_.Add(line.Field);
	}

	#endregion


	#region events

	public void OnFocused(Line line)
	{
		focusedLine_ = line;
		selectionStartLine_ = line;
	}

	public void OnDeleteKeyConsumed()
	{
		deleteKeyConsumed_ = true;
	}

	#endregion
}
