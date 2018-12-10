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
			LogNote.ChangeOpenState();
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
		RectTransform treeNoteTransform = scrollRect_.GetComponent<RectTransform>();

		float totalAreaHight = GameContext.Window.MainTabGroup.NoteAreaTransform.rect.height;
		treeNoteTransform.sizeDelta = new Vector2(treeNoteTransform.sizeDelta.x, totalAreaHight - LogNote.Height - GameContext.Config.LogNoteHeaderMargin);

		CheckScrollbarEnabled();
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
	}

	public override void Deactivate()
	{
		base.Deactivate();
		
		LogNote.gameObject.SetActive(false);
	}
	
	public override void SetNoteViewParam(NoteViewParam param)
	{
		Tree.SetPath(param.Path);
		UpdateTitleLine(param.Path);

		LogNote.SetNoteViewParam(param);
		LogNote.UpdateTitleLine(param.Path);

		actionManager_.SetTitleLine(Tree.TitleLine);
		scrollRect_.verticalScrollbar.value = param.TargetScrollValue;
	}

	public override void CacheNoteViewParam(NoteViewParam param)
	{
		base.CacheNoteViewParam(param);
		LogNote.CacheNoteViewParam(param);
	}

	void UpdateTitleLine(TreePath path)
	{
		List<Text> textList = new List<Text>(GameContext.Window.TitleLine.GetComponentsInChildren<Text>(includeInactive: true));
		List<UIMidairPrimitive> triangleList = new List<UIMidairPrimitive>(GameContext.Window.TitleLine.GetComponentsInChildren<UIMidairPrimitive>(includeInactive: true));

		while( textList.Count < path.Length + 1 )
		{
			textList.Add(Instantiate(textList[0].gameObject, GameContext.Window.TitleLine.transform).GetComponent<Text>());
			triangleList.Add(Instantiate(triangleList[0].gameObject, GameContext.Window.TitleLine.transform).GetComponent<UIMidairPrimitive>());
		}

		UnityEngine.UI.Button button = textList[0].GetComponent<UnityEngine.UI.Button>();
		button.onClick.RemoveAllListeners();
		button.onClick.AddListener(() =>
		{
			GameContext.Window.HomeTabButton.OnClick();
		});
		textList.RemoveAt(0);// home ボタンを残す
		triangleList[0].gameObject.SetActive(path.Length > 0);
		triangleList.RemoveAt(0);

		for( int i = 0; i < textList.Count; ++i )
		{
			textList[i].gameObject.SetActive(i < path.Length);
			triangleList[i].gameObject.SetActive(i < path.Length - 1);
			if( i < path.Length )
			{
				bool isLastPath = i == path.Length - 1;
				textList[i].text = path[i];
				textList[i].color = (isLastPath ? GameContext.Config.TextColor : GameContext.Config.DoneTextColor);
				button = textList[i].GetComponent<UnityEngine.UI.Button>();
				button.enabled = isLastPath == false;
				button.onClick.RemoveAllListeners();
				int length = i + 1;
				button.onClick.AddListener(() =>
				{
					GameContext.Window.AddTab(path.GetPartialPath(length));
				});
			}
		}

		if( path.Length > 0 )
		{
			StartCoroutine(UpdateTitleLineTextLengthCoroutine(textList[0].cachedTextGenerator));
		}
	}

	IEnumerator UpdateTitleLineTextLengthCoroutine(TextGenerator gen)
	{
		yield return new WaitWhile(() => gen.characterCount == 0);

		List<Text> textList = new List<Text>(GameContext.Window.TitleLine.GetComponentsInChildren<Text>());
		List<UIMidairPrimitive> triangleList = new List<UIMidairPrimitive>(GameContext.Window.TitleLine.GetComponentsInChildren<UIMidairPrimitive>());

		float x = 0;
		float margin = 12;
		float triangleWidth = 12;
		for( int i = 0; i < textList.Count; ++i )
		{
			float width = LineField.CalcTextRectLength(textList[i].cachedTextGenerator, textList[i].text.Length);
			textList[i].transform.localPosition = new Vector3(x, textList[i].transform.localPosition.y, 0);
			x += width;
			x += margin;
			if( i < triangleList.Count )
			{
				triangleList[i].transform.localPosition = new Vector3(x, triangleList[i].transform.localPosition.y, 0);
				x += triangleWidth;
			}
		}
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