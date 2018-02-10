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
public class TreeNote : Note
{
	#region properties
	
	public Tree Tree { get { return tree_; } }
	protected Tree tree_;

	public LogNote LogNote { get { return logNote_; } }
	protected LogNote logNote_;

	public bool IsEdited { get { return tree_.IsEdited; } }
	public FileInfo File { get { return tree_ != null ? tree_.File : null; } }
	public override string TitleText
	{
		get
		{
			if( logNote_.IsFullArea )
			{
				return logNote_.TitleText;
			}
			else
			{
				return tree_.TitleText;
			}
		}
	}

	#endregion

	protected override void Awake()
	{
		base.Awake();
		tree_ = GetComponent<Tree>();
		tree_.Initialize(this, new ActionManagerProxy(actionManager_), heapManager_);
		tree_.OnEdited += this.OnEdited;
		tree_.OnDoneChanged += this.OnDoneChanged;
	}


	protected override void Update()
	{
		base.Update();

		bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
		bool ctrlOnly = ctrl && !alt && !shift;
		
		if( ctrlOnly && Input.GetKeyDown(KeyCode.L) )
		{
			logNote_.IsOpended = !logNote_.IsOpended;
		}
	}

	#region event
	
	void OnDoneChanged(object sender, EventArgs e)
	{
		Line line = sender as Line;
		logNote_.OnDoneChanged(line);
	}

	#endregion


	#region tab

	public override void OnTabSelected()
	{
		base.OnTabSelected();

		logNote_.gameObject.SetActive(true);
		logNote_.UpdateLayoutElement();
		logNote_.OnTreeNoteSelected();

		tree_.SubscribeKeyInput();
		logNote_.SubscribeKeyInput();

		GameContext.Window.LogTabButton.OwnerNote = this;
	}

	public override void OnTabDeselected()
	{
		base.OnTabDeselected();
		
		logNote_.OnTreeNoteDeselected();
		logNote_.gameObject.SetActive(false);
	}

	public override void OnTabClosed()
	{
		List<TaggedLine> removeList = new List<TaggedLine>();
		foreach( TaggedLine taggedLine in GameContext.TagList.TaggedLines )
		{
			if( taggedLine.BindedLine.Tree == this.Tree )
			{
				removeList.Add(taggedLine);
			}
		}
		foreach( TaggedLine taggledLine in removeList )
		{
			taggledLine.Remove();
		}
		
		SaveNote();
		Destroy(this.gameObject);
		Destroy(logNote_.gameObject);
	}

	public override void OnBeginTabDrag()
	{
		if( Tree.FocusedLine != null )
		{
			Tree.FocusedLine.Field.IsFocused = false;
		}
	}

