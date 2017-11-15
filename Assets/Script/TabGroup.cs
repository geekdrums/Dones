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

public class TabGroup : MonoBehaviour, IEnumerable<ITabButton>
{
	#region editor params

	public Image UnderBar;
	public RectTransform NoteAreaTransform;

	public float DesiredTabWidth = 200.0f;

	#endregion


	#region params

	public ITabButton ActiveTab { get { return activeTab_; } }
	public TreeNote ActiveNote { get { return activeTab_ is NoteTabButton ? (activeTab_ as NoteTabButton).BindedNote : null; } }

	ITabButton activeTab_;
	List<ITabButton> tabButtons_ = new List<ITabButton>();

	float desiredTabGroupWidth_;
	float currentTabWidth_;

	public IEnumerable<TreeNote> TreeNotes
	{
		get
		{
			foreach( ITabButton tab in this )
			{
				if( tab is NoteTabButton )
				{
					yield return (tab as NoteTabButton).BindedNote;
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

	public void OnTabCreated(ITabButton newTab)
	{
		newTab.OwnerTabGroup = this;
		tabButtons_.Add(newTab);
		UpdateHorizontalLayout();
	}

	public void OnTabActivated(ITabButton tab)
	{
		if( activeTab_ != null && tab != activeTab_ )
		{
			activeTab_.IsOn = false;
		}
		activeTab_ = tab;

		if( ActiveNote != null )
		{
			ActiveNote.LogNote.UpdateLogTabButtons();
			ActiveNote.UpdateVerticalLayout();
		}
	}

	public void OnTabClosed(ITabButton tab)
	{
		if( tab is NoteTabButton && (tab as NoteTabButton).BindedNote.File != null )
		{
			GameContext.Window.AddRecentClosedFiles((tab as NoteTabButton).BindedNote.File.FullName);
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
		if( ActiveNote != null )
		{
			ActiveNote.UpdateLayoutElement();
			ActiveNote.UpdateVerticalLayout();
			ActiveNote.LogNote.UpdateLayoutElement();
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
		foreach( ITabButton tab in tabButtons_ )
		{
			tab.Width = currentTabWidth_;
			tab.TargetPosition = GetTabPosition(tab);
		}
	}

	Vector3 GetTabPosition(ITabButton tab)
	{
		return Vector3.right * currentTabWidth_ * (tabButtons_.IndexOf(tab));
	}

	#endregion


	#region dragging

	public void OnBeginTabDrag(NoteTabButton tab)
	{
		tab.IsOn = true;
		if( tab.BindedNote.Tree.FocusedLine != null )
		{
			tab.BindedNote.Tree.FocusedLine.Field.IsFocused = false;
		}
		tab.transform.SetAsLastSibling();
	}

	public void OnTabDragging(NoteTabButton tab, PointerEventData eventData)
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

	public void OnEndTabDrag(NoteTabButton tab)
	{
		AnimManager.AddAnim(tab, GetTabPosition(tab), ParamType.Position, AnimType.Time, GameContext.Config.AnimTime);
	}

	#endregion


	#region IEnumerable<Tree>

	public IEnumerator<ITabButton> GetEnumerator()
	{
		foreach( ITabButton tab in tabButtons_ )
		{
			yield return tab;
		}
	}
	IEnumerator IEnumerable.GetEnumerator()
	{
		return this.GetEnumerator();
	}

	public int Count { get { return tabButtons_.Count; } }

	public ITabButton this[int index]
	{
		get
		{
			if( 0 <= index && index < Count ) return tabButtons_[index];
			else return null;
		}
	}

	#endregion

}
