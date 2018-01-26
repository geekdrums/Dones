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
public class DiaryNote : DiaryNoteBase
{
	public GameObject LogTreePrefab;
	public GameObject DateUIPrefab;
	public GameObject TitleTextPrefab;
	
	public override string TitleText { get { return "Diary"; } }

	List<DateUI> dateUIlist_ = new List<DateUI>();

	public void LoadDiary(TabButton tab)
	{
		tabButton_ = tab;
		tabButton_.BindedNote = this;
		tabButton_.Text = TitleText;
		LoadMore();
	}

	public override void OnTabSelected()
	{
		base.OnTabSelected();

		SubscribeKeyInput();

		GameContext.Window.LogTabButton.OwnerNote = null;
		
		foreach( LogTree logTree in logTrees_ )
		{
			if( EditedLogTreeDict.ContainsKey(logTree.File.FullName) )
			{
				logTree.ReloadFile();
				EditedLogTreeDict.Remove(logTree.File.FullName);
			}
		}
	}

	public override void OnBeginTabDrag()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree.FocusedLine != null )
			{
				logTree.FocusedLine.Field.IsFocused = false;
			}
		}
	}

	public override void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach( DateUI dateUI in dateUIlist_ )
		{
			float datePreferredHeight = 0;
			Transform parent = dateUI.GetComponentInChildren<VerticalLayoutGroup>().transform;
			for( int i = 0; i < parent.childCount; ++i )
			{
				LogTree logTree = parent.GetChild(i).GetComponent<LogTree>();
				if( logTree != null && logTree.gameObject.activeInHierarchy )
				{
					logTree.UpdateLayoutElement(applyMinHeight: false);
					datePreferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight;
				}
				else if( parent.GetChild(i).GetComponent<LogTitleText>() != null )
				{
					datePreferredHeight += 30;
				}
			}
			datePreferredHeight = Math.Max(GameContext.Config.MinLogTreeHeight, datePreferredHeight);
			dateUI.GetComponent<LayoutElement>().preferredHeight = datePreferredHeight;
			preferredHeight += datePreferredHeight + 5;
		}
		preferredHeight += 200;
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}
	
	
	public override void LoadMore()
	{
		DateTime date = endDate_;
		int loadCount = GameContext.Config.LogLoadUnit;
		while( loadCount > 0 )
		{
			DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
			dateUI.Set(date, ToColor(date));
			dateUI.SetEnableAddDateButtton(false);
			dateUIlist_.Add(dateUI);
			Transform parent = dateUI.GetComponentInChildren<VerticalLayoutGroup>().transform;
			foreach( TreeNote treeNote in GameContext.Window.MainTabGroup.TreeNotes )
			{
				LogTitleText titleText = Instantiate(TitleTextPrefab.gameObject, dateUI.GetComponentInChildren<VerticalLayoutGroup>().transform).GetComponent<LogTitleText>();
				string filename = ToFileName(treeNote, date);
				LogTree logTree = null;
				if( File.Exists(filename) )
				{
					logTree = LoadLogTree(parent, date, filename);
					logTrees_.Add(logTree);
				}
				titleText.Intialize(this, filename, logTree, treeNote.Tree.TitleText);
			}
			date = date.AddDays(-1.0);
			--loadCount;
		}
		endDate_ = date;
		UpdateLayoutElement();
	}

	public override void ReloadNote()
	{
		DateTime date = DateTime.Now;
		List<LogTree> cachedLogTrees = new List<LogTree>(logTrees_);
		logTrees_.Clear();
		foreach( DateUI dateUI in dateUIlist_ )
		{
			Transform parent = dateUI.GetComponentInChildren<VerticalLayoutGroup>().transform;
			foreach( TreeNote treeNote in GameContext.Window.MainTabGroup.TreeNotes )
			{
				string filename = ToFileName(treeNote, date);
				if( File.Exists(filename) )
				{
					LogTree existTree = cachedLogTrees.Find((LogTree lt) => lt.File.Name == Path.GetFileName(filename));
					if( existTree != null )
					{
						existTree.ReloadFile();
						logTrees_.Add(existTree);
					}
					else
					{
						Transform titleObj = parent.Find(Path.GetFileName(filename));
						if( titleObj != null )
						{
							LogTree logTree = LoadLogTree(parent, date, filename);
							logTree.gameObject.transform.SetSiblingIndex(titleObj.GetSiblingIndex() + 1);
							logTrees_.Add(logTree);
						}
					}
				}
			}
		}
		UpdateLayoutElement();
	}

	public LogTree InsertLogTree(LogTitleText titleText, DateTime date, string filename)
	{
		LogTree logTree = LoadLogTree(titleText.transform.parent, date, filename);
		logTree.gameObject.transform.SetSiblingIndex(titleText.transform.GetSiblingIndex() + 1);

		// search insert index
		int insertIndex = -1;
		bool find = false;
		// 同じDateUIの子供から探し始める
		for( int i = 0; i < titleText.transform.parent.childCount; ++i )
		{
			LogTree existLogTree = titleText.transform.parent.GetChild(i).GetComponent<LogTree>();
			if( logTree == existLogTree )
			{
				find = true;
				if( insertIndex >= 0 )
				{
					// 直前のLogTreeが見つかってるのでここでOK
					break;
				}
			}
			else if( existLogTree != null )
			{
				insertIndex = logTrees_.IndexOf(existLogTree) + 1;
				if( find )
				{
					// ここが直後のLogTreeになるのでその前に差し込む
					insertIndex -= 1;
					break;
				}
			}
		}
		// DateUIに他のTreeが無いようなので、logTrees_で探索する
		if( insertIndex < 0 )
		{
			for( int i = 0; i < logTrees_.Count; ++i )
			{
				if( logTrees_[i].Date < logTree.Date )
				{
					insertIndex = i;
					break;
				}
			}
		}
		logTrees_.Insert(insertIndex, logTree);

		UpdateLayoutElement();

		return logTree;
	}

	protected LogTree LoadLogTree(Transform parent, DateTime date, string filename)
	{
		LogTree logTree = Instantiate(LogTreePrefab.gameObject, parent).GetComponent<LogTree>();
		logTree.Initialize(this, new ActionManagerProxy(actionManager_), heapFields_);
		logTree.LoadLog(new FileInfo(filename), date);
		logTree.SubscribeKeyInput();
		logTree.OnEdited += this.OnEdited;

		return logTree;
	}

	public override void OnEdited(object sender, EventArgs e)
	{
		base.OnEdited(sender, e);
		LogTree logTree = sender as LogTree;
		if( EditedLogTreeDict.ContainsValue(logTree) == false )
		{
			EditedLogTreeDict.Add(logTree.File.FullName, logTree);
		}
	}

	public void SaveDiary()
	{
		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree.IsEdited )
			{
				logTree.SaveFile();
			}
		}

		saveRequestedTrees_.Clear();
	}
}