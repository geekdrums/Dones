using System;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UniRx;
using UniRx.Triggers;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Window - [ Tree ] - Line
public class Tree : MonoBehaviour {

	#region editor params

	public TextField FieldPrefab;
	public int FieldCount = 100;
	
	#endregion


	#region params

	List<TextField> usingFields_ = new List<TextField>();
	GameObject heapParent_;
	Line rootLine_;
	Line focusedLine_;
	Line selectionStartLine_, selectionEndLine_;
	SortedList<int, Line> selectedLines_ = new SortedList<int, Line>();
	ActionManager actionManager_ = new ActionManager();

	// input states
	bool wasDeleteKeyConsumed_ = false;
	bool wasCtrlMInput_ = false;

	// layout
	bool isAllFolded_ = false;
	List<Line> requestLayoutLines_ = new List<Line>();
	int suspendLayoutCount_ = 0;
	float targetScrollValue_ = 1.0f;
	bool isScrollAnimating_;

	// components
	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;
	ScrollRect scrollRect_;
	TabButton tabButton_;

	// file
	FileInfo file_ = null;
	bool isEdited_ = false;

	// properties
	public ActionManager ActionManager { get { return actionManager_; } }
	public FileInfo File { get { return file_; } }
	public TabButton Tab { get { return tabButton_; } }
	public Line FocusedLine { get { return focusedLine_; } }

	public string TitleText { get { return rootLine_ != null ? rootLine_.Text : ""; } }
	public bool IsActive { get { return (tabButton_ != null ? tabButton_.IsOn : false); } set { if( tabButton_ != null ) tabButton_.IsOn = value; } }


	// utils
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


	#region unity functions

	// Use this for initialization
	void Awake () {
		actionManager_.ChainStarted += this.actionManager__ChainStarted;
		actionManager_.ChainEnded += this.actionManager__ChainEnded;
		actionManager_.Executed += this.actionManager__Executed;

		heapParent_ = new GameObject("heap");
		heapParent_.transform.parent = this.transform;
		for( int i = 0; i < FieldCount; ++i )
		{
			TextField field = Instantiate(FieldPrefab.gameObject).GetComponent<TextField>();
			field.transform.SetParent(heapParent_.transform);
		}
		heapParent_.SetActive(false);
	}
	
