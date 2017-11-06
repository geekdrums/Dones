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
public class LogNote : Note
{
	public int LoadDateCount = 7;
	public GameObject LogTreePrefab;
	public GameObject DateUIPrefab;
	public DateUI EndDateUI;

	public LogTree TodayTree { get { return todayTree_; } }
	private LogTree todayTree_;

	public TreeNote TreeNote { get { return treeNote_; } }
	TreeNote treeNote_;

	public LogNoteTabButton LogTabButton { get { return GameContext.Window.LogTabButton; } }
	public GameObject OpenButton { get { return GameContext.Window.OpenLogNoteButton; } }
	public GameObject CloseButton { get { return GameContext.Window.CloseLogNoteButton; } }

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
				if( OpenRatio <= 0.0f )
				{
					OpenRatio = 0.5f;
				}
				OnTabOpened();
			}
			else
			{
				if( OpenRatio >= 1.0f )
				{
					OpenRatio = 0.5f;
				}
				OnTabClosed();
			}
		}
	}
	private bool isOpended_ = false;

	public string TitleText { get { return treeNote_ != null ? treeNote_.Tree.TitleText.Replace(".dtml", ".dones") : ""; } }

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

	public override void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.UpdateLayoutElement();
			preferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight + 5;
			if( logTree.GetComponentInParent<DateUI>() != null )
			{
				logTree.GetComponentInParent<DateUI>().GetComponent<LayoutElement>().preferredHeight = logTree.GetComponent<LayoutElement>().preferredHeight;
				logTree.GetComponentInParent<DateUI>().GetComponent<ContentSizeFitter>().SetLayoutVertical();
			}
		}
		preferredHeight += 100;
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}

	#endregion


	#region events

	public void OnTreeNoteSelected()
	{
		scrollRect_.verticalScrollbar.value = targetScrollValue_;
	}

	public void OnTreeNoteDeselected()
	{
		targetScrollValue_ = scrollRect_.verticalScrollbar.gameObject.activeInHierarchy ? scrollRect_.verticalScrollbar.value : 1.0f;
	}

	public void OnTabOpened()
	{
		LoadUntil(today_.AddDays(-LoadDateCount));
		UpdateLogTabButtons();
	}

	public void OnTabClosed()
	{
		UpdateLogTabButtons();
	}

	public void UpdateLogTabButtons()
	{
		if( IsFullArea )
		{
			OpenButton.SetActive(false);
			CloseButton.SetActive(true);
		}
		else if( IsOpended == false )
		{
			OpenButton.SetActive(true);
			CloseButton.SetActive(false);
		}
		TreeNote.UpdateVerticalLayout();
		TreeNote.Tab.UpdateTitleText();
		TreeNote.Tab.UpdateColor();
	}

	public void OnFontSizeChanged()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.RootLine.AdjustFontSizeRecursive(GameContext.Config.FontSize, GameContext.Config.HeightPerLine);
			logTree.UpdateLayoutElement();
		}
		if( gameObject.activeInHierarchy )
		{
			UpdateLayoutElement();
		}
	}
	
	public void OnEditChanged(object sender, EventArgs e)
	{
		LogTree logTree = sender as LogTree;
		if( TreeNote != null )
		{
			if( IsFullArea )
			{
				if( logTree.IsEdited )
				{
					TreeNote.Tab.Text = TitleText + "*";
				}
				else if( IsEdited == false )
				{
					TreeNote.Tab.Text = TitleText;
				}
			}
			else
			{
				if( logTree.IsEdited )
				{
					LogTabButton.Text = TitleText + "*";
				}
				else if( IsEdited == false )
				{
					LogTabButton.Text = TitleText;
				}
			}
		}
	}

	#endregion


	#region file

	public void LoadToday(TreeNote treeNote)
	{
		treeNote_ = treeNote;

		today_ = DateTime.Now.Date;
		endDate_ = today_;

		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
		todayTree_ = Instantiate(LogTreePrefab.gameObject, dateUI.transform).GetComponent<LogTree>();
		dateUI.Set(today_, GameContext.Config.DoneColor);
		todayTree_.Initialize(this, actionManager_, heapFields_);
		todayTree_.OnEditChanged += this.OnEditChanged;
		if( treeNote_.File != null )
		{
			todayTree_.LoadLog(new FileInfo(ToFileName(treeNote_, today_)), today_);
		}
		else
		{
			todayTree_.NewLog(today_);
		}
		logTrees_.Add(todayTree_);

		LogTabButton.Text = TitleText;
	}

	public void LoadUntil(DateTime endDate)
	{
		if( treeNote_.File == null ) return;

		DateTime date = endDate_;
		DateUI lastDateUI = logTrees_.Count == 0 ? null : logTrees_[logTrees_.Count - 1].GetComponentInChildren<DateUI>();
		while( date > endDate )
		{
			date = date.AddDays(-1.0);
			string filename = ToFileName(treeNote_, date);
			bool exist = File.Exists(filename);
			if( lastDateUI != null )
			{
				lastDateUI.SetEnableAddDateButtton(exist == false);
			}
			if( exist )
			{
				DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
				LogTree logTree = Instantiate(LogTreePrefab.gameObject, dateUI.transform).GetComponent<LogTree>();
				dateUI.Set(date, ToColor(date));
				logTree.Initialize(this, actionManager_, heapFields_);
				logTree.LoadLog(new FileInfo(filename), date);
				logTree.SubscribeKeyInput();
				logTree.OnEditChanged += this.OnEditChanged;
				logTrees_.Add(logTree);

				lastDateUI = dateUI;
			}
			else
			{
				lastDateUI = null;
			}
		}
		if( lastDateUI != null )
		{
			lastDateUI.SetEnableAddDateButtton(false);
		}
		endDate_ = endDate;
		EndDateUI.Set(endDate.AddDays(-1), GameContext.Config.CommentTextColor);
		EndDateUI.transform.parent.SetAsLastSibling();
		UpdateLayoutElement();
	}

	public void AddDate(DateTime date)
	{
		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
		LogTree newDateTree = Instantiate(LogTreePrefab.gameObject, dateUI.transform).GetComponent<LogTree>();
		dateUI.Set(date, ToColor(date));
		dateUI.SetEnableAddDateButtton(treeNote_.File == null || File.Exists(ToFileName(treeNote_, date.AddDays(-1.0))) == false);
		newDateTree.Initialize(this, actionManager_, heapFields_);
		newDateTree.OnEditChanged += this.OnEditChanged;
		newDateTree.NewLog(date);
		newDateTree.SubscribeKeyInput();
		SetSortedIndex(newDateTree);
	}

	public void SetSortedIndex(LogTree logTree)
	{
		if( logTrees_.Contains(logTree) )
		{
			logTrees_.Remove(logTree);
		}

		int insertIndex = logTrees_.Count;
		for( int i = 0; i < logTrees_.Count; ++i )
		{
			if( logTrees_[i].Date < logTree.Date )
			{
				insertIndex = i;
				break;
			}
		}
		logTrees_.Insert(insertIndex, logTree);
		logTree.GetComponentInParent<DateUI>().transform.SetSiblingIndex(insertIndex);
		UpdateLayoutElement();
	}

	public void SaveLog()
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
				logTree.SaveFile();
			}
		}

		LogTabButton.Text = TitleText;
	}
	
	public void ReloadLog()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			logTree.ReloadFile();
		}
		UpdateLayoutElement();
	}

	public void LoadMoreDay()
	{
		LoadUntil(endDate_.AddDays(-1));
	}

	public void LoadMoreWeek()
	{
		LoadUntil(endDate_.AddDays(-7));
	}

	public void LoadMoreMonth()
	{
		LoadUntil(endDate_.AddMonths(-1));
	}

	public static Color ToColor(DateTime date)
	{
		if( date.DayOfWeek == DayOfWeek.Sunday ) return GameContext.Config.AccentColor;
		else if( date.DayOfWeek == DayOfWeek.Saturday ) return GameContext.Config.AccentColor;
		else return GameContext.Config.TextColor;
	}

	public static string ToFileName(TreeNote treeNote, DateTime date)
	{
		return String.Format("{0}/{1}.dones/{1}{2}.dtml", treeNote.File.DirectoryName, treeNote.File.Name.Replace(".dtml", ""), date.ToString("-yyyy-MM-dd"));
	}

	#endregion
}
