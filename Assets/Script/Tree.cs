using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;

public class Tree : MonoBehaviour {
	
	// editor params
	public TextField FieldPrefab;
	
	#region params

	List<TextField> fields_ = new List<TextField>();
	Line rootLine_;
	Line focusedLine_;
	Line selectionStartLine_, selectionEndLine_;
	SortedList<int, Line> selectedLines_ = new SortedList<int, Line>();
	ActionManager actionManager_ = new ActionManager();

	// input states
	bool wasDeleteKeyConsumed_ = false;
	bool wasCtrlMInput_ = false;
	bool isAllFolded_ = false;
	List<Line> requestLayoutLines_ = new List<Line>();
	int suspendLayoutCount_ = 0;

	// components
	LayoutElement layout_;
	ScrollRect scrollRect_;

	#endregion


	#region unity functions

	// Use this for initialization
	void Awake () {
		
		layout_ = GetComponentInParent<LayoutElement>();
		scrollRect_ = GetComponentInParent<ScrollRect>();

		actionManager_.ChainStarted += this.actionManager__ChainStarted;
		actionManager_.ChainEnded += this.actionManager__ChainEnded;
		// todo: 複数ツリーで切り替え
		GameContext.CurrentActionManager = actionManager_;

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

		foreach( Line line in rootLine_.GetAllChildren() )
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
		if( ctrlOnly )
		{
			if( Input.GetKeyDown(KeyCode.V) )
			{
				Paste();
			}
			else if( Input.GetKeyDown(KeyCode.C) )
			{
				Copy();
			}
			else if( Input.GetKeyDown(KeyCode.X) )
			{
				Cut();
			}
			else if( Input.GetKeyDown(KeyCode.A) )
			{
				SelectAll();
			}
			else if( Input.GetKeyDown(KeyCode.M) )
			{
				wasCtrlMInput_ = true;
			}
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

		if( wasCtrlMInput_ && Input.anyKeyDown )
		{
			if( ( Input.GetKeyDown(KeyCode.M) && ctrlOnly ) == false )
			{
				wasCtrlMInput_ = false;
			}
			if( Input.GetKeyDown(KeyCode.L) && ctrlOnly )
			{
				OnCtrlMLInput();
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

			if( focusedLine_ != null && focusedLine_.Field != null && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
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
	protected int SelectionSign
	{
		get
		{
			if( selectionStartLine_ == null || selectionEndLine_ == null || selectionStartLine_ == selectionEndLine_ ) return 0;
			else return selectionStartLine_.Field.RectY < selectionEndLine_.Field.RectY ? 1 : -1;
		}
	}

	public bool HasSelection { get { return selectedLines_.Count > 0; } }

	protected void UpdateSelection(Line line, bool isSelected)
	{
		line.Field.IsSelected = isSelected;
		if( isSelected && selectedLines_.Values.Contains(line) == false )
		{
			selectedLines_.Add(-(int)(line.Binding.transform.position.y - this.transform.position.y), line);
		}
		else if( isSelected == false && selectedLines_.Values.Contains(line) )
		{
			selectedLines_.Remove(-(int)(line.Binding.transform.position.y - this.transform.position.y));
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

	protected void SelectAll()
	{
		ClearSelection();

		selectionStartLine_ = rootLine_[0];
		Line line = selectionStartLine_;
		while( line != null )
		{
			line.Field.IsSelected = true;
			selectedLines_.Add(-(int)(line.Binding.transform.position.y - this.transform.position.y), line);
			selectionEndLine_ = line;
			line = line.NextVisibleLine;
		}
	}

	/// <summary>
	/// 選択部分を消して、新たに入力可能となった行を返す
	/// </summary>
	/// <returns></returns>
	public Line DeleteSelection()
	{
		actionManager_.StartChain();

		SortedList<int, Line> oldSelection = new SortedList<int, Line>(selectedLines_);
		Line oldSelectStart = selectionStartLine_;
		Line oldSelectEnd = selectionEndLine_;

		Line oldSelectTop = selectedLines_.Values[0];
		Line oldParent = oldSelectTop.Parent;
		int oldIndex = oldSelectTop.Index;

		// （入力のために）新しい行を作る
		Line newLine = new Line();
		actionManager_.Execute(new Action(
			execute: () =>
			{
				oldParent.Insert(oldIndex, newLine);
				InstantiateLine(newLine);
				newLine.Field.IsFocused = true;
			},
			undo: () =>
			{
				oldParent.Remove(newLine);
			}
			));

		List<Action> reparentActions = new List<Action>();
		List<Action> deleteActions = new List<Action>();

		foreach( Line line in GetSelectedOrFocusedLines(ascending: false) )
		{
			if( line.HasVisibleChild )
			{
				List<Line> lostChildren = new List<Line>(from lostChild in line where lostChild.Field.IsSelected == false select lostChild);
				// Childがいたら、それを上の親に切り替える
				if( lostChildren.Count > 0 )
				{
					Line prev = line.PrevVisibleLine;
					while( prev.Field.IsSelected )
					{
						// 選択中のやつは消されるので、消されないものの中で一番近いものを選ぶ
						prev = prev.PrevVisibleLine;
						if( prev == null ) prev = rootLine_;
					}
					Line lostParent = line;
					reparentActions.Add(new Action(
						execute: () =>
						{
							for( int i = 0; i < lostChildren.Count; ++i )
							{
								prev.Insert(i, lostChildren[i]);
								lostChildren[i].AdjustLayout();
							}
						},
						undo: () =>
						{
							for( int i = 0; i < lostChildren.Count; ++i )
							{
								lostParent.Add(lostChildren[i]);
								lostChildren[i].AdjustLayout();
							}
						}
						));
				}
			}

			Line parent = line.Parent;
			int index = line.Index;
			deleteActions.Add(new Action(
				execute: () =>
				{
					Line layoutStart = line.NextVisibleLine;
					line.Parent.Remove(line);
					if( layoutStart != null && layoutStart.Field.IsSelected == false )
					{
						requestLayoutLines_.Add(layoutStart);
					}
				},
				undo: () =>
				{
					parent.Insert(index, line);
					InstantiateLine(line);
					line.Field.IsSelected = true;
					if( line == oldSelectEnd )
					{
						focusedLine_ = line;
						line.Field.IsFocused = true;
					}
					Line layoutStart = line.PrevVisibleLine;
					if( layoutStart != null && layoutStart.Field.IsSelected == false )
					{
						requestLayoutLines_.Add(line);
					}
				}
				));
		}

		// 親の切り替え→削除の順で全体を実行
		foreach( Action action in reparentActions )
		{
			actionManager_.Execute(action);
		}
		foreach( Action action in deleteActions )
		{
			actionManager_.Execute(action);
		}

		// 選択解除
		actionManager_.Execute(new Action(
			execute: () =>
			{
				selectedLines_.Clear();
				selectionStartLine_ = selectionEndLine_ = null;
			},
			undo: () =>
			{
				selectedLines_ = oldSelection;
				selectionStartLine_ = oldSelectStart;
				selectionEndLine_ = oldSelectEnd;
			}
			));
		
		actionManager_.EndChain();

		return newLine;
	}

	#endregion


	#region actionManager

	void actionManager__ChainStarted(object sender, EventArgs e)
	{
		SuspendLayout();
	}

	void actionManager__ChainEnded(object sender, EventArgs e)
	{
		ResumeLayout();
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

		var updateStream = this.UpdateAsObservable();

		foreach( KeyCode key in throttleKeys )
		{
			// 最初の入力
			updateStream.Where(x => Input.GetKeyDown(key))
				.Merge(
			// 押しっぱなしにした時の自動連打
			updateStream.Where(x => Input.GetKey(key))
				.Delay(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamDelayTime))
				.ThrottleFirst(TimeSpan.FromSeconds(GameContext.Config.ArrowStreamIntervalTime))
				)
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

	protected void OnTabInput()
	{
		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			int index = line.Index;
			if( index > 0 )
			{
				Line oldParent = line.Parent;
				Line newParent = line.Parent[index - 1];
				actionManager_.Execute(new Action(
					execute: ()=>
					{
						newParent.Add(line);
						if( newParent.IsFolded )
						{
							newParent.IsFolded = false;
							newParent.AdjustLayoutRecursive();
							OnLayoutChanged();
						}
						else
						{
							line.AdjustLayout(Line.Direction.X);
						}
					},
					undo: ()=>
					{
						oldParent.Insert(index, line);
						line.AdjustLayout(Line.Direction.X);
					}
					));
			}
			else
			{
				break;
			}
		}
		actionManager_.EndChain();
	}

	protected void OnShiftTabInput()
	{
		actionManager_.StartChain();
		// 逆順で下から処理
		foreach( Line line in GetSelectedOrFocusedLines(ascending: false) )
		{
			if( line.Parent.Parent != null && ( line.Parent.Field.IsSelected == false || line.Parent.Level <= 1 ) )
			{
				int index = line.Index;
				Line oldParent = line.Parent;
				Line newParent = line.Parent.Parent;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						Line layoutStart = line.Parent[index + 1];

						newParent.Insert(line.Parent.Index + 1, line);
						line.AdjustLayout(Line.Direction.XY);

						if( layoutStart != null && layoutStart.Field.IsSelected == false )
						{
							layoutStart.Parent.AdjustLayoutRecursive(layoutStart.Index);
						}
					},
					undo: () =>
					{
						oldParent.Insert(index, line);
						line.AdjustLayout(Line.Direction.XY);
						oldParent.AdjustLayoutRecursive(index);
					}
					));
			}
		}
		actionManager_.EndChain();
	}

	protected void OnEnterInput()
	{
		int caretPos = focusedLine_.Field.CaretPosision;
		Line target = focusedLine_;
		Line parent = focusedLine_.Parent;
		int index = focusedLine_.Index;
		Line line = new Line();

		if( caretPos == 0 && target.TextLength > 0 )
		{
			// 行頭でEnterしたので行の上にLineを追加
			actionManager_.Execute(new Action(
				execute: () =>
				{
					parent.Insert(index, line);
					parent.AdjustLayoutRecursive(index);
					InstantiateLine(line);
				},
				undo: () =>
				{
					parent.Remove(line);
					parent.AdjustLayoutRecursive(index);
					target.Field.CaretPosision = caretPos;
				}
				));
		}
		else
		{
			// 行の途中（または最後）でEnterしたので行の下にLineを追加して文字を分割
			string oldString = target.Text;
			string subString = target.Text.Substring(0, caretPos);
			string newString = target.Text.Substring(caretPos, target.TextLength - caretPos);

			// 基本はすぐ下の兄弟にする
			Line insertParent = parent;
			int insertIndex = index + 1;
			// Childがいる場合はChildにする
			if( target.HasVisibleChild )
			{
				insertParent = target;
				insertIndex = 0;
			}

			actionManager_.Execute(new Action(
				execute: () =>
				{
					target.Text = subString;
					line.Text = newString;
					insertParent.Insert(insertIndex, line);
					insertParent.AdjustLayoutRecursive(insertIndex);
					InstantiateLine(line);
					line.Field.CaretPosision = 0;
					line.Field.IsFocused = true;
				},
				undo: () =>
				{
					target.Text = oldString;
					insertParent.Remove(line);
					insertParent.AdjustLayoutRecursive(insertIndex);
					target.Field.CaretPosision = caretPos;
					target.Field.IsFocused = true;
				}
				));
		}
	}

	protected void OnBackspaceInput()
	{
		if( HasSelection )
		{
			DeleteSelection();
		}
		else if( focusedLine_.Field.CaretPosision == 0 )
		{
			Line prev = focusedLine_.PrevVisibleLine;
			if( prev == null ) return;

			Line line = focusedLine_;
			if( prev.Parent == focusedLine_.Parent && prev.TextLength == 0 )
			{
				Line parent = prev.Parent;
				int index = prev.Index;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   parent.Remove(prev);
					   line.Parent.AdjustLayoutRecursive(line.Index);
				   },
				   undo: () =>
				   {
					   parent.Insert(index, prev);
					   parent.AdjustLayoutRecursive(index);
				   }
				   ));
			}
			else
			{
				actionManager_.StartChain();
				Line parent = line.Parent;
				int index = line.Index;
				string oldText = prev.Text;
				// テキスト合体してキャレット移動
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   prev.Field.CaretPosision = prev.TextLength;
					   prev.Text += line.Text;
					   prev.Field.IsFocused = true;
				   },
				   undo: () =>
				   {
					   prev.Text = oldText;
					   line.Field.CaretPosision = 0;
					   line.Field.IsFocused = true;
				   }
				   ));

				// 子供がいたら親を変更
				if( line.Count > 0 )
				{
					List<Line> children = new List<Line>(line);
					actionManager_.Execute(new Action(
						execute: () =>
						{
							line.IsFolded = false;
							for( int i = 0; i < children.Count; ++i )
							{
								prev.Insert(i, children[i]);
								children[i].AdjustLayout();
							}
							prev.IsFolded = false;
						},
						undo: () =>
						{
							for( int i = 0; i < children.Count; ++i )
							{
								line.Add(children[i]);
								children[i].AdjustLayout();
							}
							requestLayoutLines_.Add(line);
						}
						));
				}

				// 削除
				Line layoutStart = line.NextVisibleLine;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						line.Parent.Remove(line);
						if( layoutStart != null )
						{
							requestLayoutLines_.Add(layoutStart);
						}
					},
					undo: () =>
					{
						parent.Insert(index, line);
						InstantiateLine(line);
						if( layoutStart != null )
						{
							requestLayoutLines_.Add(layoutStart);
						}
					}
					));
				actionManager_.EndChain();
			}
		}
	}

	protected void OnDeleteInput()
	{
		if( HasSelection )
		{
			DeleteSelection();
		}
		else if( wasDeleteKeyConsumed_ )
		{
			wasDeleteKeyConsumed_ = false;
			return;
		}

		if( focusedLine_.Field.CaretPosision == focusedLine_.TextLength )
		{
			Line next = focusedLine_.NextVisibleLine;
			if( next == null ) return;

			if( next.Parent == focusedLine_.Parent && next.TextLength == 0 )
			{
				next.Parent.Remove(next);
				focusedLine_.Parent.AdjustLayoutRecursive(focusedLine_.Index + 1);
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
					layoutStart.Parent.AdjustLayoutRecursive(layoutStart.Index);
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
				focusedLine_.Field.IsFocused = false;
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
					focusedLine_.Field.IsFocused = false;
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
					focusedLine_.Field.IsFocused = false;
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
	
	protected void OnCtrlMLInput()
	{
		ClearSelection();

		SuspendLayout();
		if( isAllFolded_ )
		{
			Line line = rootLine_[0];
			// unfold all
			while( line != null )
			{
				if( line.Count > 0 && line.IsFolded )
				{
					line.IsFolded = false;
					line.AdjustLayout(Line.Direction.XY);
					line.AdjustLayoutRecursive(0, (Line l) => l.Count > 0 && l.IsFolded);
				}
				line = line.NextVisibleLine;
			}
			
			isAllFolded_ = false;
		}
		else
		{
			Line layoutStart = null;
			Line line = rootLine_.LastVisibleLine;
			// fold all
			while( line != null )
			{
				if( line.Count > 0 && line.IsFolded == false )
				{
					line.IsFolded = true;
					foreach( Line child in line )
					{
						child.AdjustLayout();
					}
					layoutStart = line;
				}
				line = line.PrevVisibleLine;
			}

			isAllFolded_ = true;

			if( focusedLine_ != null )
			{
				while( focusedLine_.Parent.IsFolded )
				{
					focusedLine_ = focusedLine_.Parent;
				}
				focusedLine_.Field.IsFocused = true;
			}

			if( layoutStart != null )
			{
				layoutStart.AdjustLayoutRecursive();
			}
		}
		ResumeLayout();
		OnLayoutChanged();
	}

	#endregion


	#region clipboard

	public static string Clipboard
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
	public static string[] LineSeparator = new string[] { System.Environment.NewLine };
	public static string TabString = "	";

	protected void Copy()
	{
		if( HasSelection )
		{
			string clipboardLines = "";
			foreach( Line line in selectedLines_.Values )
			{
				int level = line.Level - 1;
				for( int i = 0; i < level; ++i )
				{
					clipboardLines += TabString;
				}
				clipboardLines += line.Text + System.Environment.NewLine;

				if( line.IsFolded )
				{
					foreach( Line child in line.GetAllChildren() )
					{
						level = child.Level - 1;
						for( int i = 0; i < level; ++i )
						{
							clipboardLines += TabString;
						}
						clipboardLines += child.Text + System.Environment.NewLine;
					}
				}
			}
			Clipboard = clipboardLines.Remove(clipboardLines.Length - 1);
		}
	}

	protected void Paste()
	{
		if( focusedLine_ == null )
			return;

		Line pasteStart = focusedLine_;
		if( HasSelection )
		{
			pasteStart = DeleteSelection();
		}

		string[] cilpboardLines = Clipboard.Split(LineSeparator, System.StringSplitOptions.None);

		string text = cilpboardLines[0];
		int currentLevel = 0;
		int oldLevel = 0;
		while( text.StartsWith(TabString) )
		{
			++currentLevel;
			text = text.Remove(0, 1);
		}
		pasteStart.Field.Paste(text);
		oldLevel = currentLevel;

		SuspendLayout();
		Line parent = pasteStart.Parent;
		Line brother = pasteStart;
		Line layoutStart = pasteStart.NextVisibleLine;
		for( int i = 1; i < cilpboardLines.Length; ++i )
		{
			text = cilpboardLines[i];
			currentLevel = 0;
			while( text.StartsWith(TabString) )
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
				parent.Insert(brother.Index + 1, line);
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;

					brother = parent;
					parent = parent.Parent;
				}
				parent.Insert(brother.Index + 1, line);
			}
			InstantiateLine(line);
			brother = line;
			oldLevel = currentLevel;
		}

		if( layoutStart != null )
		{
			layoutStart.Parent.AdjustLayoutRecursive(layoutStart.Index);
		}
		ResumeLayout();
		OnLayoutChanged();
	}

	protected void Cut()
	{
		if( HasSelection )
		{
			Copy();
			DeleteSelection();
		}
	}

	#endregion


	#region utils

	protected void InstantiateLine(Line line)
	{
		line.Bind(Instantiate(FieldPrefab.gameObject));
		fields_.Add(line.Field);
		OnLayoutChanged();
	}

	protected IEnumerable<Line> GetSelectedOrFocusedLines(bool ascending = true)
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

	#endregion


	#region events

	public void OnFocused(Line line)
	{
		focusedLine_ = line;
		selectionStartLine_ = line;

		// update scroll
		float scrollHeight = scrollRect_.GetComponent<RectTransform>().sizeDelta.y;
		float focusHeight = -(focusedLine_.Field.transform.position.y - this.transform.position.y);

		// focusLineが下側に出て見えなくなった場合
		float focusUnderHeight = -(focusedLine_.Field.transform.position.y - scrollRect_.transform.position.y) - scrollHeight;
		if( focusUnderHeight > 0 )
		{
			scrollRect_.verticalScrollbar.value = 1.0f - (focusHeight + GameContext.Config.HeightPerLine - scrollHeight) / (layout_.preferredHeight - scrollHeight);
		}

		// focusLineが上側に出て見えなくなった場合
		float focusOverHeight = (focusedLine_.Field.transform.position.y + GameContext.Config.HeightPerLine - scrollRect_.transform.position.y);
		if( focusOverHeight > 0 )
		{
			scrollRect_.verticalScrollbar.value = (layout_.preferredHeight - scrollHeight - focusHeight + GameContext.Config.HeightPerLine) / (layout_.preferredHeight - scrollHeight);
		}
	}

	public void OnDeleteKeyConsumed()
	{
		wasDeleteKeyConsumed_ = true;
	}

	public void OnTextFieldDestroy(TextField field)
	{
		fields_.Remove(field);
		OnLayoutChanged();
	}
	
	public void OnLayoutChanged()
	{
		if( suspendLayoutCount_ <= 0 )
		{
			Line lastLine = rootLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				layout_.preferredHeight = -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 1.5f;
			}
		}
	}

	public void SuspendLayout()
	{
		++suspendLayoutCount_;
	}

	public void ResumeLayout()
	{
		--suspendLayoutCount_;
		if( suspendLayoutCount_ <= 0 )
		{
			suspendLayoutCount_ = 0;
			if( requestLayoutLines_.Count > 0 )
			{
				foreach( Line line in requestLayoutLines_ )
				{
					line.Parent.AdjustLayoutRecursive(line.Index);
				}
				requestLayoutLines_.Clear();
				OnLayoutChanged();
			}
		}
	}

	#endregion
}
