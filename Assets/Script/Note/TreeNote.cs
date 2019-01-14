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

// Window - [ TreeNote ] - Line
public class TreeNote : Note
{
	#region properties
	
	public Tree Tree { get { return tree_; } }
	protected Tree tree_;

	protected Tree focusedTree_;

	public bool IsEdited { get { return tree_.IsEdited; } }
	public FileInfo File { get { return tree_ != null ? tree_.File : null; } }

	public LogNote LogNote;
	
	public LineField LinePrefab;
	public TagText TagTextPrefab;
	public Text TitlePathPrefab;

	protected HeapManager<LineField> heapManager_ = new HeapManager<LineField>();
	protected HeapManager<TagText> tagHeapManager_ = new HeapManager<TagText>();
	protected GameObject heapParentObject_;
	protected GameObject tagHeapParentObject_;

	#endregion

	protected override void Awake()
	{
		base.Awake();
		
		heapParentObject_ = new GameObject("LineHeap");
		heapParentObject_.transform.SetParent(this.transform);
		heapParentObject_.SetActive(false);
		heapManager_.Initialize(GameContext.Config.LineHeapCount, LinePrefab, heapParentObject_.transform, useFromFirst: true); // lastのほうにFoldされたLineとかが溜まっていくので、Reviveする可能性が高いため0番目のフリーな要素から使っていく

		tagHeapParentObject_ = new GameObject("TagHeap");
		tagHeapParentObject_.transform.SetParent(this.transform);
		tagHeapParentObject_.SetActive(false);
		tagHeapManager_.Initialize(1, TagTextPrefab, tagHeapParentObject_.transform);

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
			LogNote.ChangeOpenState();
		}
		