	public void OnFontSizeChanged()
	{
		tree_.RootLine.AdjustFontSizeRecursive(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
		foreach( LineField field in heapManager_ )
		{
			field.textComponent.fontSize = GameContext.Config.FontSize;
			field.RectHeight = GameContext.Config.HeightPerLine;
			field.OnTextLengthChanged();
		}
		logNote_.OnFontSizeChanged();
		UpdateLayoutElement();
		CheckScrollbarEnabled();
	}

	#endregion


	#region logtab

	public void OnLogSplitLineDragging(object sender, PointerEventData eventData)
	{
		RectTransform logNoteTransform = GameContext.Window.LogNoteTransform;
		RectTransform treeNoteTransform = GameContext.Window.TreeNoteTransform;
		logNoteTransform.anchoredPosition += new Vector2(0, eventData.delta.y);
		float height = Tab.OwnerTabGroup.NoteAreaTransform.rect.height;
		if( logNoteTransform.anchoredPosition.y < -height )
		{
			logNoteTransform.anchoredPosition = new Vector2(logNoteTransform.anchoredPosition.x, -height);
		}
		else if( logNoteTransform.anchoredPosition.y > 0 )
		{
			logNoteTransform.anchoredPosition = new Vector2(logNoteTransform.anchoredPosition.x, 0);
		}
		logNoteTransform.sizeDelta = new Vector2(logNoteTransform.sizeDelta.x, logNoteTransform.anchoredPosition.y + height);
		logNote_.OpenRatio = logNoteTransform.sizeDelta.y / height;

		treeNoteTransform.sizeDelta = new Vector2(treeNoteTransform.sizeDelta.x, height * (1.0f - logNote_.OpenRatio) - GameContext.Config.LogNoteHeaderMargin);

		CheckScrollbarEnabled();
		logNote_.CheckScrollbarEnabled();
	}

	public void OnLogSplitLineEndDrag(object sender, PointerEventData eventData)
	{
		if( logNote_.OpenRatio <= 0 )
		{
			logNote_.IsOpended = false;
		}
		else if( logNote_.OpenRatio >= 1 )
		{
			GameContext.Window.UpdateVerticalLayout();
		}
	}

	public void OpenLogNote()
	{
		if( logNote_.IsOpended == false )
		{
			logNote_.IsOpended = true;
		}
		GameContext.Window.UpdateVerticalLayout();
	}

	public void CloseLogNote()
	{
		if( logNote_.IsOpended )
		{
			logNote_.OpenRatio = 0.0f;
			logNote_.IsOpended = false;
		}
	}

	#endregion


	#region override

	public override void UpdateLayoutElement()
	{
		if( gameObject.activeInHierarchy )
		{
			layout_.preferredHeight = tree_.PreferredHeight;
			contentSizeFitter_.SetLayoutVertical();
		}
	}

	public override void ScrollTo(Line targetLine)
	{
		if( IsActive == false )
		{
			IsActive = true;
		}

		base.ScrollTo(targetLine);
	}

	#endregion


	#region file

	public void NewNote(string path, TabButton tab, LogNote logNote)
	{
		tabButton_ = tab;
		logNote_ = logNote;
		
		tree_.NewFile(new FileInfo(path));

		tabButton_.BindedNote = this;
		tabButton_.Text = tree_.TitleText;

		logNote.Initialize(this);
	}

	public void LoadNote(string path, TabButton tab, LogNote logNote)
	{
		tabButton_ = tab;
		logNote_ = logNote;

		tree_.LoadFile(new FileInfo(path));
		//if( GameContext.Config.DoBackUp && tree_.File.Exists )
		//{
		//	tree_.File.CopyTo(tree_.File.FullName + ".bak", overwrite: true);
		//}

		tabButton_.BindedNote = this;
		tabButton_.Text = tree_.TitleText;
		targetScrollValue_ = 1.0f;
		
		logNote.Initialize(this);
	}

	public void SaveNote()
	{
		if( tree_.File == null )
		{
			SaveAs();
		}
		else
		{
			tree_.SaveFile();
			logNote_.SaveLog();
		}
		saveRequestedTrees_.Clear();
	}

	public void SaveAs()
	{
		SaveFileDialog saveFileDialog = new SaveFileDialog();
		saveFileDialog.Filter = "dones file (*.dtml)|*.dtml";
		saveFileDialog.FileName = tree_.TitleText;
		DialogResult dialogResult = saveFileDialog.ShowDialog();
		if( dialogResult == DialogResult.OK )
		{
			//if( GameContext.Config.DoBackUp )
			//{
			//	DeleteBackup();
			//}

			tree_.SaveFileAs(new FileInfo(saveFileDialog.FileName));
			logNote_.Initialize(this);
			logNote_.SaveLog();
			GameContext.Window.AddRecentOpenedFiles(tree_.File.FullName);

#if UNITY_STANDALONE_WIN
			GameContext.Window.SetTitle(tree_.TitleText + " - Dones");
#endif
		}
	}

	public override void ReloadNote()
	{
		if( tree_.File != null )
		{
			tree_.ReloadFile();
			logNote_.ReloadLog();
		}
	}

	public void DeleteBackup()
	{
		if( tree_.File != null )
		{
			FileInfo backupFile = new FileInfo(tree_.File.FullName + ".bak");
			if( backupFile.Exists )
			{
				backupFile.Delete();
			}
		}
	}
	
	public bool AskDoClose()
	{
		if( IsEdited )
		{
			GameContext.Window.ModalDialog.Show(tree_.TitleText + "ファイルへの変更を保存しますか？", this.CloseConfirmCallback);
			return false;
		}

		if( logNote_.IsEdited )
		{
			GameContext.Window.ModalDialog.Show(logNote_.TitleText + "ログファイルへの変更を保存しますか？", this.CloseConfirmCallback);
			return false;
		}
		return true;
	}

	void CloseConfirmCallback(ModalDialog.DialogResult result)
	{
		switch( result )
		{
		case ModalDialog.DialogResult.Yes:
			SaveNote();
			Tab.DoClose();
			break;
		case ModalDialog.DialogResult.No:
			Tab.DoClose();
			break;
		case ModalDialog.DialogResult.Cancel:
			// do nothing
			break;
		}
	}

	#endregion
}