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

// Window > Note > [ Tree ] > Line
public class Tree : MonoBehaviour
{
	#region params

	public Note OwnerNote { get { return ownerNote_; } }
	protected Note ownerNote_;

	protected TreePath path_;
	protected Line rootLine_;
	protected GameObject rootLineObject_;
	protected GameObject titleLineObject_;
	protected Line titleLine_;
	protected Line focusedLine_, lastFocusedLine_;
	protected Line selectionStartLine_, selectionEndLine_;
	protected SortedList<int, Line> selectedLines_ = new SortedList<int, Line>();

	protected HeapManager<LineField> heapManager_;
	protected ActionManagerProxy actionManager_;
	public HeapManager<TagText> TagHeapManager { get { return tagHeapManager_; } }
	protected HeapManager<TagText> tagHeapManager_;

	// input states
	protected Vector3 cachedMousePosition_;
	protected bool wasDeleteKeyConsumed_ = false;
	protected bool wasCtrlDPushed_ = false;
	protected List<IDisposable> throttleInputSubscriptions_ = new List<IDisposable>();

	// layout
	protected bool isAllFolded_ = false;
	protected List<Line> requestLayoutLines_ = new List<Line>();
	protected int suspendLayoutCount_ = 0;


	// file
	protected FileInfo file_ = null;
	public bool IsEdited
	{
		get { return isEdited_; }
		set
		{
			isEdited_ = value;

			if( isEdited_ && OnEdited != null )
				OnEdited(this, null);
		}
	}
	protected bool isEdited_ = false;


	// event
	public event EventHandler OnEdited;
	public event EventHandler OnDoneChanged;

	// properties
	public ActionManagerProxy ActionManager { get { return actionManager_; } }
	public FileInfo File { get { return file_; } }
	public Line FocusedLine { get { return focusedLine_; } }
	public Line LastFocusedLine { get { return lastFocusedLine_; } }
	public Line RootLine { get { return rootLine_; } }
	public Line TitleLine { get { return titleLine_; } }
	public TreePath Path { get { return path_; } }

	public string TitleText { get { return titleLine_ != null ? titleLine_.Text : ""; } }
	public override string ToString() { return TitleText; }


	#endregion


	public void Initialize(Note ownerNote, ActionManagerProxy actionManager, HeapManager<LineField> heapManager, HeapManager<TagText> tagHeapManager = null)
	{
		ownerNote_ = ownerNote;
		actionManager_ = actionManager;
		heapManager_ = heapManager;
		tagHeapManager_ = tagHeapManager;

		actionManager_.ChainStarted += this.actionManager__ChainStarted;
		actionManager_.ChainEnded += this.actionManager__ChainEnded;
		actionManager_.Executed += this.actionManager__Executed;

		rootLineObject_ = new GameObject("RootLine");
		rootLineObject_.transform.SetParent(this.transform, worldPositionStays: false);
		rootLineObject_.SetActive(false);
	}


	#region unity events

