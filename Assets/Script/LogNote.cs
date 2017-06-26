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
	
	public LogTabButton Tab { get { return tabButton_; } }
	protected LogTabButton tabButton_;


	public float OpenRatio
	{
		get { return openRatio_; }
		set { openRatio_ = value; }
	}
	private float openRatio_ = 0.5f;

	public bool IsOpended
	{
		get { return isOpended_; }
		set
		{
			isOpended_ = value;
			if( value )
			{
				OnTabOpened();
			}
			else
			{
				OnTabClosed();
			}
		}
	}
	private bool isOpended_ = false;

	public string TitleText { get { return treeNote_ != null ? treeNote_.TitleText.Replace(".dtml", ".dones") : ""; } }

	List<LogTree> logTrees_ = new List<LogTree>();
	DateTime today_;
	DateTime endDate_;

	ActionManager actionManager_;
	GameObject heapParent_;
	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;

	void Awake()
	{
		actionManager_ = new ActionManager();

		heapParent_ = new GameObject("heap");
		heapParent_.transform.parent = this.transform;
		heapParent_.SetActive(false);

		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
	}

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


	#region events

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
	
	public void OnTabOpened()
	{
		tabButton_.gameObject.SetActive(treeNote_.IsActive);
		LoadUntil(today_.AddDays(-LoadDateCount));
		GameContext.Window.OnLogTabOpened();
	}

	public void OnTabClosed()
	{
		GameContext.Window.OnLogTabClosed(this);
		tabButton_.gameObject.SetActive(false);
		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree != todayTree_ )
			{
				Destroy(logTree.gameObject);
			}
		}
		logTrees_.RemoveAll((LogTree logTree) => logTree != todayTree_);
		endDate_ = today_;
	}

	#endregion


	#region file

	public void LoadToday(TreeNote treeNote, LogTabButton tabButton)
	{
		treeNote_ = treeNote;
		tabButton_ = tabButton;

		today_ = DateTime.Now.Date;
		endDate_ = today_;

		todayTree_ = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, todayTree_.transform).GetComponent<DateUI>();
		dateUI.Set(today_);
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

		tabButton_.BindedLogNote = this;
		tabButton_.Text = TitleText;
		tabButton_.gameObject.SetActive(treeNote_.IsActive);
	}

	public void LoadUntil(DateTime endDate)
	{
		DateTime date = endDate_;
		while( date > endDate )
		{
			date = date.AddDays(-1.0);
			string filename = ToFileName(treeNote_.File, date);
			if( File.Exists(filename) )
			{
				LogTree logTree = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
				DateUI dateUI = Instantiate(DateUIPrefab.gameObject, logTree.transform).GetComponent<DateUI>();
				dateUI.Set(date);
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

		tabButton_.Text = TitleText;
	}

	public static string ToFileName(FileInfo treeFile, DateTime date)
	{
		return String.Format("{0}/{1}.dones/{1}{2}.dtml", treeFile.DirectoryName, treeFile.Name.Replace(".dtml", ""), date.ToString("-yyyy-MM-dd"));
	}

	#endregion
}
