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

// Tree structure of Lines.
public class Tree : MonoBehaviour {

	#region editor params

	public TextField FieldPrefab;
	public int FieldCount = 100;

	#endregion


	#region params
	
	protected List<TextField> heapFields_ = new List<TextField>();
	protected Line rootLine_;
	protected Line focusedLine_;
	protected Line selectionStartLine_, selectionEndLine_;
	protected SortedList<int, Line> selectedLines_ = new SortedList<int, Line>();
	protected ActionManager actionManager_;

	// input states
	protected bool wasDeleteKeyConsumed_ = false;
	protected bool wasCtrlMInput_ = false;
	protected List<IDisposable> throttleInputSubscriptions_ = new List<IDisposable>();

	// layout
	protected bool isAllFolded_ = false;
	protected List<Line> requestLayoutLines_ = new List<Line>();
	protected int suspendLayoutCount_ = 0;

	// components
	protected LayoutElement layout_;
	protected ContentSizeFitter contentSizeFitter_;


	// file
	protected FileInfo file_ = null;
	protected bool isEdited_ = false;

	// properties
	public ActionManager ActionManager { get { return actionManager_; } }
	public FileInfo File { get { return file_; } }
	public Line FocusedLine { get { return focusedLine_; } }
	public Line RootLine { get { return rootLine_; } }

	public string TitleText { get { return rootLine_ != null ? rootLine_.Text : ""; } }
	public override string ToString() { return TitleText; }

	public virtual bool IsEdited
	{
		get { return isEdited_; }
		set
		{
			if( value && GameContext.Config.IsAutoSave )
			{
				Save();
				return;
			}
			isEdited_ = value;
		}
	}

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


	#region unity events

	protected virtual void Awake()
	{
		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
	}

	// Update is called once per frame
	protected virtual void Update()
	{
		if( rootLine_ == null ) return;

		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;


		// keyboard input
		if( ctrl )
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
				if( shift )
				{
					GameContext.Window.SaveAll();
				}
				else
				{
					Save();
				}
			}
			else if( Input.GetKeyDown(KeyCode.Space) )
			{
				OnCtrlSpaceInput();
			}
			else if( Input.GetKeyDown(KeyCode.D) )
			{
				OnCtrlDInput();
			}
			//ArialのBoldが残念すぎるので、フォント改善するまで封印
//#if UNITY_EDITOR
//			else if( Input.GetKeyDown(KeyCode.G) )
//#else
//			else if( Input.GetKeyDown(KeyCode.B) )
//#endif
//			{
//				OnCtrlBInput();
//			}
			else if( Input.GetKeyDown(KeyCode.L) && wasCtrlMInput_ == false )
			{
				OnCtrlLInput();
			}
			else if( Input.GetKeyDown(KeyCode.Home) )
			{
				rootLine_[0].Field.IsFocused = true;
			}
			else if( Input.GetKeyDown(KeyCode.End) )
			{
				rootLine_.LastVisibleLine.Field.IsFocused = true;
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
				TextField field = null;
				if( EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null )
				{
					field = EventSystem.current.currentSelectedGameObject.GetComponent<TextField>();
				}
				if( field != null )
				{
					line = field.BindedLine;
				}
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
				if( focusedLine_ != null && focusedLine_.Field.IsFocused && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
				{
					selectionStartLine_ = focusedLine_;
				}
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

			rect = selectionStartLine_.Field.Rect;
			if( selectedLines_.Count == 1 && rect.Contains(Input.mousePosition) && Input.mousePosition.x - rect.x < selectionStartLine_.Field.GetTextRectLength() )
			{
				UpdateSelection(selectionStartLine_, false);
			}
			else if( selectedLines_.Count == 0 && rect.Contains(Input.mousePosition)
				&& selectionStartLine_.Field.selectionFocusPosition != selectionStartLine_.Field.selectionAnchorPosition
				&& Input.mousePosition.x - rect.x > selectionStartLine_.Field.GetTextRectLength() )
			{
				UpdateSelection(selectionStartLine_, true);
			}
		}
		else if( Input.GetMouseButtonUp(0) && HasSelection == false )
		{
			selectionStartLine_ = selectionEndLine_ = null;
		}
	}

	protected virtual void OnDisable()
	{
		foreach( IDisposable subscription in throttleInputSubscriptions_ )
		{
			subscription.Dispose();
		}
		throttleInputSubscriptions_.Clear();
	}