	// Update is called once per frame
	void Update()
	{
		if( rootLine_ == null ) return;

		if( isScrollAnimating_ )
		{
			scrollRect_.verticalScrollbar.value = Mathf.Lerp(scrollRect_.verticalScrollbar.value, targetScrollValue_, 0.2f);
			if( Mathf.Abs(scrollRect_.verticalScrollbar.value - targetScrollValue_) < 0.01f )
			{
				scrollRect_.verticalScrollbar.value = targetScrollValue_;
				isScrollAnimating_ = false;
			}
		}

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
#if UNITY_EDITOR
			else if( Input.GetKeyDown(KeyCode.W) )
#else
			else if( Input.GetKeyDown(KeyCode.S) )
#endif
			{
				Save();
			}
			else if( Input.GetKeyDown(KeyCode.Space) )
			{
				OnCtrlSpaceInput();
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
				if( focusedLine_ != null && focusedLine_.Field.IsFocused )
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
				UpdateSelection(selectionEndLine_, true);
			}
		}
		else if( Input.GetMouseButtonUp(0) && HasSelection == false )
		{
			selectionStartLine_ = selectionEndLine_ = null;
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

	public void ClearSelection(bool clearStartAndEnd = true)
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
							}
						},
						undo: () =>
						{
							for( int i = 0; i < lostChildren.Count; ++i )
							{
								lostParent.Add(lostChildren[i]);
							}
							RequestLayout(lostParent);
							RequestLayout(prev.NextVisibleLine);
						}
						));
				}
			}

			Line parent = line.Parent;
			int index = line.Index;
			Line layoutStart = line.NextVisibleLine;
			deleteActions.Add(new Action(
				execute: () =>
				{
					line.Parent.Remove(line);
					if( layoutStart != null && layoutStart.Field.IsSelected == false )
					{
						RequestLayout(layoutStart);
					}
				},
				undo: () =>
				{
					parent.Insert(index, line);
					line.Field.IsSelected = true;
					if( line == oldSelectEnd )
					{
						line.Field.IsFocused = true;
						OnFocused(line);
					}
					if( layoutStart != null && layoutStart.Field.IsSelected == false )
					{
						RequestLayout(layoutStart);
					}
				}
				));
		}
		
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
				selectedLines_ = new SortedList<int, Line>(oldSelection);
				selectionStartLine_ = oldSelectStart;
				selectionEndLine_ = oldSelectEnd;
			}
			));

		// （入力のために）新しい行を作る
		Line newLine = new Line();
		actionManager_.Execute(new Action(
			execute: () =>
			{
				oldParent.Insert(oldIndex, newLine);
				newLine.Field.IsFocused = true;
				OnFocused(newLine);
			},
			undo: () =>
			{
				oldParent.Remove(newLine);
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

	void actionManager__Executed(object sender, ActionEventArgs e)
	{
		if( focusedLine_ != null && e.Action is Line.TextAction == false )
		{
			focusedLine_.FixTextInputAction();
		}

		if( suspendLayoutCount_ <= 0 )
		{
			AdjustRequestedLayouts();
		}

		if( isEdited_ == false )
		{
			isEdited_ = true;
			tabButton_.Text = TitleText + "*";
		}
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
				.RepeatUntilDisable(this)
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
			if( index > 0 && (line.Parent == rootLine_ || line.Parent.Field.IsSelected == false) )
			{
				Line oldParent = line.Parent;
				Line newParent = line.Parent[index - 1];
				actionManager_.Execute(new Action(
					execute: () =>
					{
						newParent.Add(line);
						if( newParent.IsFolded )
						{
							newParent.IsFolded = false;
							newParent.AdjustLayoutRecursive();
						}
					},
					undo: () =>
					{
						oldParent.Insert(index, line);
					}
					));
			}
			//else
			//{
			//	break;
			//}
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
						if( layoutStart != null && layoutStart.Field.IsSelected == false )
						{
							layoutStart.Parent.AdjustLayoutRecursive(layoutStart.Index);
						}
					},
					undo: () =>
					{
						oldParent.Insert(index, line);
						oldParent.AdjustLayoutRecursive(index);
					}
					));
			}
		}
		actionManager_.EndChain();
	}

	protected void OnCtrlSpaceInput()
	{
		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			if( line.Text != "" )
			{
				Line targetLine = line;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						targetLine.IsDone = !targetLine.IsDone;
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
					parent.AdjustLayoutRecursive(index + 1);
					UpdateScrollTo(target);
				},
				undo: () =>
				{
					parent.Remove(line);
					parent.AdjustLayoutRecursive(index);
					target.Field.CaretPosision = caretPos;
					UpdateScrollTo(target);
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
					insertParent.AdjustLayoutRecursive(insertIndex + 1);
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
			Line line = focusedLine_;
			Line prev = line.PrevVisibleLine;
			if( prev == null ) return;

			if( prev.Parent == line.Parent && prev.TextLength == 0 )
			{
				Line parent = prev.Parent;
				int index = prev.Index;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   parent.Remove(prev);
					   RequestLayout(line);
				   },
				   undo: () =>
				   {
					   parent.Insert(index, prev);
					   RequestLayout(line);
				   }
				   ));
			}
			else if( prev.Parent == line.Parent && line.TextLength == 0 )
			{
				Line parent = line.Parent;
				int index = line.Index;
				Line layoutStart = line.NextVisibleLine;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   parent.Remove(line);
					   RequestLayout(layoutStart);
					   prev.Field.CaretPosision = prev.TextLength;
					   prev.Field.IsFocused = true;
				   },
				   undo: () =>
				   {
					   parent.Insert(index, line);
					   RequestLayout(layoutStart);
					   line.Field.CaretPosision = 0;
					   line.Field.IsFocused = true;
				   }
				   ));
			}
			else
			{
				actionManager_.StartChain();
				Line parent = line.Parent;
				int index = line.Index;

				// テキスト合体してキャレット移動
				string oldText = prev.Text;
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
							prev.IsFolded = false;
							for( int i = 0; i < children.Count; ++i )
							{
								prev.Insert(i, children[i]);
							}
						},
						undo: () =>
						{
							for( int i = 0; i < children.Count; ++i )
							{
								line.Add(children[i]);
							}
							RequestLayout(line);
						}
						));
				}

				// 削除
				Line layoutStart = line.NextVisibleLine;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						line.Parent.Remove(line);
						RequestLayout(layoutStart);
					},
					undo: () =>
					{
						parent.Insert(index, line);
						RequestLayout(layoutStart);
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
			Line line = focusedLine_;
			Line next = line.NextVisibleLine;
			if( next == null ) return;

			if( next.Parent == line.Parent && next.TextLength == 0 )
			{
				Line parent = next.Parent;
				int index = next.Index;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   parent.Remove(next);
					   parent.AdjustLayoutRecursive(index);
				   },
				   undo: () =>
				   {
					   parent.Insert(index, next);
					   parent.AdjustLayoutRecursive(index + 1);
				   }
				   ));
			}
			else if( next.Parent == line.Parent && line.TextLength == 0 )
			{
				Line parent = line.Parent;
				int index = line.Index;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   parent.Remove(line);
					   RequestLayout(next);
					   next.Field.IsFocused = true;
				   },
				   undo: () =>
				   {
					   parent.Insert(index, line);
					   RequestLayout(next);
					   line.Field.IsFocused = true;
				   }
				   ));
			}
			else
			{
				actionManager_.StartChain();
				Line parent = next.Parent;
				int index = next.Index;

				// テキスト合体
				string oldText = line.Text;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   line.Text += next.Text;
				   },
				   undo: () =>
				   {
					   line.Text = oldText;
				   }
				   ));

				// 子供がいたら親を変更
				if( next.Count > 0 )
				{
					List<Line> children = new List<Line>(next);
					actionManager_.Execute(new Action(
						execute: () =>
						{
							next.IsFolded = false;
							line.IsFolded = false;
							for( int i = 0; i < children.Count; ++i )
							{
								line.Insert(i, children[i]);
							}
						},
						undo: () =>
						{
							for( int i = 0; i < children.Count; ++i )
							{
								next.Add(children[i]);
							}
							RequestLayout(line);
						}
						));
				}

				Line layoutStart = next.NextVisibleLine;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						parent.Remove(next);
						RequestLayout(layoutStart);
					},
					undo: () =>
					{
						parent.Insert(index, next);
						RequestLayout(layoutStart);
					}
					));
				actionManager_.EndChain();
			}
		}
	}

	protected void OnArrowInput(KeyCode key)
	{
		// 選択があれば解除
		if( HasSelection )
		{
			selectionEndLine_.Field.IsFocused = true;
			OnFocused(selectionEndLine_);
			ClearSelection();
		}

		switch( key )
		{
		case KeyCode.DownArrow:
		case KeyCode.UpArrow:
			if( Input.GetKey(KeyCode.LeftAlt) )
			{
				// 上下の兄弟と交換
				Line src = focusedLine_;
				Line dest = (key == KeyCode.DownArrow ? src.NextSiblingLine : src.PrevSiblingLine);
				if( dest != null )
				{
					actionManager_.Execute(new Action(
						execute: () =>
						{
							src.Parent.Insert(dest.Index, src);
							dest.AdjustLayout();
							src.Field.IsFocused = true;
						}
						));
				}
			}
			else
			{
				// フォーカスを上下に移動
				Line line = (key == KeyCode.DownArrow ? focusedLine_.NextVisibleLine : focusedLine_.PrevVisibleLine);
				if( line != null )
				{
					focusedLine_.Field.IsFocused = false;
					line.Field.IsFocused = true;
				}
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
			selectionStartLine_ = focusedLine_;
			selectionEndLine_ = focusedLine_;
		}

		switch( key )
		{
		case KeyCode.DownArrow:
		case KeyCode.UpArrow:
			// 選択行を上下に追加または削除
			int sign = key == KeyCode.DownArrow ? 1 : -1;
			Line line = (sign > 0 ? selectionEndLine_.NextVisibleLine : selectionEndLine_.PrevVisibleLine);
			if( line != null && HasSelection )
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

		actionManager_.StartChain();
		if( isAllFolded_ )
		{
			// unfold all
			Line line = rootLine_[0];
			Line layoutStart = null;
			while( line != null )
			{
				if( line.Count > 0 && line.IsFolded )
				{
					if( layoutStart == null )
						layoutStart = line.NextVisibleLine;

					Line unfoldLine = line;
					actionManager_.Execute(new Action(
						execute: () =>
						{
							unfoldLine.IsFolded = false;
							unfoldLine.AdjustLayout();
							unfoldLine.AdjustLayoutRecursive(0, (Line l) => l.Count > 0 && l.IsFolded);
						},
						undo: () =>
						{
							unfoldLine.IsFolded = true;
							unfoldLine.AdjustLayoutInChildren();
						}
						));
				}
				line = line.NextVisibleLine;
			}

			actionManager_.Execute(new Action(
				execute: () =>
				{
					isAllFolded_ = false;
				},
				undo: () =>
				{
					isAllFolded_ = true;
					if( layoutStart != null )
					{
						RequestLayout(layoutStart);
					}
				}
				));
		}
		else
		{
			// fold all
			Line addLine = rootLine_.LastVisibleLine;
			List<Line> foldLines = new List<Line>();
			while( addLine != null )
			{
				if( addLine.Count > 0 && addLine.IsFolded == false )
				{
					foldLines.Add(addLine);
				}
				addLine = addLine.PrevVisibleLine;
			}

			foreach(Line line in foldLines)
			{
				Line foldLine = line;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						foldLine.IsFolded = true;
						foldLine.AdjustLayoutInChildren();
					},
					undo: () =>
					{
						foldLine.IsFolded = false;
						foldLine.AdjustLayout();
						foldLine.AdjustLayoutRecursive(0, (Line l) => l.Count > 0 && foldLines.Contains(l));
					}
					));
			}

			Line foldStart = (foldLines.Count > 0 ? foldLines[foldLines.Count - 1] : null);
			actionManager_.Execute(new Action(
				execute: () =>
				{
					isAllFolded_ = true;
					if( foldStart != null )
					{
						RequestLayout(foldStart.NextVisibleLine);
					}
				},
				undo: () =>
				{
					isAllFolded_ = false;
				}
				));

			if( focusedLine_ != null )
			{
				Line newFocusLine = focusedLine_;
				while( newFocusLine.Parent.IsFolded )
				{
					newFocusLine = newFocusLine.Parent;
				}
				newFocusLine.Field.IsFocused = true;
				OnFocused(newFocusLine);
			}
		}
		actionManager_.EndChain();
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

	protected void Copy()
	{
		if( HasSelection )
		{
			StringBuilder clipboardLines = new StringBuilder();
			foreach( Line line in selectedLines_.Values )
			{
				line.AppendStringTo(clipboardLines, appendTag: true);
				if( line.IsFolded )
				{
					foreach( Line child in line.GetAllChildren() )
					{
						child.AppendStringTo(clipboardLines, appendTag: true);
					}
				}
			}
			Clipboard = clipboardLines.ToString();
		}
	}

	protected void Paste()
	{
		if( focusedLine_ == null )
			return;

		actionManager_.StartChain();

		// delete
		Line pasteStart = null;
		if( HasSelection )
		{
			pasteStart = DeleteSelection();
		}

		string[] cilpboardLines = Clipboard.Split(LineSeparator, System.StringSplitOptions.None);
		string pasteText = cilpboardLines[0];
		int currentLevel = 0;
		while( pasteText.StartsWith(Line.TabString) )
		{
			++currentLevel;
			pasteText = pasteText.Remove(0, 1);
		}
		
		if( cilpboardLines.Length == 1 )
		{
			// paste for current focused line
			if( pasteStart == null )
			{
				pasteStart = focusedLine_;
			}

			string oldText = pasteStart.Text;
			int oldCaretPos = pasteStart.Field.CaretPosision;
			actionManager_.Execute(new Action(
				execute: () =>
				{
					pasteStart.Field.Paste(pasteText);
				},
				undo: () =>
				{
					pasteStart.Field.CaretPosision = oldCaretPos;
					pasteStart.Text = oldText;
				}
				));
		}
		else
		{
			// paste mutiple lines.
			if( focusedLine_.Text == "" )
			{
				pasteStart = focusedLine_;
			}
			else if( pasteStart == null )
			{
				Line pasteParent = focusedLine_.Parent;
				int pasteIndex = focusedLine_.Index + 1;
				pasteStart = new Line();
				actionManager_.Execute(new Action(
					execute: () =>
					{
						pasteParent.Insert(pasteIndex, pasteStart);
					},
					undo: () =>
					{
						pasteParent.Remove(pasteStart);
					}
					));
			}
			
			Line layoutStart = pasteStart.NextVisibleLine;
			actionManager_.Execute(new Action(
				execute: () =>
				{
					pasteStart.LoadTag(ref pasteText);
					pasteStart.Text = pasteText;
					RequestLayout(layoutStart);
				},
				undo: () =>
				{
					pasteStart.Field.CaretPosision = 0;
					pasteStart.Text = "";
					pasteStart.IsDone = false;
					RequestLayout(layoutStart);
				}
				));
		}

		int oldLevel = currentLevel;
		Line parent = pasteStart.Parent;
		Line brother = pasteStart;
		for( int i = 1; i < cilpboardLines.Length; ++i )
		{
			string text = cilpboardLines[i];
			currentLevel = 0;
			while( text.StartsWith(Line.TabString) )
			{
				++currentLevel;
				text = text.Remove(0, 1);
			}

			Line line = new Line(text, loadTag: true);

			Line insertParent;
			int insertIndex;

			if( currentLevel > oldLevel )
			{
				insertParent = brother;
				insertIndex = brother.Count;
				parent = brother;
			}
			else if( currentLevel == oldLevel )
			{
				insertParent = parent;
				insertIndex = brother.Index + 1;
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;

					brother = parent;
					parent = parent.Parent;
				}
				insertParent = parent;
				insertIndex = brother.Index + 1;
			}

			actionManager_.Execute(new Action(
				execute: () =>
				{
					insertParent.Insert(insertIndex, line);
				},
				undo: () =>
				{
					insertParent.Remove(line);
				}
				));

			brother = line;
			oldLevel = currentLevel;
		}


		actionManager_.EndChain();
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


	#region events
	
	public void Bind(Line line)
	{
		TextField field = heapParent_.GetComponentInChildren<TextField>();
		if( field == null )
		{
			for( int i = 0; i < FieldCount; ++i )
			{
				field = Instantiate(FieldPrefab.gameObject).GetComponent<TextField>();
				field.transform.SetParent(heapParent_.transform);
			}
		}
		if( field.BindedLine != null && field.BindedLine != line )
		{
			field.BindedLine.UnBind();
		}
		field.gameObject.SetActive(true);
		line.Bind(field.gameObject);
		usingFields_.Add(field);
	}

	public void OnReBind(Line line)
	{
		TextField field = line.Field;
		if( field != null && field.gameObject.activeInHierarchy )
		{
			usingFields_.Add(field);
		}
	}

	public void OnRemove(Line line)
	{
		TextField field = line.Field;
		if( field != null && field.gameObject.activeInHierarchy )
		{
			usingFields_.Remove(field);
			field.transform.SetParent(heapParent_.transform);
			field.gameObject.SetActive(false);
		}
	}

	public void OnFocused(Line line)
	{
		if( focusedLine_ == line )
		{
			return;
		}

		if( focusedLine_ != null )
		{
			focusedLine_.FixTextInputAction();
		}

		focusedLine_ = line;
		selectionStartLine_ = line;

		UpdateScrollTo(focusedLine_);
	}

	public void OnFocusEnded(Line line)
	{
		if( line == focusedLine_ )
		{
			focusedLine_.FixTextInputAction();
			focusedLine_ = null;
		}
	}

	public void OnFoldUpdated(Line line, bool isFolded)
	{
		if( line.IsFolded != isFolded )
		{
			actionManager_.Execute(new Action(
				execute: () =>
				{
					line.IsFolded = isFolded;
					line.AdjustLayoutRecursive();
					line.Field.IsFocused = true;
				},
				undo: () =>
				{
					line.IsFolded = !isFolded;
					line.AdjustLayoutRecursive();
					line.Field.IsFocused = true;
				}
				));
		}
	}

	public void OnDeleteKeyConsumed()
	{
		wasDeleteKeyConsumed_ = true;
	}

	public void OnTextFieldDestroy(TextField field)
	{
		usingFields_.Remove(field);
	}

	public void OnActivated()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>();

		GameContext.CurrentActionManager = actionManager_;

		UpdateLayoutElement();
		scrollRect_.verticalScrollbar.value = targetScrollValue_;

		if( focusedLine_ == null )
		{
			focusedLine_ = rootLine_[0];
		}
		focusedLine_.Field.IsFocused = true;

		SubscribeKeyInput();

