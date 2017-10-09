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

// Window - [ TreeNote ] - Line
public class TreeNote : Tree
{
	#region properties

	public bool IsActive { get { return (tabButton_ != null ? tabButton_.IsOn : false); } set { if( tabButton_ != null ) tabButton_.IsOn = value; } }
	public override bool IsEdited
	{
		get
		{
			return isEdited_;
		}
		set
		{
			isEdited_ = value;
			tabButton_.Text = TitleText + (isEdited_ ? "*" : "");
		}
	}
	public TabButton Tab { get { return tabButton_; } }
	protected TabButton tabButton_;

	public LogNote LogNote { get { return logNote_; } }
	protected LogNote logNote_;

	protected ScrollRect scrollRect_;
	protected float targetScrollValue_ = 1.0f;
	protected bool isScrollAnimating_;

	#endregion


	#region unity events

	protected override void Awake()
	{
		base.Awake();
		scrollRect_ = GetComponentInParent<ScrollRect>();

		actionManager_ = new ActionManager();
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
	protected override void Update()
	{
		if( isScrollAnimating_ )
		{
			scrollRect_.verticalScrollbar.value = Mathf.Lerp(scrollRect_.verticalScrollbar.value, targetScrollValue_, 0.2f);
			if( Mathf.Abs(scrollRect_.verticalScrollbar.value - targetScrollValue_) < 0.01f )
			{
				scrollRect_.verticalScrollbar.value = targetScrollValue_;
				isScrollAnimating_ = false;
			}
		}

		scrollRect_.enabled = Input.GetKey(KeyCode.LeftControl) == false;

		base.Update();
	}

	#endregion


	#region input

	protected override void OnCtrlSpaceInput()
	{
		if( focusedLine_ == null ) return;

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
						LogNote.OnDoneChanged(targetLine);
					}
					));
			}
		}
		actionManager_.EndChain();
	}

	protected override void OnCtrlLInput()
	{
		LogNote.IsOpended = !LogNote.IsOpended;
	}

	#endregion


	#region tab, scroll

	public override void ScrollTo(Line targetLine)
	{
		if( IsActive == false )
		{
			IsActive = true;
		}

		float scrollHeight = scrollRect_.GetComponent<RectTransform>().rect.height;
		float targetAbsolutePositionY = targetLine.TargetAbsolutePosition.y;
		float targetHeight = -(targetAbsolutePositionY - this.transform.position.y);
		float heightPerLine = GameContext.Config.HeightPerLine;

		// focusLineが下側に出て見えなくなった場合
		float targetUnderHeight = -(targetAbsolutePositionY - scrollRect_.transform.position.y) + heightPerLine / 2 - scrollHeight;
		if( targetUnderHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01(1.0f - (targetHeight + heightPerLine * 1.5f - scrollHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}

		// focusLineが上側に出て見えなくなった場合
		float targetOverHeight = (targetAbsolutePositionY - scrollRect_.transform.position.y);
		if( targetOverHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01((layout_.preferredHeight - scrollHeight - targetHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}
	}
	
	public void CheckScrollbarEnabled()
	{
		if( scrollRect_.verticalScrollbar.isActiveAndEnabled == false )
		{
			scrollRect_.verticalScrollbar.value = 1.0f;
		}
	}

	public void OnTabSelected()
	{
		GameContext.CurrentActionManager = actionManager_;

		UpdateLayoutElement();
		logNote_.UpdateLayoutElement();
		scrollRect_.verticalScrollbar.value = targetScrollValue_;

		if( focusedLine_ == null )
		{
			focusedLine_ = rootLine_[0];
		}
		focusedLine_.Field.IsFocused = true;

		SubscribeKeyInput();
		logNote_.SubscribeKeyInput();

#if UNITY_STANDALONE_WIN
		GameContext.Window.SetTitle(TitleText + " - Dones");
#endif

		if( LogNote != null )
		{
			LogNote.gameObject.SetActive(true);
			LogNote.OnTreeNoteSelected();
		}
	}

	public void OnTabDeselected()
	{
		targetScrollValue_ = scrollRect_.verticalScrollbar.gameObject.activeInHierarchy ? scrollRect_.verticalScrollbar.value : 1.0f;

		if( LogNote != null )
		{
			LogNote.OnTreeNoteDeselected();
			LogNote.gameObject.SetActive(false);
		}
	}

	public void OnTabClosed()
	{
		List<ShortLine> removeList = new List<ShortLine>();
		foreach( ShortLine shortLine in GameContext.Window.LineList )
		{
			if( shortLine.BindedLine.Tree == this )
			{
				removeList.Add(shortLine);
			}
		}
		foreach( ShortLine shortLine in removeList )
		{
			GameContext.Window.LineList.RemoveShortLine(shortLine);
		}
	}

	public void OnFontSizeChanged()
	{
		rootLine_.AdjustFontSizeRecursive(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
		foreach(TextField field in heapParent_.GetComponentsInChildren<TextField>())
		{
			field.textComponent.fontSize = GameContext.Config.FontSize;
			field.RectHeight = GameContext.Config.HeightPerLine;
			field.OnTextLengthChanged();
		}
		UpdateLayoutElement();
		LogNote.OnFontSizeChanged(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
	}

	#endregion


	#region file

	public void NewFile(TabButton tab, LogNote logNote)
	{
		tabButton_ = tab;
		logNote_ = logNote;

		SuspendLayout();
		rootLine_ = new Line("new");
		rootLine_.Bind(this.gameObject);
		rootLine_.Add(new Line(""));
		ResumeLayout();

		tabButton_.BindedNote = this;
		tabButton_.Text = TitleText;
	}

	public void Load(string path, TabButton tab, LogNote logNote, bool isActive)
	{
		if( file_ != null )
		{
			return;
		}

		file_ = new FileInfo(path);

		tabButton_ = tab;
		logNote_ = logNote;

		gameObject.SetActive(isActive);
		logNote_.gameObject.SetActive(isActive);

		LoadInternal();

		tabButton_.BindedNote = this;
		tabButton_.Text = TitleText;
		targetScrollValue_ = 1.0f;
	}

	public override void Save()
	{
		if( file_ == null )
		{
			SaveAs();
		}
		else
		{
			SaveInternal();
			logNote_.Save();
		}
	}

	public void SaveAs()
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

		SaveInternal();
		logNote_.Save();

		GameContext.Window.AddRecentOpenedFiles(file_.FullName);
	}

	#endregion
}