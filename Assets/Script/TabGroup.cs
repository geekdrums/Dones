using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Text;

public class TabGroup : MonoBehaviour, IEnumerable<Tree>
{
	#region editor params

	public GameObject TabParent;
	public GameObject NoteParent;
	public GameObject LogNoteParent;
	public LogTabButton LogTabButton;
	public Image UnderBar;
	public GameObject OpenButton;
	public GameObject CloseButton;

	public RectTransform NoteAreaTransform;
	public RectTransform TreeNoteTransform;
	public RectTransform LogNoteTransform;

	public float DesiredTabWidth = 200.0f;

	#endregion


	#region params

	public TreeNote ActiveNote { get { return activeNote_; } }

	TreeNote activeNote_;
	List<TreeNote> trees_ = new List<TreeNote>();
	Stack<string> recentClosedFiles_ = new Stack<string>();

	float desiredTabGroupWidth_;
	float currentTabWidth_;

	#endregion


	#region unity events

	void Start()
	{
		desiredTabGroupWidth_ = UnityEngine.Screen.width - GameContext.Window.HeaderWidth;
		currentTabWidth_ = DesiredTabWidth;
	}

	// Update is called once per frame
	void Update()
	{
		// focus判定。なんか他の方法がいいかも？
		if( activeNote_ == null || activeNote_.FocusedLine == null ) return;

		bool ctrl = Input.GetKey(KeyCode.LeftControl);
		bool shift = Input.GetKey(KeyCode.LeftShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( ctrlOnly )
		{
			if( Input.GetKeyDown(KeyCode.LeftArrow) && Input.GetKey(KeyCode.LeftCommand) )
			{
				int index = trees_.IndexOf(activeNote_);
				if( index > 0 )
				{
					trees_[index - 1].IsActive = true;
				}
			}
			else if( Input.GetKeyDown(KeyCode.RightArrow) && Input.GetKey(KeyCode.LeftCommand) )
			{
				int index = trees_.IndexOf(activeNote_);
				if( index < trees_.Count - 1 )
				{
					trees_[index + 1].IsActive = true;
				}
			}
		}
		if( ctrl && shift )
		{
			if( Input.GetKeyDown(KeyCode.T) && recentClosedFiles_.Count > 0 )
			{
				LoadTree(recentClosedFiles_.Pop(), true);
			}
		}
		if( Input.GetKeyDown(KeyCode.F5) && activeNote_.File != null )
		{
			activeNote_.Reload();
			activeNote_.LogNote.Reload();
		}
	}

	#endregion


	#region files

	public void NewFile()
	{
		TabButton tab = Instantiate(GameContext.Window.TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		TreeNote treeNote = Instantiate(GameContext.Window.TreeNotePrefab.gameObject, NoteParent.transform).GetComponent<TreeNote>();
		LogNote logNote = Instantiate(GameContext.Window.LogNotePrefab.gameObject, LogNoteParent.transform).GetComponent<LogNote>();

		tab.OwnerTabGroup = this;
		treeNote.NewFile(tab, logNote);
		logNote.LoadToday(treeNote);
		treeNote.IsActive = true;
		OnTreeCreated(treeNote);
	}

	public void Save()
	{
		if( activeNote_ != null )
		{
			activeNote_.Save();
		}
	}

	public void SaveAs()
	{
		if( activeNote_ != null )
		{
			activeNote_.SaveAs();
		}
	}

	public void LoadTree(string path, bool isActive)
	{
		foreach( TreeNote existTree in trees_ )
		{
			if( existTree.File != null && existTree.File.FullName.Replace('\\', '/') == path.Replace('\\', '/') )
			{
				if( existTree != activeNote_ )
				{
					existTree.IsActive = true;
				}
				return;
			}
		}

		TabButton tab = Instantiate(GameContext.Window.TabButtonPrefab.gameObject, TabParent.transform).GetComponent<TabButton>();
		TreeNote treeNote = Instantiate(GameContext.Window.TreeNotePrefab.gameObject, NoteParent.transform).GetComponent<TreeNote>();
		LogNote logNote = Instantiate(GameContext.Window.LogNotePrefab.gameObject, LogNoteParent.transform).GetComponent<LogNote>();

		tab.OwnerTabGroup = this;
		treeNote.Load(path, tab, logNote, isActive);
		logNote.LoadToday(treeNote);
		treeNote.IsActive = isActive;
		OnTreeCreated(treeNote);
	}

	#endregion
	

	#region events

	public void OnTreeCreated(TreeNote newTree)
	{
		trees_.Add(newTree);

		UpdateHorizontalLayout();
	}

	public void OnTreeActivated(TreeNote treeNote)
	{
		if( activeNote_ != null && treeNote != activeNote_ )
		{
			activeNote_.IsActive = false;
		}
		activeNote_ = treeNote;

		UpdateLogTabButtons();
		UpdateVerticalLayout();
	}

	public void OnTreeClosed(TreeNote closedTree)
	{
		if( closedTree.File != null )
		{
			recentClosedFiles_.Push(closedTree.File.FullName);
		}

		int index = trees_.IndexOf(closedTree);
		trees_.Remove(closedTree);
		if( trees_.Count == 0 )
		{
			NewFile();
		}
		else if( closedTree == activeNote_ )
		{
			if( index >= trees_.Count ) index = trees_.Count - 1;
			trees_[index].IsActive = true;
		}

		UpdateHorizontalLayout();
	}
	
	public void OnLogNoteClosed(LogNote logNote)
	{
		if( logNote.OpenRatio >= 1.0f )
		{
			logNote.OpenRatio = 0.5f;
		}
		logNote.OnTabClosed();
		OpenButton.SetActive(true);
		CloseButton.SetActive(false);

		UpdateVerticalLayout();
	}

	public void OnLogNoteOpened(LogNote logNote)
	{
		if( logNote.OpenRatio <= 0.0f )
		{
			logNote.OpenRatio = 0.5f;
		}
		logNote.OnTabOpened();

		OpenButton.SetActive(false);
		CloseButton.SetActive(activeNote_.LogNote.IsFullArea);

		UpdateVerticalLayout();
	}

	public void OpenLogNote()
	{
		if( activeNote_.LogNote.IsOpended == false )
		{
			activeNote_.LogNote.IsOpended = true;
		}
	}

	public void CloseLogNote()
	{
		if( activeNote_.LogNote.IsOpended )
		{
			activeNote_.LogNote.OpenRatio = 0.0f;
			activeNote_.LogNote.IsOpended = false;
		}

		UpdateLogTabButtons();
	}

	#endregion


	#region layout

	public void UpdateLayoutAll()
	{
		UpdateHorizontalLayout();
		UpdateVerticalLayout();
		if( activeNote_ != null )
		{
			activeNote_.UpdateLayoutElement();
			activeNote_.LogNote.UpdateLayoutElement();
		}
	}

	public void UpdateHorizontalLayout()
	{
		desiredTabGroupWidth_ = UnityEngine.Screen.width - GameContext.Window.HeaderWidth;
		NoteAreaTransform.offsetMin = new Vector3(GameContext.Window.HeaderWidth, NoteAreaTransform.offsetMin.y);

		currentTabWidth_ = DesiredTabWidth;
		if( DesiredTabWidth * trees_.Count > desiredTabGroupWidth_ )
		{
			currentTabWidth_ = desiredTabGroupWidth_ / trees_.Count;
		}
		foreach( TreeNote tree in trees_ )
		{
			tree.Tab.Width = currentTabWidth_;
			tree.Tab.TargetPosition = GetTabPosition(tree.Tab);
		}
	}

	public void UpdateVerticalLayout()
	{
		if( activeNote_ == null || activeNote_.LogNote == null ) return;

		LogTabButton.transform.parent.gameObject.SetActive(activeNote_.LogNote.IsOpended && activeNote_.LogNote.IsFullArea == false);

		float logNoteRatio = activeNote_.LogNote.IsOpended ? activeNote_.LogNote.OpenRatio : 0.0f;
		float height = NoteAreaTransform.rect.height;

		TreeNoteTransform.sizeDelta = new Vector2(TreeNoteTransform.sizeDelta.x, height * (1.0f - logNoteRatio) - (logNoteRatio > 0.0f ? 40.0f : 0.0f));
		LogNoteTransform.sizeDelta = new Vector2(LogNoteTransform.sizeDelta.x, height * logNoteRatio);
		LogNoteTransform.anchoredPosition = new Vector2(LogNoteTransform.anchoredPosition.x, -height + LogNoteTransform.sizeDelta.y);

		activeNote_.CheckScrollbarEnabled();
		activeNote_.LogNote.CheckScrollbarEnabled();
	}
	
	public void UpdateLogTabButtons()
	{
		if( activeNote_.LogNote.IsFullArea )
		{
			OpenButton.SetActive(false);
			CloseButton.SetActive(true);
		}
		else if( activeNote_.LogNote.IsOpended == false )
		{
			OpenButton.SetActive(true);
			CloseButton.SetActive(false);
		}
		activeNote_.Tab.UpdateTitleText();
		activeNote_.Tab.UpdateColor();
	}

	Vector3 GetTabPosition(TabButton tab)
	{
		return Vector3.right * currentTabWidth_ * (trees_.IndexOf(tab.BindedNote));
	}

	#endregion


	#region dragging

	public void OnBeginTabDrag(TabButton tab)
	{
		tab.IsOn = true;
		if( tab.BindedNote.FocusedLine != null )
		{
			tab.BindedNote.FocusedLine.Field.IsFocused = false;
		}
		tab.transform.SetAsLastSibling();
	}

	public void OnTabDragging(TabButton tab, PointerEventData eventData)
	{
		int index = trees_.IndexOf(tab.BindedNote);
		tab.transform.localPosition += new Vector3(eventData.delta.x, 0);
		if( tab.transform.localPosition.x < 0 )
		{
			tab.transform.localPosition = new Vector3(0, tab.transform.localPosition.y);
		}
		float tabmax = desiredTabGroupWidth_ - currentTabWidth_;
		if( tab.transform.localPosition.x > tabmax )
		{
			tab.transform.localPosition = new Vector3(tabmax, tab.transform.localPosition.y);
		}
		int desiredIndex = Mathf.Clamp((int)(tab.transform.localPosition.x / currentTabWidth_), 0, trees_.Count - 1);
		if( index != desiredIndex )
		{
			trees_.Remove(tab.BindedNote);
			trees_.Insert(desiredIndex, tab.BindedNote);
			int sign = (int)Mathf.Sign(desiredIndex - index);
			for( int i = index; i != desiredIndex; i += sign )
			{
				AnimManager.AddAnim(trees_[i].Tab, GetTabPosition(trees_[i].Tab), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			}
		}
	}

	public void OnEndTabDrag(TabButton tab)
	{
		AnimManager.AddAnim(tab, GetTabPosition(tab), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
	}

	public void OnLogSplitLineDragging(object sender, PointerEventData eventData)
	{
		LogNoteTransform.anchoredPosition += new Vector2(0, eventData.delta.y);
		float height = NoteAreaTransform.rect.height;
		if( LogNoteTransform.anchoredPosition.y < -height )
		{
			LogNoteTransform.anchoredPosition = new Vector2(LogNoteTransform.anchoredPosition.x, -height);
		}
		else if( LogNoteTransform.anchoredPosition.y > 0 )
		{
			LogNoteTransform.anchoredPosition = new Vector2(LogNoteTransform.anchoredPosition.x, 0);
		}
		LogNoteTransform.sizeDelta = new Vector2(LogNoteTransform.sizeDelta.x, LogNoteTransform.anchoredPosition.y + height);
		activeNote_.LogNote.OpenRatio = LogNoteTransform.sizeDelta.y / height;

		TreeNoteTransform.sizeDelta = new Vector2(TreeNoteTransform.sizeDelta.x, height * (1.0f - activeNote_.LogNote.OpenRatio) - 10.0f);

		activeNote_.CheckScrollbarEnabled();
		activeNote_.LogNote.CheckScrollbarEnabled();
	}

	public void OnLogSplitLineEndDrag(object sender, PointerEventData eventData)
	{
		if( activeNote_.LogNote.OpenRatio <= 0 )
		{
			activeNote_.LogNote.IsOpended = false;
		}
		else if( activeNote_.LogNote.OpenRatio >= 1 )
		{
			UpdateLogTabButtons();
			UpdateVerticalLayout();
		}
	}

	#endregion


	#region IEnumerable<Tree>

	public IEnumerator<Tree> GetEnumerator()
	{
		foreach( Tree tree in trees_ )
		{
			yield return tree;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	public int Count { get { return trees_.Count; } }

	public TreeNote this[int index]
	{
		get
		{
			if( 0 <= index && index < Count ) return trees_[index];
			else return null;
		}
	}

	#endregion

}