		if( Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) )
		{
			Tree oldFocusedTree = focusedTree_;
			focusedTree_ = null;
			Vector2 mousePosition = Input.mousePosition;
			if( Rect.Contains(mousePosition) )
			{
				Tree.OnTreeFocused(mousePosition);
				focusedTree_ = Tree;
			}
			else
			{
				Rect logNoteRect = LogNote.Rect;
				if( logNoteRect.Contains(mousePosition) )
				{
					foreach( LogTree logTree in LogNote.LogTrees )
					{
						Rect logTreeRect = logTree.Rect;
						if( logTreeRect.Contains(mousePosition) )
						{
							logTree.OnTreeFocused(mousePosition);
							focusedTree_ = logTree;
							break;
						}
						else if( logNoteRect.yMin > logTreeRect.yMax )
						{
							break;
						}
					}
				}
			}

			if( oldFocusedTree != null && oldFocusedTree != focusedTree_ )
			{
				oldFocusedTree.UpdateMouseInput(ctrl, shift, alt);
			}
		}

		if( focusedTree_ != null && focusedTree_.RootLine != null )
		{
			focusedTree_.UpdateKeyboardInput(ctrl, shift, alt);

			if( GameContext.Window.ContextMenu.gameObject.activeInHierarchy == false )
			{
				focusedTree_.UpdateMouseInput(ctrl, shift, alt);
			}
			if( Input.GetMouseButtonUp(1) )
			{
				GameContext.Window.ContextMenu.Open(focusedTree_, Input.mousePosition);
			}
		}
	}

	#region event
	
	void OnDoneChanged(object sender, EventArgs e)
	{
		Line line = sender as Line;
		LogNote.OnDoneChanged(line);
	}

	#endregion


	#region layout

	public void UpdateVerticalLayout()
	{
		float totalAreaHight = GameContext.Window.TabGroup.NoteAreaTransform.rect.height;
		scrollRectTransform_.sizeDelta = new Vector2(scrollRectTransform_.sizeDelta.x, totalAreaHight - LogNote.Rect.size.y - GameContext.Config.LogNoteHeaderMargin);

		CheckScrollbarEnabled();
	}

	#endregion


	#region tab

	public override void Activate()
	{
		base.Activate();
		GameContext.CurrentActionManager = actionManager_;

		LogNote.gameObject.SetActive(true);
		LogNote.UpdateLayoutElement();

		tree_.SubscribeKeyInput();
		LogNote.SubscribeKeyInput();

		focusedTree_ = Tree;
		if( Tree.TitleLine != null && Tree.TitleLine.Count > 0 )
		{
			Tree.TitleLine[0].Field.Select();
		}
	}

	public override void Deactivate()
	{
		base.Deactivate();
		
		LogNote.gameObject.SetActive(false);
	}
	
	public override void SetNoteViewParam(NoteViewParam param)
	{
		Tree.SetPath(param.Path);
		Tree.TitleLine[0].Field.Select();
		UpdateTreePathList(param.Path);

		LogNote.SetNoteViewParam(param);
		LogNote.UpdateTitleText(param.Path);

		actionManager_.SetTitleLine(Tree.TitleLine);
		scrollRect_.verticalScrollbar.value = param.TargetScrollValue;
	}

	public override void CacheNoteViewParam(NoteViewParam param)
	{
		base.CacheNoteViewParam(param);
		LogNote.CacheNoteViewParam(param);
	}

	void UpdateTreePathList(TreePath path)
	{
		List<Text> titlePathList = new List<Text>(GameContext.Window.TitlePath.GetComponentsInChildren<Text>(includeInactive: true));
		
		// 足りなかったら足す
		while( titlePathList.Count < path.Length )
		{
			titlePathList.Add(Instantiate(TitlePathPrefab.gameObject, GameContext.Window.TitlePath.transform).GetComponent<Text>());
		}
		
		// 文字列設定、余計な分はActiveを切る
		for( int i = 0; i < titlePathList.Count; ++i )
		{
			titlePathList[i].gameObject.SetActive(i < path.Length);
			if( i < path.Length )
			{
				bool isLastPath = i == path.Length - 1;
				titlePathList[i].text = path[i];
				titlePathList[i].color = (isLastPath ? GameContext.Config.TextColor : GameContext.Config.DoneTextColor);
				GameContext.TextLengthHelper.AbbreviateText(titlePathList[i], GameContext.Config.TabTextMaxWidth);
				UnityEngine.UI.Button button = titlePathList[i].GetComponent<UnityEngine.UI.Button>();
				button.enabled = isLastPath == false;
				button.onClick.RemoveAllListeners();
				int length = i + 1;
				button.onClick.AddListener(() =>
				{
					GameContext.Window.TabGroup.AddTab(this, path.GetPartialPath(length));
				});
			}
		}
		
		// 位置設定
		float x = 55;
		float margin = 12;
		float triangleWidth = 12;
		for( int i = 0; i < path.Length; ++i )
		{
			float width = TextLengthHelper.GetFullTextRectLength(titlePathList[i]);
			titlePathList[i].transform.localPosition = new Vector3(x, titlePathList[i].transform.localPosition.y, 0);
			titlePathList[i].rectTransform.sizeDelta = new Vector2(width, titlePathList[i].rectTransform.sizeDelta.y);
			x += width;
			x += margin;
			Image triangle = titlePathList[i].GetComponentInChildren<Image>();
			triangle.transform.localPosition = new Vector3(width + margin, triangle.transform.localPosition.y, 0);
			x += triangleWidth;
		}
		GameContext.Window.SearchField.transform.localPosition = new Vector3(x, GameContext.Window.SearchField.transform.localPosition.y, 0);
	}

	public override void Destroy()
	{
		GameContext.Window.TagList.ClearAll();		
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


	#region override

	public override void UpdateLayoutElement()
	{
		if( gameObject.activeInHierarchy )
		{
			layout_.preferredHeight = tree_.GetPreferredHeight() + +GameContext.Config.HeightPerLine;
			contentSizeFitter_.SetLayoutVertical();
		}
	}

	#endregion


	#region file

	public void LoadNote(string path)
	{
		tree_.LoadFile(new FileInfo(path));

		if( GameContext.Config.DoBackUp && tree_.File.Exists )
		{
			tree_.File.CopyTo(tree_.File.FullName + ".bak", overwrite: true);
		}

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
			TreePath path = tree_.Path;

			tree_.ReloadFile();

			Tree.SetPath(path);
			UpdateTreePathList(path);
			actionManager_.SetTitleLine(Tree.TitleLine);

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