#if UNITY_STANDALONE_WIN
		GameContext.Window.SetTitle(TitleText + " - Dones");
#endif
	}

	public void OnDeactivated()
	{
		if( scrollRect_ != null )
		{
			targetScrollValue_ = scrollRect_.verticalScrollbar.value;
		}

		layout_ = null;
		contentSizeFitter_ = null;
		scrollRect_ = null;
	}

	#endregion


	#region layout

	protected void UpdateLayoutElement()
	{
		if( suspendLayoutCount_ <= 0 && layout_ != null )
		{
			Line lastLine = rootLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				layout_.preferredHeight = -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 1.0f;
				contentSizeFitter_.SetLayoutVertical();
			}
		}
	}

	protected void UpdateScrollTo(Line targetLine)
	{
		float scrollHeight = scrollRect_.GetComponent<RectTransform>().rect.height;
		float targetHeight = -(targetLine.Field.transform.position.y - this.transform.position.y);
		float heightPerLine = GameContext.Config.HeightPerLine;

		// focusLineが下側に出て見えなくなった場合
		float targetUnderHeight = -(targetLine.Field.transform.position.y - scrollRect_.transform.position.y) + heightPerLine / 2 - scrollHeight;
		if( targetUnderHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01(1.0f - (targetHeight + heightPerLine * 1.5f - scrollHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}

		// focusLineが上側に出て見えなくなった場合
		float targetOverHeight = (targetLine.Field.transform.position.y + heightPerLine - scrollRect_.transform.position.y);
		if( targetOverHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01((layout_.preferredHeight - scrollHeight - targetHeight + heightPerLine) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}
	}

	protected void SuspendLayout()
	{
		++suspendLayoutCount_;
	}

	protected void ResumeLayout()
	{
		--suspendLayoutCount_;
		if( suspendLayoutCount_ <= 0 )
		{
			suspendLayoutCount_ = 0;
			AdjustRequestedLayouts();
		}
	}

	protected void RequestLayout(Line line)
	{
		if( line == null || line.Parent == null ) return;
		
		if( suspendLayoutCount_ > 0 )
		{
			if( requestLayoutLines_.Contains(line) == false )
			{
				requestLayoutLines_.Add(line);
			}
		}
		else
		{
			line.Parent.AdjustLayoutRecursive(line.Index);
		}
	}

	protected void AdjustRequestedLayouts()
	{
		if( requestLayoutLines_.Count > 0 )
		{
			foreach( Line line in requestLayoutLines_ )
			{
				if( line.Parent != null )
				{
					line.Parent.AdjustLayoutRecursive(line.Index);
				}
			}
			requestLayoutLines_.Clear();
		}
		UpdateLayoutElement();
	}

	#endregion
	

	#region file
	
	public void NewFile(TabButton tab)
	{
		tabButton_ = tab;

		SuspendLayout();
		rootLine_ = new Line("new");
		rootLine_.Bind(this.gameObject);
		rootLine_.Add(new Line(""));
		ResumeLayout();

		tabButton_.BindedTree = this;
		tabButton_.Text = TitleText;
	}

	public void Load(string path, TabButton tab)
	{
		if( file_ != null )
		{
			return;
		}

		tabButton_ = tab;

		file_ = new FileInfo(path);

		SuspendLayout();
		rootLine_ = new Line(file_.Name);
		rootLine_.Bind(this.gameObject);

		StreamReader reader = new StreamReader(file_.OpenRead());
		Line parent = rootLine_;
		Line brother = null;
		string text = null;
		int currentLevel, oldLevel = 0;
		while( (text = reader.ReadLine()) != null )
		{
			currentLevel = 0;
			while( text.StartsWith(Line.TabString) )
			{
				++currentLevel;
				text = text.Remove(0, 1);
			}

			Line line = new Line(text, loadTag: true);

			Line addParent;

			if( currentLevel > oldLevel )
			{
				addParent = brother;
				parent = brother;
			}
			else if( currentLevel == oldLevel )
			{
				addParent = parent;
			}
			else// currentLevel < oldLevel 
			{
				for( int level = oldLevel; level > currentLevel; --level )
				{
					if( parent.Parent == null ) break;

					brother = parent;
					parent = parent.Parent;
				}
				addParent = parent;
			}

			addParent.Add(line);

			brother = line;
			oldLevel = currentLevel;
		}
		if( rootLine_.Count == 0 )
		{
			rootLine_.Add(new Line(""));
		}
		reader.Close();
		ResumeLayout();

		tabButton_.BindedTree = this;
		tabButton_.Text = TitleText;
	}

	public void Save(bool saveAs = false)
	{
		if( file_ == null || saveAs )
		{
			SaveFileDialog saveFileDialog = new SaveFileDialog();
			saveFileDialog.Filter = "dones file (*.dtml)|*.dtml";
			saveFileDialog.FileName = rootLine_.Text;
			DialogResult dialogResult = saveFileDialog.ShowDialog();
			if( dialogResult == DialogResult.OK )
			{
				file_ = new FileInfo(saveFileDialog.FileName);
				rootLine_.Text = file_.Name;
#if UNITY_STANDALONE_WIN
				GameContext.Window.SetTitle(TitleText + " - Dones");
#endif
			}
			else
			{
				return;
			}
		}

		StringBuilder builder = new StringBuilder();
		foreach( Line line in rootLine_.GetAllChildren() )
		{
			line.AppendStringTo(builder, appendTag: true);
		}

		StreamWriter writer = new StreamWriter(file_.FullName, append: false);
		writer.Write(builder.ToString());
		writer.Flush();
		writer.Close();

		isEdited_ = false;
		tabButton_.Text = TitleText;
	}

	#endregion
}
