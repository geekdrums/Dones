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

public class TabGroup : MonoBehaviour, IEnumerable<TabButton>
{
	#region editor params

	public Image UnderBar;
	public RectTransform NoteAreaTransform;

	public float DesiredTabWidth = 200.0f;

	#endregion


	#region params

	public TabButton ActiveTab { get { return activeTab_; } }
	public Note ActiveNote { get { return activeTab_ != null ? activeTab_.BindedNote : null; } }
	public TreeNote ActiveTreeNote { get { return activeTab_ != null && activeTab_.BindedNote != null ? activeTab_.BindedNote as TreeNote : null; } }
	public DiaryNote ExistDiaryNote
	{
		get
		{
			TabButton diaryTab = tabButtons_.Find((TabButton tab) => tab.BindedNote is DiaryNote);
			if( diaryTab != null ) return diaryTab.BindedNote as DiaryNote;
			else return null;
		}
	}

	TabButton activeTab_;
	List<TabButton> tabButtons_ = new List<TabButton>();

	float desiredTabGroupWidth_;
	float currentTabWidth_;

	public IEnumerable<TreeNote> TreeNotes
	{
		get
		{
			foreach( TabButton tab in this )
			{
				if( tab.BindedNote is TreeNote )
				{
					yield return tab.BindedNote as TreeNote;
				}
			}
		}
	}

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
		if( activeTab_ == null ) return;
		
		bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
		bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
		bool ctrlOnly = ctrl && !alt && !shift;

		if( ctrlOnly )
		{
			if( (Input.GetKeyDown(KeyCode.LeftArrow) && Input.GetKey(KeyCode.LeftCommand)) || Input.GetKeyDown(KeyCode.PageUp) )
			{
				int index = tabButtons_.IndexOf(activeTab_);
				if( index > 0 )
				{
					tabButtons_[index - 1].IsOn = true;
				}
			}
			else if( (Input.GetKeyDown(KeyCode.RightArrow) && Input.GetKey(KeyCode.LeftCommand)) || Input.GetKeyDown(KeyCode.PageDown) )
			{
				int index = tabButtons_.IndexOf(activeTab_);
				if( index < tabButtons_.Count - 1 )
				{
					tabButtons_[index + 1].IsOn = true;
				}
			}
		}
	}

	#endregion

	

	#region events

	public void OnTabCreated(TabButton newTab)
	{
		newTab.OwnerTabGroup = this;
		tabButtons_.Add(newTab);
		UpdateHorizontalLayout();
	}

	public void OnTabActivated(TabButton tab)
	{
		if( activeTab_ != null && tab != activeTab_ )
		{
			activeTab_.IsOn = false;
		}
		activeTab_ = tab;
		GameContext.Window.UpdateVerticalLayout();
	}

	public void OnTabClosed(TabButton tab)
	{
		if( tab.BindedNote is TreeNote && (tab.BindedNote as TreeNote).File != null )
		{
			GameContext.Window.AddRecentClosedFiles((tab.BindedNote as TreeNote).File.FullName);
		}

		int index = tabButtons_.IndexOf(tab);
		tabButtons_.Remove(tab);
		if( tab == activeTab_ )
		{
			if( index >= tabButtons_.Count ) index = tabButtons_.Count - 1;
			tabButtons_[index].IsOn = true;
		}

		UpdateHorizontalLayout();
	}

	#endregion


	#region layout

	public void UpdateLayoutAll()
	{
		UpdateHorizontalLayout();
		GameContext.Window.UpdateVerticalLayout();
		if( ActiveTreeNote != null )
		{
			ActiveTreeNote.UpdateLayoutElement();
			ActiveTreeNote.LogNote.UpdateLayoutElement();
		}
	}

	public void UpdateHorizontalLayout()
	{
		desiredTabGroupWidth_ = UnityEngine.Screen.width - GameContext.Window.HeaderWidth;
		NoteAreaTransform.offsetMin = new Vector3(GameContext.Window.HeaderWidth, NoteAreaTransform.offsetMin.y);

		currentTabWidth_ = DesiredTabWidth;
		if( DesiredTabWidth * tabButtons_.Count > desiredTabGroupWidth_ )
		{
			currentTabWidth_ = desiredTabGroupWidth_ / tabButtons_.Count;
		}
		foreach( TabButton tab in tabButtons_ )
		{
			tab.Width = currentTabWidth_;
			tab.TargetPosition = GetTabPosition(tab);
		}
	}

	Vector3 GetTabPosition(TabButton tab)
	{
		return Vector3.right * currentTabWidth_ * (tabButtons_.IndexOf(tab));
	}

	#endregion


	#region dragging

	public void OnBeginTabDrag(TabButton tab)
	{
		tab.IsOn = true;
		if( tab.BindedNote is TreeNote && (tab.BindedNote as TreeNote).Tree.FocusedLine != null )
		{
			(tab.BindedNote as TreeNote).Tree.FocusedLine.Field.IsFocused = false;
		}
		tab.transform.SetAsLastSibling();
	}

	public void OnTabDragging(TabButton tab, PointerEventData eventData)
	{
		int index = tabButtons_.IndexOf(tab);
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
		int desiredIndex = Mathf.Clamp((int)(tab.transform.localPosition.x / currentTabWidth_), 0, tabButtons_.Count - 1);
		if( index != desiredIndex )
		{
			tabButtons_.Remove(tab);
			tabButtons_.Insert(desiredIndex, tab);
			int sign = (int)Mathf.Sign(desiredIndex - index);
			for( int i = index; i != desiredIndex; i += sign )
			{
				AnimManager.AddAnim(tabButtons_[i].gameObject, GetTabPosition(tabButtons_[i]), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
			}
		}
	}

	public void OnEndTabDrag(TabButton tab)
	{
		AnimManager.AddAnim(tab, GetTabPosition(tab), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
	}

	#endregion


	#region IEnumerable<Tree>

	public IEnumerator<TabButton> GetEnumerator()
	{
		foreach( TabButton tab in tabButtons_ )
		{
			yield return tab;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	public int Count { get { return tabButtons_.Count; } }

	public TabButton this[int index]
	{
		get
		{
			if( 0 <= index && index < Count ) return tabButtons_[index];
			else return null;
		}
	}

	#endregion

}
