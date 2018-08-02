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

	public bool IsEdited { get { return tree_.IsEdited; } }
	public FileInfo File { get { return tree_ != null ? tree_.File : null; } }

	public LogNote LogNote;

	public TagText TagTextPrefab;

	protected HeapManager<TagText> tagHeapManager_;

	#endregion

	protected override void Awake()
	{
		base.Awake();
		tagHeapManager_ = new HeapManager<TagText>();
		tagHeapManager_.Initialize(1, TagTextPrefab);
		tree_ = GetComponent<Tree>();
		tree_.Initialize(this, new ActionManagerProxy(actionManager_), heapManager_, tagHeapManager_);
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
			LogNote.IsOpended = !LogNote.IsOpended;
		}
	}

	#region event
	
	void OnDoneChanged(object sender, EventArgs e)
	{
		Line line = sender as Line;
		LogNote.OnDoneChanged(line);
	}

	#endregion


	#region tab

	public override void Activate()
	{
		base.Activate();

		LogNote.gameObject.SetActive(true);
		LogNote.UpdateLayoutElement();

		tree_.SubscribeKeyInput();
		LogNote.SubscribeKeyInput();

		GameContext.Window.LogTabButton.OwnerNote = this;
	}

	public override void Deactivate()
	{
		base.Deactivate();
		
		LogNote.gameObject.SetActive(false);
	}
	
	public override void SetNoteViewParam(NoteViewParam param)
	{
		Tree.SetPath(param.Path);
		scrollRect_.verticalScrollbar.value = param.TargetScrollValue;
		LogNote.SetNoteViewParam(param);
	}


	public override void CacheNoteViewParam(NoteViewParam param)
	{
		base.CacheNoteViewParam(param);
		LogNote.CacheNoteViewParam(param);
	}

	public override void Destroy()
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
		Destroy(LogNote.gameObject);
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
		foreach( TagText tagText in tagHeapManager_ )
		{
			tagText.TextComponent.fontSize = GameContext.Config.FontSize;
		}
		LogNote.OnFontSizeChanged();
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
		float height = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
		if( logNoteTransform.anchoredPosition.y < -height )
		{
			logNoteTransform.anchoredPosition = new Vector2(logNoteTransform.anchoredPosition.x, -height);
		}
		else if( logNoteTransform.anchoredPosition.y > 0 )
		{
			logNoteTransform.anchoredPosition = new Vector2(logNoteTransform.anchoredPosition.x, 0);
		}
		logNoteTransform.sizeDelta = new Vector2(logNoteTransform.sizeDelta.x, logNoteTransform.anchoredPosition.y + height);
		LogNote.OpenRatio = logNoteTransform.sizeDelta.y / height;

		treeNoteTransform.sizeDelta = new Vector2(treeNoteTransform.sizeDelta.x, height * (1.0f - LogNote.OpenRatio) - GameContext.Config.LogNoteHeaderMargin);

		CheckScrollbarEnabled();
		LogNote.CheckScrollbarEnabled();
	}

	public void OnLogSplitLineEndDrag(object sender, PointerEventData eventData)
	{
		if( LogNote.OpenRatio <= 0 )
		{
			LogNote.IsOpended = false;
		}
		else if( LogNote.OpenRatio >= 1 )
		{
			GameContext.Window.UpdateVerticalLayout();
		}
	}

	public void OpenLogNote()
	{
		if( LogNote.IsOpended == false )
		{
			LogNote.IsOpended = true;
		}
		GameContext.Window.UpdateVerticalLayout();
	}

	public void CloseLogNote()
	{
		if( LogNote.IsOpended )
		{
			LogNote.OpenRatio = 0.0f;
			LogNote.IsOpended = false;
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

	#endregion


	#region file

	public void LoadNote(string path)
	{
		tree_.LoadFile(new FileInfo(path));
		targetScrollValue_ = 1.0f;
	}

	public void SaveNote()
	{
		tree_.SaveFile();
		LogNote.SaveLog();
		saveRequestedTrees_.Clear();
	}

	public override void ReloadNote()
	{
		if( tree_.File != null )
		{
			tree_.ReloadFile();
			LogNote.ReloadLog();
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

	#endregion
}