	protected virtual void OnDestroy()
	{
		actionManager_.ChainStarted -= this.actionManager__ChainStarted;
		actionManager_.ChainEnded -= this.actionManager__ChainEnded;
		actionManager_.Executed -= this.actionManager__Executed;
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
		if( focusedLine_ == null ) return;

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
					while( prev != null && prev.Field.IsSelected )
					{
						// 選択中のやつは消されるので、消されないものの中で一番近いものを選ぶ
						prev = prev.PrevVisibleLine;
					}
					if( prev == null ) prev = rootLine_;
					Line lostParent = line;
					bool wasFolded = prev.IsFolded;
					reparentActions.Add(new Action(
						execute: () =>
						{
							int startIndex = prev.Count;
							if( wasFolded )
							{
								prev.IsFolded = false;
							}
							for( int i = startIndex; i < startIndex + lostChildren.Count; ++i )
							{
								prev.Insert(i, lostChildren[i - startIndex]);
							}
							RequestLayout(prev[0]);
						},
						undo: () =>
						{
							for( int i = 0; i < lostChildren.Count; ++i )
							{
								lostParent.Add(lostChildren[i]);
							}
							if( wasFolded )
							{
								prev.IsFolded = true;
							}
							RequestLayout(prev[0]);
							RequestLayout(lostParent);
							RequestLayout(prev.NextVisibleLine);
						}
						));
				}
			}

			Line parent = line.Parent;
			int index = line.Index;
			Line layoutStart = null;
			deleteActions.Add(new Action(
				execute: () =>
				{
					layoutStart = line.NextVisibleLine;
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
				RequestLayout(newLine.NextVisibleLine);
				newLine.Field.IsFocused = true;
				OnFocused(newLine);
			},
			undo: () =>
			{
				RequestLayout(newLine.NextVisibleLine);
				oldParent.Remove(newLine);
			}
			));

		actionManager_.EndChain();

		return newLine;
	}

	#endregion


	#region actionManager

	protected void actionManager__ChainStarted(object sender, EventArgs e)
	{
		SuspendLayout();
	}

	protected void actionManager__ChainEnded(object sender, EventArgs e)
	{
		ResumeLayout();
	}

	protected void actionManager__Executed(object sender, ActionEventArgs e)
	{
		if( focusedLine_ != null && e.Action is Line.TextAction == false )
		{
			focusedLine_.FixTextInputAction();
		}

		if( suspendLayoutCount_ <= 0 )
		{
			AdjustRequestedLayouts();
			UpdateLayoutElement();
		}

		if( IsEdited == false )
		{
			IsEdited = true;
		}
	}

	#endregion


	#region Input

	public void SubscribeKeyInput()
	{
		if( throttleInputSubscriptions_.Count > 0 )
		{
			return;
		}

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
			throttleInputSubscriptions_.Add(
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
				.Subscribe(_ => OnThrottleInput(key))
			);
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
			if( index > 0 && (line.Parent == rootLine_ || line.Parent.Field.IsSelected == false) && line.IsComment == false )
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
		}
		actionManager_.EndChain();
	}

	protected void OnShiftTabInput()
	{
		actionManager_.StartChain();
		// 逆順で下から処理
		foreach( Line line in GetSelectedOrFocusedLines(ascending: false) )
		{
			if( line.Parent.Parent != null && ( line.Parent.Field.IsSelected == false || line.Parent.Level <= 0 ) && line.IsComment == false )
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

	protected virtual void OnCtrlSpaceInput()
	{
	}

	protected void OnEnterInput()
	{
		int caretPos = focusedLine_.Field.CaretPosision;
		Line target = focusedLine_;
		Line parent = focusedLine_.Parent;
		int index = focusedLine_.Index;
		Line line = new Line(focusedLine_.IsComment ? "> " : "");

		if( caretPos == 0 && target.TextLength > 0 )
		{
			// 行頭でEnterしたので行の上にLineを追加
			actionManager_.Execute(new Action(
				execute: () =>
				{
					parent.Insert(index, line);
					parent.AdjustLayoutRecursive(index + 1);
					line.Field.CaretPosision = (line.IsComment ? 2 : 0);
					ScrollTo(target);
				},
				undo: () =>
				{
					parent.Remove(line);
					parent.AdjustLayoutRecursive(index);
					target.Field.CaretPosision = caretPos;
					ScrollTo(target);
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
					line.Text += newString;
					insertParent.Insert(insertIndex, line);
					insertParent.AdjustLayoutRecursive(insertIndex + 1);
					target.CheckIsLink();
					line.CheckIsLink();
					line.Field.CaretPosision = (line.IsComment ? 2 : 0);
					line.Field.IsFocused = true;
				},
				undo: () =>
				{
					target.Text = oldString;
					insertParent.Remove(line);
					insertParent.AdjustLayoutRecursive(insertIndex);
					target.CheckIsLink();
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
			else if( prev.Parent == line.Parent && line.TextLength == 0 && line.Count == 0 )
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
				int oldCaret = line.Field.CaretPosision;
				actionManager_.Execute(new Action(
				   execute: () =>
				   {
					   line.Text += next.Text;
				   },
				   undo: () =>
				   {
					   line.Field.CaretPosision = oldCaret;
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
							if( next.Level < line.Level )
							{
								RequestLayout(next);
							}
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
							ScrollTo(src);
						}
						));
				}
			}
			else if( Input.GetKey(KeyCode.LeftControl) )
			{
				OnFoldUpdated(focusedLine_, key == KeyCode.UpArrow);
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
				else
				{
					OnOverflowArrowInput(key);
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
				else
				{
					OnOverflowArrowInput(key);
				}
			}
			break;
		case KeyCode.LeftArrow:
			// カーソル位置が最初ならフォーカス移動
			if( focusedLine_.Field.CaretPosision <= 0 || (focusedLine_.IsComment && focusedLine_.Field.CaretPosision <= 2) )
			{
				Line prev = focusedLine_.PrevVisibleLine;
				if( prev != null )
				{
					focusedLine_.Field.IsFocused = false;
					prev.Field.CaretPosision = prev.TextLength;
					prev.Field.IsFocused = true;
				}
				else
				{
					OnOverflowArrowInput(key);
				}
			}
			break;
		}
	}

	protected virtual void OnOverflowArrowInput(KeyCode key)
	{

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
			else if( selectedLines_.Count == 1 && selectedLines_.Values[0] == focusedLine_ )
			{
				ClearSelection();
				focusedLine_.Field.SetSelection(0, focusedLine_.TextLength);
			}
			break;
		}
	}
	
	protected void OnCtrlMLInput()
	{
		if( focusedLine_ == null ) return;

		ClearSelection();

		actionManager_.StartChain();
		if( isAllFolded_ )
		{
			// unfold all
			Line line = rootLine_[0];
			while( line != null )
			{
				if( line.Count > 0 && line.IsFolded )
				{
					Line unfoldLine = line;
					Line layoutStart = line.NextVisibleLine;
					actionManager_.Execute(new Action(
						execute: () =>
						{
							unfoldLine.IsFolded = false;
							RequestLayout(layoutStart);
						},
						undo: () =>
						{
							unfoldLine.IsFolded = true;
							RequestLayout(layoutStart);
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
				Line layoutStart = line.NextSiblingOrUnkleLine;
				actionManager_.Execute(new Action(
					execute: () =>
					{
						foldLine.IsFolded = true;
						RequestLayout(layoutStart);
					},
					undo: () =>
					{
						foldLine.IsFolded = false;
						RequestLayout(layoutStart);
					}
					));
			}
			
			actionManager_.Execute(new Action(
				execute: () =>
				{
					isAllFolded_ = true;
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

	protected virtual void OnCtrlLInput()
	{

	}

	protected virtual void OnCtrlDInput()
	{
		if( focusedLine_ == null ) return;

		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			if( line.IsDone ) continue;

			Line targetLine = line;
			actionManager_.Execute(new Action(
				execute: ()=>
				{
					targetLine.IsOnList = !targetLine.IsOnList;
					ShortLineList lineList = GameContext.Window.LineList;
					if( targetLine.IsOnList )
					{
						lineList.InstantiateShortLine(targetLine);
					}
					else
					{
						lineList.RemoveLine(targetLine);
					}
				}));
		}
		actionManager_.EndChain();
	}

	protected void OnCtrlBInput()
	{
		if( focusedLine_ == null ) return;

		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			Line targetLine = line;
			actionManager_.Execute(new Action(
				execute: () =>
				{
					targetLine.IsBold = !targetLine.IsBold;
				}));
		}
		actionManager_.EndChain();
	}

	#endregion


	#region copy and paste

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
			bool appendTag = Input.GetKey(KeyCode.LeftShift) == false;
			int ignoreLevel = selectedLines_.Values[0].Level;
			StringBuilder clipboardLines = new StringBuilder();
			foreach( Line line in selectedLines_.Values )
			{
				line.AppendStringTo(clipboardLines, appendTag, ignoreLevel);
				if( line.IsFolded && appendTag )
				{
					foreach( Line child in line.GetAllChildren() )
					{
						child.AppendStringTo(clipboardLines, appendTag, ignoreLevel);
					}
				}
			}
			Clipboard = clipboardLines.ToString().TrimEnd(System.Environment.NewLine.ToCharArray());
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

			bool loadTag = pasteStart.Text == "";
			string oldText = pasteStart.Text;
			string oldTag = pasteStart.GetTagStrings();
			int oldCaretPos = pasteStart.Field.CaretPosision;
			actionManager_.Execute(new Action(
				execute: () =>
				{
					if( loadTag )
					{
						string beforeRefText = pasteText;
						pasteStart.LoadTag(ref pasteText);
						pasteStart.Field.Paste(pasteText);
						pasteStart.UpdateBindingField();
						pasteText = beforeRefText;
					}
					else
					{
						pasteStart.Field.Paste(pasteText);
					}
				},
				undo: () =>
				{
					pasteStart.Field.CaretPosision = oldCaretPos;
					if( loadTag )
					{
						string refTag = oldTag;
						pasteStart.LoadTag(ref refTag);
					}
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
				int pasteIndex = focusedLine_.Index;
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
					string beforeRefText = pasteText;
					pasteStart.LoadTag(ref pasteText);
					pasteStart.Field.Paste(pasteText);
					pasteStart.UpdateBindingField();
					pasteText = beforeRefText;
					RequestLayout(layoutStart);
				},
				undo: () =>
				{
					pasteStart.Field.CaretPosision = 0;
					pasteStart.Text = "";
					string noTag = "";
					pasteStart.LoadTag(ref noTag);
					pasteStart.UpdateBindingField();
					RequestLayout(layoutStart);
				}
				));
		}

		int oldLevel = currentLevel;
		int startLevel = pasteStart.Level;
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
				for( int level = oldLevel; currentLevel < level && startLevel < brother.Level; --level )
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
		TextField field = heapFields_.Count > 0 ? heapFields_[0] : null;
		if( field == null )
		{
			for( int i = 0; i < FieldCount; ++i )
			{
				field = Instantiate(FieldPrefab.gameObject).GetComponent<TextField>();
				field.transform.SetParent(this.transform);
				field.gameObject.SetActive(false);
				heapFields_.Add(field);
			}
		}
		if( field.BindedLine != null )
		{
			field.BindedLine.UnBind();
		}
		field.gameObject.SetActive(true);
		line.Bind(field.gameObject);
		heapFields_.Remove(field);
	}

	public void OnReBind(Line line)
	{
		if( line.Field != null )
		{
			line.Field.gameObject.SetActive(true);
			heapFields_.Remove(line.Field);
		}
	}

	public void BackToHeap(Line line)
	{
		if( line.Field != null )
		{
			line.Field.gameObject.SetActive(false);
			heapFields_.Add(line.Field);
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
		GameContext.CurrentActionManager = actionManager_;

		if( focusedLine_ != null && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
		{
			selectionStartLine_ = line;
		}

		ScrollTo(focusedLine_);
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
			Line layoutStart = line.NextSiblingOrUnkleLine;
			actionManager_.Execute(new Action(
				execute: () =>
				{
					line.IsFolded = isFolded;
					line.Field.IsFocused = true;
					RequestLayout(layoutStart);
				},
				undo: () =>
				{
					line.IsFolded = !isFolded;
					line.Field.IsFocused = true;
					RequestLayout(layoutStart);
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
	}
	
	public virtual void ScrollTo(Line targetLine)
	{

	}

	#endregion


	#region layout

	public virtual void UpdateLayoutElement()
	{
		if( suspendLayoutCount_ <= 0 && layout_ != null && rootLine_ != null && gameObject.activeInHierarchy )
		{
			Line lastLine = rootLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				layout_.preferredHeight = -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 1.5f;
				contentSizeFitter_.SetLayoutVertical();
			}
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
			UpdateLayoutElement();
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
	}

	#endregion


	#region files

	public virtual void Save()
	{

	}

	protected void SaveInternal()
	{
		StringBuilder builder = new StringBuilder();
		foreach( Line line in rootLine_.GetAllChildren() )
		{
			line.AppendStringTo(builder, appendTag: true);
		}

		StreamWriter writer = new StreamWriter(file_.FullName, append: false);
		writer.Write(builder.ToString());
		writer.Flush();
		writer.Close();

		IsEdited = false;
	}
	
	protected void LoadInternal()
	{
		SuspendLayout();

		rootLine_ = new Line(file_.Name);
		gameObject.name = "Tree - " + TitleText;
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
		rootLine_.AdjustFontSizeRecursive(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
		IsEdited = false;
	}

	public void Reload()
	{
		if( file_ == null || file_.Exists == false )
		{
			return;
		}

		if( rootLine_ != null )
		{
			ClearSelection();
			foreach( Line line in rootLine_.GetAllChildren() )
			{
				if( line.IsOnList )
				{
					GameContext.Window.LineList.RemoveLine(line);
				}
				BackToHeap(line);
			}
			rootLine_ = null;
			focusedLine_ = null;
			GC.Collect();
		}

		LoadInternal();
	}

	#endregion
}
