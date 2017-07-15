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

// Window - [ LogNote ] - LogTree - Line
public class LogNote : MonoBehaviour
{
	public int LoadDateCount = 7;
	public GameObject LogTreePrefab;
	public GameObject DateUIPrefab; 

	public LogTree TodayTree { get { return todayTree_; } }
	private LogTree todayTree_;

	public TreeNote TreeNote { get { return treeNote_; } }
	TreeNote treeNote_;

	public LogTabButton LogTabButton { get { return TreeNote.Tab.OwnerTabGroup.LogTabButton; } }

	public float OpenRatio
	{
		get { return openRatio_; }
		set { openRatio_ = value; }
	}
	private float openRatio_ = 0.5f;

	public bool IsFullArea { get { return isOpended_ && openRatio_ >= 1.0f; } }

	public bool IsOpended
	{
		get { return isOpended_; }
		set
		{
			isOpended_ = value;
			if( value )
			{
				TreeNote.Tab.OwnerTabGroup.OnLogNoteOpened(this);
			}
			else
			{
				TreeNote.Tab.OwnerTabGroup.OnLogNoteClosed(this);
			}
		}
	}
	private bool isOpended_ = false;

	public string TitleText { get { return treeNote_ != null ? treeNote_.TitleText.Replace(".dtml", ".dones") : ""; } }

	public bool IsEdited
	{
		get
		{
			foreach( LogTree logTree in logTrees_ )
			{
				if( logTree.IsEdited )
					return true;
			}
			return false;
		}
	}

	List<LogTree> logTrees_ = new List<LogTree>();
	DateTime today_;
	DateTime endDate_;

	ActionManager actionManager_;
	GameObject heapParent_;
	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;
	ScrollRect scrollRect_;
	float targetScrollValue_ = 1.0f;
	bool isScrollAnimating_;


	#region unity events

	void Awake()
	{
		actionManager_ = new ActionManager();

		heapParent_ = new GameObject("heap");
		heapParent_.transform.parent = this.transform;
		heapParent_.SetActive(false);

		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
		scrollRect_ = GetComponentInParent<ScrollRect>();
	}

