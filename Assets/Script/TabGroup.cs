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

	public UIGaugeRenderer SplitBar;
	public RectTransform NoteAreaTransform;
	public RectTransform NoteTitleAreaTransform;
	//public FileMenuButton FileMenu;
	public GameObject Split;

	public float DesiredTabWidth = 200.0f;
	public float DesiredTabHeight = 30.0f;

	#endregion


	#region params

	public TabButton ActiveTab { get { return activeTab_; } }
	public Note ActiveNote { get { return activeTab_ != null ? activeTab_.BindedNote : null; } }
	public TreeNote ActiveTreeNote { get { return activeTab_ != null && activeTab_.BindedNote != null ? activeTab_.BindedNote as TreeNote : null; } }

	TabButton activeTab_;
	List<TabButton> tabButtons_ = new List<TabButton>();

	//float desiredTabGroupWidth_;
	//float currentTabWidth_;

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
		//desiredTabGroupWidth_ = UnityEngine.Screen.width - GameContext.Window.TagListWidth;
		//currentTabWidth_ = DesiredTabWidth;
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
					if( tabButtons_[index - 1].CanSelect(showDialog: true) )
					{
						tabButtons_[index - 1].IsSelected = true;
					}
				}
			}
			else if( (Input.GetKeyDown(KeyCode.RightArrow) && Input.GetKey(KeyCode.LeftCommand)) || Input.GetKeyDown(KeyCode.PageDown) )
			{
				int index = tabButtons_.IndexOf(activeTab_);
				if( index < tabButtons_.Count - 1 )
				{
					if( tabButtons_[index + 1].CanSelect(showDialog: true) )
					{
						tabButtons_[index + 1].IsSelected = true;
					}
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
		UpdateTabLayout();
	}

	public void OnTabSelected(TabButton tab)
	{
		if( activeTab_ == tab )
		{
			return;
		}

		TabButton oldActiveTab = activeTab_;

		activeTab_ = tab;
		
#if UNITY_STANDALONE_WIN
		GameContext.Window.SetTitle(tab.Text + " - Dones");
#endif

		if( oldActiveTab != null )
		{
			oldActiveTab.IsSelected = false;
			if( oldActiveTab.BindedNote != activeTab_.BindedNote )
			{
				oldActiveTab.BindedNote.Deactivate();
			}
		}
		if( oldActiveTab == null || oldActiveTab.BindedNote != activeTab_.BindedNote )
		{
			activeTab_.BindedNote.Activate();
		}
	}

	public void OnTabClosed(TabButton tab)
	{
		if( tab.BindedNote is TreeNote )
		{
			GameContext.Window.AddRecentClosedTab(tab);
		}

		int index = tabButtons_.IndexOf(tab);
		tabButtons_.Remove(tab);
		if( tab == activeTab_ )
		{
			if( index >= tabButtons_.Count ) index = tabButtons_.Count - 1;
			while( tabButtons_[index].CanSelect() == false )
			{
				index--;
				if( index < 0 )
				{
					index = tabButtons_.Count - 1;
				}
			}
			tabButtons_[index].IsSelected = true;
		}

		UpdateTabLayout();
	}

	#endregion


	#region layout

	public void UpdateLayoutAll()
	{
		UpdateTabLayout();
		GameContext.Window.UpdateVerticalLayout();
		if( ActiveTreeNote != null )
		{
			ActiveTreeNote.UpdateLayoutElement();
			ActiveTreeNote.LogNote.UpdateLayoutElement();
		}
	}

	public void UpdateTabLayout()
	{
		//desiredTabGroupWidth_ = UnityEngine.Screen.width - GameContext.Window.TagListWidth;
		NoteAreaTransform.offsetMax = new Vector3(-GameContext.TagList.Width - 10, NoteAreaTransform.offsetMax.y);
		NoteTitleAreaTransform.offsetMax = new Vector3(-GameContext.TagList.Width - 10, NoteTitleAreaTransform.offsetMax.y);

		//currentTabWidth_ = DesiredTabWidth;
		//if( DesiredTabWidth * tabButtons_.Count > desiredTabGroupWidth_ )
		//{
		//	currentTabWidth_ = desiredTabGroupWidth_ / tabButtons_.Count;
		//}
		foreach( TabButton tab in tabButtons_ )
		{
			tab.Width = DesiredTabWidth;
			tab.TargetPosition = GetTabPosition(tab);
		}
		//FileMenu.TargetPosition = Vector3.down * DesiredTabHeight * tabButtons_.Count;
		Split.transform.SetAsLastSibling();
	}

	Vector3 GetTabPosition(TabButton tab)
	{
		return Vector3.down * DesiredTabHeight * (tabButtons_.IndexOf(tab));
	}

	#endregion


	#region dragging

	public void OnBeginTabDrag(TabButton tab)
	{
		//tab.IsSelected = true;
		if( tab.BindedNote is TreeNote && (tab.BindedNote as TreeNote).Tree.FocusedLine != null )
		{
			(tab.BindedNote as TreeNote).Tree.FocusedLine.Field.IsFocused = false;
		}
		tab.transform.SetAsLastSibling();
	}

	public void OnTabDragging(TabButton tab, PointerEventData eventData)
	{
		int index = tabButtons_.IndexOf(tab);
		tab.transform.localPosition += new Vector3(0, eventData.delta.y);
		if( tab.transform.localPosition.y > 0 )
		{
			tab.transform.localPosition = new Vector3(tab.transform.localPosition.x, 0);
		}
		float tabmax = -(tabButtons_.Count * DesiredTabHeight);
		if( tab.transform.localPosition.y < tabmax )
		{
			tab.transform.localPosition = new Vector3(tab.transform.localPosition.x, tabmax);
		}
		int desiredIndex = Mathf.Clamp((int)(-tab.transform.localPosition.y / DesiredTabHeight), 0, tabButtons_.Count - 1);
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
		Split.transform.SetAsLastSibling();
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

	public int IndexOf(TabButton tab)
	{
		return tabButtons_.IndexOf(tab);
	}

	#endregion

}