	public void UpdateKeyboardInput(bool ctrl, bool shift, bool alt)
	{
		if( focusedLine_ == null && HasSelection == false )
		{
			return;
		}

		if( ctrl )
		{
			if( Input.GetKeyDown(KeyCode.V) )
			{
				Paste();
			}
			else if( Input.GetKeyDown(KeyCode.C) )
			{
				Copy(withformat: (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) == false);
			}
			else if( Input.GetKeyDown(KeyCode.X) )
			{
				Cut();
			}
			else if( Input.GetKeyDown(KeyCode.A) )
			{
				SelectAll();
			}
			else if( Input.GetKeyDown(KeyCode.E) )
			{
				OnCtrlEInput();
			}
#if UNITY_EDITOR
			else if( Input.GetKeyDown(KeyCode.W) )
#else
			else if( Input.GetKeyDown(KeyCode.S) )
#endif
			{
				SaveFile();
			}
			else if( Input.GetKeyDown(KeyCode.Space) )
			{
				if( shift )
				{
					OnCtrlShiftSpaceInput();
				}
				else
				{
					OnCtrlSpaceInput();
				}
			}
			else if( Input.GetKeyDown(KeyCode.D) )
			{
				OnCtrlDInput();
				wasCtrlDPushed_ = true;
			}
#if UNITY_EDITOR
			else if( Input.GetKeyDown(KeyCode.G) )
#else
			else if( Input.GetKeyDown(KeyCode.B) )
#endif
			{
				OnCtrlBInput();
			}
			else if( Input.GetKeyDown(KeyCode.Home) )
			{
				if( ownerNote_ is TreeNote )
				{
					Line line = titleLine_[0];
					line.Field.IsFocused = true;
					OnFocused(line);
				}
				else if( ownerNote_ is LogNote )
				{
					(ownerNote_ as LogNote).OnHomeEndInput(KeyCode.Home);
				}
			}
			else if( Input.GetKeyDown(KeyCode.End) )
			{
				if( ownerNote_ is TreeNote )
				{
					Line line = titleLine_.LastVisibleLine;
					line.Field.IsFocused = true;
					OnFocused(line);
				}
				else if( ownerNote_ is LogNote )
				{
					(ownerNote_ as LogNote).OnHomeEndInput(KeyCode.End);
				}
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

		if( wasCtrlDPushed_ && Input.anyKeyDown && ctrl == false && (ctrl && Input.GetKeyDown(KeyCode.D)) == false )
		{
			wasCtrlDPushed_ = false;
		}
	}

	public void UpdateMouseInput(bool ctrl, bool shift, bool alt)
	{
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
				LineField field = null;
				if( EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null )
				{
					field = EventSystem.current.currentSelectedGameObject.GetComponent<LineField>();
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

			cachedMousePosition_ = Input.mousePosition;
		}
		else if( Input.GetMouseButton(0) && selectionStartLine_ != null )
		{
			if( selectionEndLine_ == null )
				selectionEndLine_ = selectionStartLine_;

			if( (cachedMousePosition_ - Input.mousePosition).magnitude > 2.0f )
			{
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
				if( selectedLines_.Count == 1 && rect.Contains(Input.mousePosition) && Input.mousePosition.x - rect.x < selectionStartLine_.Field.GetFullTextRectLength() )
				{
					UpdateSelection(selectionStartLine_, false);
				}
				else if( selectedLines_.Count == 0 && rect.Contains(Input.mousePosition)
					&& selectionStartLine_.Field.selectionFocusPosition != selectionStartLine_.Field.selectionAnchorPosition
					&& Input.mousePosition.x - rect.x > selectionStartLine_.Field.GetFullTextRectLength() )
				{
					UpdateSelection(selectionStartLine_, true);
				}
			}

			cachedMousePosition_ = Input.mousePosition;
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

	public void UpdateSelection(Line line, bool isSelected)
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

		selectionStartLine_ = titleLine_[0];
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

		List<LineAction> reparentActions = new List<LineAction>();
		List<LineAction> deleteActions = new List<LineAction>();

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
					if( prev == null ) prev = titleLine_;
					Line lostParent = line;
					bool wasFolded = prev.IsFolded;
					reparentActions.Add(new LineAction(
						targetLines: lostChildren.ToArray(),
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
			deleteActions.Add(new LineAction(
				targetLines: line,
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

		foreach( LineAction action in reparentActions )
		{
			actionManager_.Execute(action);
		}
		foreach( LineAction action in deleteActions )
		{
			actionManager_.Execute(action);
		}

		// 選択解除
		actionManager_.Execute(new LineAction(
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
		actionManager_.Execute(new LineAction(
			targetLines: newLine,
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

	protected void actionManager__ChainStarted(object sender, ChainActionEventArgs e)
	{
		SuspendLayout();
	}

	protected void actionManager__ChainEnded(object sender, ChainActionEventArgs e)
	{
		ResumeLayout();

		IsEdited = true;
	}

	protected void actionManager__Executed(object sender, ActionEventArgs e)
	{
		if( focusedLine_ != null && e.Action is Line.TextAction == false )
		{
			focusedLine_.FixTextInputAction();
		}

		if( GameContext.Window.TagIncrementalDialog.IsActive && e.Action is Line.TextAction == false )
		{
			GameContext.Window.TagIncrementalDialog.Close();
		}

		if( suspendLayoutCount_ <= 0 )
		{
			AdjustRequestedLayouts();
			ownerNote_.UpdateLayoutElement();
		}

		if( actionManager_.IsChaining == false )
		{
			IsEdited = true;
		}
	}

	#endregion


	#region input

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

		IObservable<double> pointerIntervalObservable =
			this.LateUpdateAsObservable().Where(_ => Input.GetMouseButtonDown(0))
				.TimeInterval()
				.Select(t => t.Interval.TotalSeconds);

		pointerIntervalObservable.Buffer(2, 1)
			.Where(list => list[0] > GameContext.Config.DoubleClickInterval)
			.Where(list => list.Count > 1 ? list[1] <= GameContext.Config.DoubleClickInterval : false)
			.Subscribe(_ => OnDoubleClick()).AddTo(this);
		pointerIntervalObservable.Buffer(3, 1)
			.Where(list => list[0] > GameContext.Config.DoubleClickInterval)
			.Where(list => list.Count > 2 && list[1] <= GameContext.Config.DoubleClickInterval && list[2] <= GameContext.Config.DoubleClickInterval)
			.Subscribe(_ => OnTripleClick()).AddTo(this);
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
				OnArrowInput(key);
				break;
		}
	}

	protected void OnTabInput()
	{
		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			int index = line.Index;
			if( index > 0 && (line.Parent == titleLine_ || line.Parent.Field.IsSelected == false) && line.IsComment == false )
			{
				Line oldParent = line.Parent;
				Line newParent = line.Parent[index - 1];
				bool wasParentFolded = false;
				actionManager_.Execute(new LineAction(
					targetLines: line,
					execute: () =>
					{
						wasParentFolded = newParent.IsFolded;
						if( wasParentFolded )
						{
							newParent.IsFolded = false;
						}
						newParent.Add(line);
						if( wasParentFolded )
						{
							RequestLayout(newParent.NextSiblingOrUnkleLine);
						}
					},
					undo: () =>
					{
						oldParent.Insert(index, line);
						if( wasParentFolded )
						{
							newParent.IsFolded = true;
						}
						if( wasParentFolded )
						{
							RequestLayout(line);
						}
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
			if( line.Parent.IsTitleLine == false && (line.Parent.Field.IsSelected == false || line.Parent.Level <= 0) && line.IsComment == false )
			{
				int index = line.Index;
				Line targetLine = line;
				Line oldParent = line.Parent;
				Line newParent = line.Parent.Parent;
				actionManager_.Execute(new LineAction(
					targetLines: targetLine,
					execute: () =>
					{
						Line layoutStart = targetLine.Parent[index + 1];
						newParent.Insert(targetLine.Parent.Index + 1, targetLine);
						if( layoutStart != null && layoutStart.Field.IsSelected == false )
						{
							layoutStart.Parent.AdjustLayoutRecursive(layoutStart.Index);
						}
					},
					undo: () =>
					{
						oldParent.Insert(index, targetLine);
						oldParent.AdjustLayoutRecursive(index);
					}
					));
			}
		}
		actionManager_.EndChain();
	}

	public void OnCtrlSpaceInput()
	{
		if( focusedLine_ == null ) return;

		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			if( line.Text != "" )
			{
				Line targetLine = line;
				Done(targetLine);
			}
		}
		actionManager_.EndChain();
	}

	public virtual void OnCtrlShiftSpaceInput()
	{
		if( focusedLine_ == null ) return;

		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			if( line.IsDone == false && line.Text != "" )
			{
				Line targetLine = line;
				actionManager_.Execute(new LineAction(
					targetLines: targetLine,
					execute: () =>
					{
						(ownerNote_ as TreeNote).LogNote.TodayTree.AddLog(targetLine);
						targetLine.Field.OnRepeatDone();
					},
					undo: () =>
					{
						(ownerNote_ as TreeNote).LogNote.TodayTree.RemoveLog(targetLine);
					}));

			}
		}
		actionManager_.EndChain();
	}

	public void Done(Line targetLine)
	{
		List<string> removeTags = new List<string>();
		actionManager_.Execute(new LineAction(
			targetLines: targetLine,
			execute: () =>
			{
				targetLine.IsDone = !targetLine.IsDone;

				// タグ整理
				if( targetLine.IsDone )
				{
					// doneされたのでタグを消す、Repeatの場合は反映する
					foreach( string tag in targetLine.Tags )
					{
						TagParent tagParent = GameContext.Window.TagList.GetTagParent(tag);
						if( tagParent != null )
						{
							TaggedLine taggedLine = tagParent.FindBindedLine(targetLine);
							if( taggedLine != null )
							{
								taggedLine.IsDone = targetLine.IsDone;
							}
							if( tagParent.IsRepeat == false )
							{
								removeTags.Add(tag);
							}
						}
					}
					foreach( string removeTag in removeTags )
					{
						targetLine.RemoveTag(removeTag);
					}
				}
				else
				{
					// done解除されたのでRepeatのやつは反映して、消したのは復活させる
					foreach( string tag in targetLine.Tags )
					{
						TagParent tagParent = GameContext.Window.TagList.GetTagParent(tag);
						if( tagParent != null )
						{
							TaggedLine taggedLine = tagParent.FindBindedLine(targetLine);
							if( taggedLine != null )
							{
								taggedLine.IsDone = targetLine.IsDone;
							}
						}
					}
					// さっき消したタグは復活
					foreach( string removedTag in removeTags )
					{
						targetLine.AddTag(removedTag);
					}
					removeTags.Clear();
				}

				if( targetLine.Field != null )
					targetLine.Field.SetHashTags(targetLine.Tags);

				if( OnDoneChanged != null )
					OnDoneChanged(targetLine, null);
			}
			));
	}

	protected void OnEnterInput()
	{
		if( focusedLine_ != null && GameContext.Window.TagIncrementalDialog.IsActive )
		{
			// タグのインクリメンタルサーチで選択した時の場合
			string selectedTag = GameContext.Window.TagIncrementalDialog.GetSelectedTag();
			if( selectedTag != null )
			{
				Line line = focusedLine_;
				string caretTag = Line.GetTagInCaretPosition(line.Text, line.Field.CaretPosision);
				int oldCaretPos = line.Field.CaretPosision;
				actionManager_.Execute(new LineAction(
					targetLines: line,
					execute: () =>
					{
						line.Field.CaretPosision = 0;
						line.RemoveTag(caretTag);
						line.AddTag(selectedTag);
						line.Field.CaretPosision = line.Text.Length;
					},
					undo: () =>
					{
						line.Field.CaretPosision = 0;
						line.RemoveTag(selectedTag);
						line.AddTag(caretTag);
						line.Field.CaretPosision = oldCaretPos;
					}
					));
				GameContext.Window.TagIncrementalDialog.Close();
				return;
			}
		}

		int caretPos = focusedLine_.Field.CaretPosision;
		Line target = focusedLine_;
		Line parent = focusedLine_.Parent;
		int index = focusedLine_.Index;
		Line newline = new Line();
		if( focusedLine_.IsComment )
		{
			newline.IsComment = true;
		}

		if( caretPos == 0 && target.TextLength > 0 )
		{
			// 行頭でEnterしたので行の上にLineを追加
			actionManager_.Execute(new LineAction(
				targetLines: newline,
				execute: () =>
				{
					parent.Insert(index, newline);
					parent.AdjustLayoutRecursive(index + 1);
					newline.Field.CaretPosision = 0;
					ownerNote_.ScrollTo(target);
				},
				undo: () =>
				{
					parent.Remove(newline);
					parent.AdjustLayoutRecursive(index);
					target.Field.CaretPosision = caretPos;
					ownerNote_.ScrollTo(target);
				}
				));
		}
		else
		{
			// 行の途中（または最後）でEnterしたので行の下にLineを追加して文字を分割
			string oldString = target.Text;
			string subString = target.Text.Substring(0, caretPos);
			string newString = target.Text.Substring(caretPos, target.TextLength - caretPos);
			newline.Text = newString;

			// 基本はすぐ下の兄弟にする
			Line insertParent = parent;
			int insertIndex = index + 1;
			// Childがいる場合はChildにする
			if( target.HasVisibleChild )
			{
				insertParent = target;
				insertIndex = 0;
			}

			actionManager_.Execute(new LineAction(
				targetLines: new Line[] { target, newline },
				execute: () =>
				{
					target.Text = subString;
					insertParent.Insert(insertIndex, newline);
					insertParent.AdjustLayoutRecursive(insertIndex + 1);
					newline.Field.CaretPosision = 0;
					newline.Field.IsFocused = true;
				},
				undo: () =>
				{
					target.Text = oldString;
					insertParent.Remove(newline);
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
				// 前のテキストが無いので前のラインを消せばOK
				Line parent = prev.Parent;
				int index = prev.Index;
				actionManager_.Execute(new LineAction(
					targetLines: prev,
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
				// 選択ラインのテキストが無いので選択ラインを消せばOK
				Line parent = line.Parent;
				int index = line.Index;
				Line layoutStart = line.NextVisibleLine;
				actionManager_.Execute(new LineAction(
					targetLines: line,
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
				// 選択もその前もテキストがあるのでテキスト合体が必要
				actionManager_.StartChain();
				Line parent = line.Parent;
				int index = line.Index;

				// テキスト合体してキャレット移動
				string oldText = prev.Text;
				actionManager_.Execute(new LineAction(
					targetLines: prev,
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
					actionManager_.Execute(new LineAction(
						targetLines: children.ToArray(),
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
				actionManager_.Execute(new LineAction(
					targetLines: line,
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
				// 次のテキストが無いので次のラインを消せばOK
				Line parent = next.Parent;
				int index = next.Index;
				actionManager_.Execute(new LineAction(
					targetLines: next,
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
				// 選択ラインのテキストが無いので選択ラインを消せばOK
				Line parent = line.Parent;
				int index = line.Index;
				actionManager_.Execute(new LineAction(
					targetLines: line,
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
				// 選択もその次もテキストがあるのでテキスト合体が必要
				actionManager_.StartChain();
				Line parent = next.Parent;
				int index = next.Index;

				// テキスト合体
				string oldText = line.Text;
				int oldCaret = line.Field.CaretPosision;
				actionManager_.Execute(new LineAction(
					targetLines: line,
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
					actionManager_.Execute(new LineAction(
						targetLines: children.ToArray(),
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

				// 削除
				Line layoutStart = next.NextVisibleLine;
				actionManager_.Execute(new LineAction(
					targetLines: next,
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
		if( GameContext.Window.TagIncrementalDialog.IsActive )
		{
			if( key == KeyCode.UpArrow || key == KeyCode.DownArrow )
			{
				GameContext.Window.TagIncrementalDialog.OnArrowInput(key);
				return;
			}
		}

		bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

		if( shift && ctrl == false )
		{
			OnShiftArrowInput(key);
			return;
		}

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
				if( alt )
				{
					OnAltArrowInput(key);
				}
				else if( ctrl )
				{
					if( shift )
					{
						OnCtrlShiftArrowInput(key);
					}
					else if( focusedLine_.Count > 0 && (key == KeyCode.UpArrow) != focusedLine_.IsFolded )
					{
						OnCtrlArrowInput(key);
					}
					else
					{
						Line newFocus = key == KeyCode.UpArrow ? focusedLine_.Parent : focusedLine_.LastVisibleLine;
						if( newFocus != null && newFocus != titleLine_ )
						{
							newFocus.Field.IsFocused = true;
							OnFocused(newFocus);
						}
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
					if( GameContext.Window.TagIncrementalDialog.IsActive )
					{
						GameContext.Window.TagIncrementalDialog.Close();
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
					else
					{
						OnOverflowArrowInput(key);
					}
					if( GameContext.Window.TagIncrementalDialog.IsActive )
					{
						GameContext.Window.TagIncrementalDialog.Close();
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

		if( GameContext.Window.TagIncrementalDialog.IsActive )
		{
			GameContext.Window.TagIncrementalDialog.Close();
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

	public void OnAltArrowInput(KeyCode key)
	{
		// 上下の兄弟と交換
		Line src = focusedLine_;
		Line dest = (key == KeyCode.DownArrow ? src.NextSiblingLine : src.PrevSiblingLine);
		if( dest != null )
		{
			actionManager_.Execute(new LineAction(
				targetLines: new Line[] { src, dest },
				execute: () =>
				{
					src.Parent.Insert(dest.Index, src);
					dest.AdjustLayout(withAnim: true);
					ownerNote_.ScrollTo(src);
				}
				));
		}
	}

	public void OnCtrlArrowInput(KeyCode key)
	{
		// 折りたたみor展開
		OnFoldUpdated(focusedLine_, key == KeyCode.UpArrow);
	}

	public void OnCtrlShiftArrowInput(KeyCode key)
	{
		// すべて折りたたみor展開
		bool isFolded = (key == KeyCode.UpArrow);

		actionManager_.StartChain();
		OnFoldUpdated(focusedLine_, isFolded);
		foreach( Line line in focusedLine_.GetAllChildren() )
		{
			OnFoldUpdated(line, isFolded);
		}
		actionManager_.EndChain();
	}

	public virtual void OnCtrlEInput()
	{
		if( focusedLine_ == null ) return;

		if( focusedLine_.Count == 0 )
		{
			Line newLine = new Line();
			Line titleLine = focusedLine_;
			Line layoutStart = titleLine.NextSiblingOrUnkleLine;
			actionManager_.Execute(new LineAction(
				targetLines: newLine,
				execute:()=>
				{
					titleLine.Add(newLine);
				},
				undo:()=>
				{
					titleLine.Remove(newLine);
					RequestLayout(layoutStart);
				}));
		}

		GameContext.Window.TabGroup.AddTab(focusedLine_);
	}

	protected virtual void OnCtrlDInput()
	{
		if( focusedLine_ == null ) return;

		TagList tagList = GameContext.Window.TagList;

		string newtag = tagList.Count > 0 ? tagList[0].Tag : GameContext.Config.DefaultTag;

		// Ctrl+Dを連続で押している時はその次のタグに変更する
		string existTag = null;
		if( wasCtrlDPushed_ && focusedLine_.Tags.Count > 0 )
		{
			existTag = focusedLine_.Tags[focusedLine_.Tags.Count - 1];
			int existIndex = -1;
			for( int i = 0; i < tagList.Count; ++i )
			{
				if( tagList[i].Tag == existTag )
				{
					existIndex = i;
					break;
				}
			}

			if( existIndex >= 0 )
			{
				int sign = (Input.GetKey(KeyCode.LeftShift) ? tagList.Count - 1 : 1);
				int newTagIndex = (existIndex + sign) % tagList.Count;
				bool foundNewTag = false;
				// 次のタグが万が一既に含まれていた場合はその次のタグ……と探していく。
				while( newTagIndex != existIndex )
				{
					string newtagCandid = tagList[newTagIndex].Tag;
					if( focusedLine_.Tags.Contains(newtagCandid) == false )
					{
						foundNewTag = true;
						newtag = newtagCandid;
						break;
					}
					else
					{
						newTagIndex += sign;
						newTagIndex %= tagList.Count;
					}
				}
				// 変更すべきタグが見つからなかったのでナシ
				if( foundNewTag == false )
				{
					existTag = null;
				}
			}
		}

		actionManager_.StartChain();
		foreach( Line line in GetSelectedOrFocusedLines() )
		{
			if( line.IsDone || line.Tags.Contains(newtag) ) continue;

			Line targetLine = line;
			bool hasExistTag = existTag != null && targetLine.Tags.Contains(existTag);
			actionManager_.Execute(new LineAction(
				targetLines: targetLine,
				execute: () =>
				{
					if( hasExistTag )
					{
						targetLine.RemoveTag(existTag);
					}
					targetLine.AddTag(newtag);
				},
				undo: () =>
				{
					targetLine.RemoveTag(newtag);
					if( hasExistTag )
					{
						targetLine.AddTag(existTag);
					}
				}
				));
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
			actionManager_.Execute(new LineAction(
				targetLines: targetLine,
				execute: () =>
				{
					targetLine.IsBold = !targetLine.IsBold;
				}));
		}
		actionManager_.EndChain();
	}

	public void OnDoubleClick()
	{
		if( focusedLine_ != null )
		{
			focusedLine_.Field.OnDoubleClick();
		}
	}

	public void OnTripleClick()
	{
		if( focusedLine_ != null )
		{
			UpdateSelection(focusedLine_, true);
		}
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

	public void Copy(bool withformat = true)
	{
		if( HasSelection || (focusedLine_ != null && focusedLine_.Field.HasSelection == false ) )
		{
			bool appendTag = withformat;
			int ignoreLevel = (selectedLines_.Count > 0 ? selectedLines_.Values[0] : focusedLine_).Level;
			StringBuilder clipboardLines = new StringBuilder();
			foreach( Line line in GetSelectedOrFocusedLines() )
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

	public void Paste()
	{
		if( focusedLine_ == null )
			return;

		actionManager_.StartChain();

		// delete
		Line pasteStart = null;
		if( HasSelection )
		{
			// 選択範囲がある場合は、そこが消されて入力用の新しいLineが返される
			pasteStart = DeleteSelection();
		}

		string[] cilpboardLines = Clipboard.Split(LineSeparator, System.StringSplitOptions.None);

		// 1行目はカーソル位置に挿入するなど、それ以降とは違う処理が入るので最初に対応
		string pasteText = cilpboardLines[0];

		// Levelは相対的な値として貼り付けたいので、先頭行のレベルを基本として保存しておく
		int currentLevel = 0;
		while( pasteText.StartsWith(Line.TabString) )
		{
			++currentLevel;
			pasteText = pasteText.Remove(0, 1);
		}

		// 1行目の貼り付け。
		if( cilpboardLines.Length == 1 )
		{
			// 1行しかないなら、選択行にそのままPasteする
			if( pasteStart == null )
			{
				pasteStart = focusedLine_;
			}

			bool loadTag = pasteStart.Text == "";
			string oldText = pasteStart.Text;
			string oldTag = pasteStart.GetTagStrings();
			int oldCaretPos = pasteStart.Field.CaretPosision;
			actionManager_.Execute(new LineAction(
				targetLines: pasteStart,
				execute: () =>
				{
					if( loadTag )
					{
						string beforeRefText = pasteText;
						pasteStart.LoadLineTag(ref pasteText);
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
						pasteStart.LoadLineTag(ref refTag);
					}
					pasteStart.Text = oldText;
				}
				));
		}
		else
		{
			// 2行以上あるなら、空白の選択行またはその直前にPaste開始用の行を用意する
			if( focusedLine_.Text == "" )
			{
				pasteStart = focusedLine_;
			}
			else if( pasteStart == null )
			{
				Line pasteParent = focusedLine_.Parent;
				int pasteIndex = focusedLine_.Index;
				pasteStart = new Line();
				actionManager_.Execute(new LineAction(
					targetLines: pasteStart,
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

			// 最初の行を貼り付け
			Line layoutStart = pasteStart.NextVisibleLine;
			actionManager_.Execute(new LineAction(
				targetLines: pasteStart,
				execute: () =>
				{
					string beforeRefText = pasteText;
					pasteStart.LoadLineTag(ref pasteText);
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
					pasteStart.LoadLineTag(ref noTag);
					pasteStart.UpdateBindingField();
					RequestLayout(layoutStart);
				}
				));
		}

		// 2行目以降を貼り付け
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

			actionManager_.Execute(new LineAction(
				targetLines: line,
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

	public void Cut()
	{
		if( HasSelection )
		{
			Copy(withformat: (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) == false);
			DeleteSelection();
		}
	}

	#endregion


	#region events
	
	public virtual void OnTreeFocused(Vector2 mousePosition)
	{
		if( titleLine_ != null )
		{
			Line line = titleLine_.LastVisibleLine;
			if( line != null && line.Field != null && mousePosition.y < line.Field.RectY )
			{
				line.Field.Select();
			}
		}
		GameContext.CurrentActionManager = (ActionManager)actionManager_;
	}

	public GameObject FindBindingField()
	{
		LineField field = heapManager_.Instantiate();
		field.Initialize();
		if( field.BindedLine != null )
		{
			Transform heapParent = transform.Find("LineHeap");
			foreach( LineField childField in field.GetComponentsInChildren<LineField>() )
			{
				childField.transform.SetParent(heapParent);
			}
			field.BindedLine.UnBind();
		}
		return field.gameObject;
	}

	public void OnReBind(Line line)
	{
		if( line.Field != null )
		{
			heapManager_.Revive(line.Field);
		}
	}

	public void BackToHeap(Line line)
	{
		if( line.Field != null )
		{
			heapManager_.BackToHeap(line.Field);
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
		if( line != null )
		{
			lastFocusedLine_ = line;
		}
		GameContext.CurrentActionManager = (ActionManager)actionManager_;

		if( focusedLine_ != null && focusedLine_.Field.Rect.Contains(Input.mousePosition) )
		{
			selectionStartLine_ = line;
		}

		ownerNote_.ScrollTo(focusedLine_);
	}

	public void OnFocusEnded(Line line)
	{
		if( GameContext.Window.ContextMenu.gameObject.activeInHierarchy )
		{
			return;
		}

		if( line == focusedLine_ )
		{
			focusedLine_.FixTextInputAction();
			focusedLine_ = null;
		}
	}

	public void OnFoldUpdated(Line line, bool isFolded)
	{
		if( line.Count > 0 && line.IsFolded != isFolded )
		{
			Line layoutStart = line.NextSiblingOrUnkleLine;
			actionManager_.Execute(new LineAction(
				targetLines: line,
				execute: () =>
				{
					line.IsFolded = !line.IsFolded;
					RequestLayout(layoutStart);
				},
				undo: () =>
				{
					line.IsFolded = !line.IsFolded;
					RequestLayout(layoutStart);
				}
				));
		}
	}

	public void OnDeleteKeyConsumed()
	{
		wasDeleteKeyConsumed_ = true;
	}

	public void OnTextFieldDestroy(LineField field)
	{
	}

	#endregion


	#region utils
	
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

	public IEnumerable<SearchField.SearchResult> Search(string text, bool containFolded = true)
	{
		if( titleLine_ != null && text != null && text != "" && text != " "/*space*/ && text != "	"/*tab*/ )
		{
			foreach( Line line in (containFolded ? titleLine_.GetAllChildren() : titleLine_.GetVisibleChildren()) )
			{
				foreach( SearchField.SearchResult res in line.Search(text) )
				{
					yield return res;
				}
			}
		}
	}

	#endregion


	#region layout

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
			ownerNote_.UpdateLayoutElement();
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

	public float GetPreferredHeight()
	{
		if( suspendLayoutCount_ <= 0 && titleLine_ != null && gameObject.activeInHierarchy )
		{
			Line lastLine = titleLine_.LastVisibleLine;
			if( lastLine != null && lastLine.Field != null )
			{
				return -(lastLine.TargetAbsolutePosition.y - this.transform.position.y) + GameContext.Config.HeightPerLine * 2.0f;
			}
		}
		return 0;
	}

	#endregion


	#region files

	public void NewFile(FileInfo file)
	{
		file_ = file;
		path_ = new TreePath();
		SuspendLayout();
		rootLine_ = new Line(file_.Name);
		titleLine_ = rootLine_;
		titleLineObject_ = rootLineObject_;
		gameObject.name = "Tree - " + TitleText;
		titleLine_.Bind(this.gameObject);
		titleLine_.Add(new Line(""));
		ResumeLayout();
	}

	public virtual void SaveFile()
	{
		StringBuilder builder = new StringBuilder();
		foreach( Line line in rootLine_.GetAllChildren() )
		{
			line.AppendStringTo(builder, appendTag: true, fromRoot: true);
		}

		StreamWriter writer = new StreamWriter(file_.FullName, append: false);
		writer.Write(builder.ToString());
		writer.Flush();
		writer.Close();

		IsEdited = false;
	}

	public void SaveAllTreeInOneFile(StringBuilder builder, string title)
	{
		if( title != "" )
		{
			builder.AppendLine(title);
		}
		foreach( Line line in rootLine_.GetAllChildren() )
		{
			if( title != "" )
			{
				builder.Append("	");
			}
			line.AppendStringTo(builder, appendTag: true);
		}
	}

	public void LoadFile(FileInfo file)
	{
		file_ = file;
		path_ = new TreePath();

		SuspendLayout();

		rootLine_ = new Line(file_.Name);
		titleLine_ = rootLine_;
		titleLineObject_ = rootLineObject_;
		gameObject.name = "Tree - " + TitleText;
		titleLine_.Bind(this.gameObject);

		if( file_.Exists )
		{
			StreamReader reader = new StreamReader(file_.OpenRead());
			Line parent = titleLine_;
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
			reader.Close();
		}
		if( titleLine_.Count == 0 )
		{
			titleLine_.Add(new Line(""));
		}

		ResumeLayout();
		titleLine_.AdjustFontSizeRecursive(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
		IsEdited = false;
		actionManager_.Clear();
	}


	public void ReloadFile()
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
				line.BackToHeap();
			}
			rootLine_ = null;
			focusedLine_ = null;
			titleLine_ = null;
			titleLineObject_ = null;
			GC.Collect();
		}
		
		LoadFile(file_);
	}

	#endregion


	#region path

	public Line GetLineFromPath(TreePath path)
	{
		if( path.Length == 0 )
		{
			return rootLine_;
		}

		Line line = rootLine_;
		foreach( string lineStr in path )
		{
			bool find = false;
			foreach( Line child in line )
			{
				if( child.TextWithoutHashTags == lineStr )
				{
					line = child;
					find = true;
					break;
				}
			}
			if( find == false )
			{
				return null;
			}
		}

		return line;
	}

	public void SetRootPath()
	{
		path_ = new TreePath();
		SetTitleLine(rootLine_);
	}

	public void SetPath(TreePath path)
	{
		path_ = path;
		SetTitleLine(GetLineFromPath(path));
	}

	protected void SetTitleLine(Line line)
	{
		// Bindされてなければ（Foldされている状態なら）親までたどってBindする
		if( line != null && line.BindState != Line.EBindState.Bind )
		{
			Line unbidedLine = line;
			while( unbidedLine != null && unbidedLine.IsTitleLine == false )
			{
				switch( unbidedLine.BindState )
				{
					case Line.EBindState.Bind:
						unbidedLine = null;
						break;
					case Line.EBindState.Unbind:
						unbidedLine.Bind(FindBindingField());
						unbidedLine = unbidedLine.Parent;
						break;
					case Line.EBindState.WeakBind:
						unbidedLine.ReBind();
						unbidedLine = unbidedLine.Parent;
						break;
				}
			}
		}

		Line oldTitleLine = titleLine_;
		GameObject oldTitleLineObject = titleLineObject_;
		titleLine_ = line;
		titleLineObject_ = titleLine_ != null ? (titleLine_.Binding == this.gameObject ? rootLineObject_ : titleLine_.Binding) : null;

		// 前のTitleLine以下を元のツリーに戻す
		if( oldTitleLine != null && oldTitleLineObject != null )
		{
			oldTitleLine.Bind(oldTitleLineObject);
			oldTitleLine.OnChildVisibleChanged(oldTitleLineObject);
		}

		// 新しいTitleLine以下をTreeの下に移動する
		if( titleLine_ != null && titleLineObject_ != null )
		{
			titleLine_.Bind(this.gameObject);
			titleLine_.OnChildVisibleChanged(this.gameObject);
		}

		focusedLine_ = null;
		lastFocusedLine_ = null;
		selectionStartLine_ = selectionEndLine_ = null;
		selectedLines_.Clear();

		ownerNote_.UpdateLayoutElement();
	}

	#endregion
}