	// Update is called once per frame
	void Update()
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
	}

	#endregion


	#region input

	public void SubscribeKeyInput()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.SubscribeKeyInput();
		}
	}

	public void OnOverflowArrowInput(LogTree tree, KeyCode key)
	{
		int index = logTrees_.IndexOf(tree);
		switch(key)
		{
		case KeyCode.DownArrow:
			{
				Line next = (index < logTrees_.Count - 1 ? logTrees_[index + 1].RootLine[0] : null);
				if( next != null )
				{
					next.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.UpArrow:
			{
				Line prev = (index > 0 ? logTrees_[index - 1].RootLine.LastVisibleLine : null);
				if( prev != null )
				{
					prev.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.RightArrow:
			{
				Line next = (index < logTrees_.Count - 1 ? logTrees_[index + 1].RootLine[0] : null);
				if( next != null )
				{
					next.Field.CaretPosision = 0;
					next.Field.IsFocused = true;
				}
			}
			break;
		case KeyCode.LeftArrow:
			{
				Line prev = (index > 0 ? logTrees_[index - 1].RootLine.LastVisibleLine : null);
				if( prev != null )
				{
					prev.Field.CaretPosision = prev.TextLength;
					prev.Field.IsFocused = true;
				}
			}
			break;
		}
	}

	public void OnDoneChanged(Line line)
	{
		if( TodayTree != null )
		{
			if( line.IsDone )
			{
				TodayTree.AddLog(line);
			}
			else
			{
				TodayTree.RemoveLog(line);
			}
		}
	}

	#endregion


	#region layout

	public void ScrollTo(Line targetLine)
	{
		float scrollHeight = scrollRect_.GetComponent<RectTransform>().rect.height;
		float targetHeight = -(targetLine.Field.transform.position.y - this.transform.position.y);
		float heightPerLine = GameContext.Config.HeightPerLine;

		// focusLineが下側に出て見えなくなった場合
		float targetUnderHeight = -(targetLine.Field.transform.position.y - scrollRect_.transform.position.y) + heightPerLine / 2 - scrollHeight;
		if( targetUnderHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01(1.0f - (targetHeight + heightPerLine * 1.5f - scrollHeight) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}

		// focusLineが上側に出て見えなくなった場合
		float targetOverHeight = (targetLine.Field.transform.position.y + heightPerLine - scrollRect_.transform.position.y);
		if( targetOverHeight > 0 )
		{
			targetScrollValue_ = Mathf.Clamp01((layout_.preferredHeight - scrollHeight - targetHeight + heightPerLine) / (layout_.preferredHeight - scrollHeight));
			isScrollAnimating_ = true;
			return;
		}
	}

	public void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach( LogTree logTree in logTrees_ )
		{
			preferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight + 5;
		}
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}

	public void CheckScrollbarEnabled()
	{
		if( scrollRect_.verticalScrollbar.isActiveAndEnabled == false )
		{
			scrollRect_.verticalScrollbar.value = 1.0f;
		}
	}

	#endregion


	#region events

	public void OnTabOpened()
	{
		LoadUntil(today_.AddDays(-LoadDateCount));
		TreeNote.Tab.UpdateTitleText();
		TreeNote.Tab.UpdateColor();
	}

	public void OnTabClosed()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree != todayTree_ )
			{
				Destroy(logTree.gameObject);
			}
		}
		logTrees_.RemoveAll((LogTree logTree) => logTree != todayTree_);
		endDate_ = today_;
		TreeNote.Tab.UpdateTitleText();
		TreeNote.Tab.UpdateColor();
	}

	public void OnFontSizeChanged(int fontSize, float heightPerLine)
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.RootLine.AdjustFontSizeRecursive(fontSize, heightPerLine);
		}
		UpdateLayoutElement();
	}

	#endregion


	#region file

	public void LoadToday(TreeNote treeNote)
	{
		treeNote_ = treeNote;

		today_ = DateTime.Now.Date;
		endDate_ = today_;

		todayTree_ = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, todayTree_.transform).GetComponent<DateUI>();
		dateUI.Set(today_, ToColor(today_));
		todayTree_.Initialize(actionManager_, heapParent_);
		if( treeNote_.File != null )
		{
			todayTree_.Load(ToFileName(treeNote_.File, today_), today_);
		}
		else
		{
			todayTree_.NewTree(today_);
		}
		logTrees_.Add(todayTree_);

		LogTabButton.Text = TitleText;
	}

	public void LoadUntil(DateTime endDate)
	{
		if( treeNote_.File == null ) return;

		DateTime date = endDate_;
		while( date > endDate )
		{
			date = date.AddDays(-1.0);
			string filename = ToFileName(treeNote_.File, date);
			if( File.Exists(filename) )
			{
				LogTree logTree = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
				DateUI dateUI = Instantiate(DateUIPrefab.gameObject, logTree.transform).GetComponent<DateUI>();
				dateUI.Set(date, ToColor(date));
				logTree.Initialize(actionManager_, heapParent_);
				logTree.Load(filename, date);
				logTree.SubscribeKeyInput();
				logTrees_.Add(logTree);
			}
		}
		endDate_ = endDate;
	}

	public void Save()
	{
		if( treeNote_.File == null )
		{
			Debug.LogError(treeNote_.ToString() + " doesn't have target File!");
			return;
		}

		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree.IsEdited )
			{
				logTree.Save();
			}
		}

		LogTabButton.Text = TitleText;
	}
	
	public void Reload()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.Reload();
		}
		UpdateLayoutElement();
	}

	public Color ToColor(DateTime date)
	{
		if( date.DayOfWeek == DayOfWeek.Sunday ) return GameContext.Config.AccentColor;
		else if( date.DayOfWeek == DayOfWeek.Saturday ) return GameContext.Config.AccentColor;
		else if( date == today_ ) return GameContext.Config.DoneColor;
		else return GameContext.Config.TextColor;
	}

	public static string ToFileName(FileInfo treeFile, DateTime date)
	{
		return String.Format("{0}/{1}.dones/{1}{2}.dtml", treeFile.DirectoryName, treeFile.Name.Replace(".dtml", ""), date.ToString("-yyyy-MM-dd"));
	}

	#endregion
}
