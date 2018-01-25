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
	public DateUI EndDateUI;
	
	public override string TitleText { get { return "Diary"; } }

	List<DateUI> dateUIlist_ = new List<DateUI>();

	public void LoadDiary(TabButton tab)
	{
		tabButton_ = tab;
		tabButton_.BindedNote = this;
		tabButton_.Text = TitleText;

		LoadUntil(today_.AddDays(-LoadDateCount));
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
			foreach( LogTree logTree in dateUI.GetComponentsInChildren<LogTree>() )
			{
				logTree.UpdateLayoutElement(applyMinHeight: false);
				datePreferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight + 30;
			}
			datePreferredHeight = Math.Max(GameContext.Config.MinLogTreeHeight, datePreferredHeight);
			dateUI.GetComponent<LayoutElement>().preferredHeight = datePreferredHeight;
			preferredHeight += datePreferredHeight + 5;
		}
		preferredHeight += 120;
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}
	
	
	public override void LoadUntil(DateTime endDate)
	{
		DateTime date = endDate_;
		while( date > endDate )
		{
			DateUI dateUI = null;
			foreach( TreeNote treeNote in GameContext.Window.MainTabGroup.TreeNotes )
			{
				string filename = ToFileName(treeNote, date);
				if( File.Exists(filename) )
				{
					LoadDate(ref dateUI, date, filename, treeNote.Tree.TitleText);
				}
			}
			date = date.AddDays(-1.0);
		}
		endDate_ = endDate;
		EndDateUI.Set(endDate.AddDays(-1), GameContext.Config.CommentTextColor);
		EndDateUI.transform.parent.SetAsLastSibling();
		UpdateLayoutElement();
	}

	public override void ReloadNote()
	{
		DateTime date = DateTime.Now;
		while( date > endDate_ )
		{
			DateUI dateUI = dateUIlist_.Find((DateUI dui) => dui.Date.Date == date.Date);
			foreach( TreeNote treeNote in GameContext.Window.MainTabGroup.TreeNotes )
			{
				string filename = ToFileName(treeNote, date);
				if( File.Exists(filename) )
				{
					LogTree existTree = logTrees_.Find((LogTree lt) => lt.File.Name == Path.GetFileName(filename));
					if( existTree != null )
					{
						existTree.ReloadFile();
					}
					else
					{
						LoadDate(ref dateUI, date, filename, treeNote.Tree.TitleText);
					}
				}
			}
			date = date.AddDays(-1.0);
		}
		EndDateUI.transform.parent.SetAsLastSibling();
		UpdateLayoutElement();
	}

	protected void LoadDate(ref DateUI dateUI, DateTime date, string filename, string title)
	{
		if( dateUI == null )
		{
			dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
			dateUI.Set(date, ToColor(date));
			dateUI.SetEnableAddDateButtton(false);
			dateUIlist_.Add(dateUI);
		}

		Text titleText = Instantiate(TitleTextPrefab.gameObject, dateUI.GetComponentInChildren<VerticalLayoutGroup>().transform).GetComponentInChildren<Text>();
		titleText.text = title;

		LogTree logTree = Instantiate(LogTreePrefab.gameObject, dateUI.GetComponentInChildren<VerticalLayoutGroup>().transform).GetComponent<LogTree>();
		logTree.Initialize(this, new ActionManagerProxy(actionManager_), heapFields_);
		logTree.LoadLog(new FileInfo(filename), date);
		logTree.SubscribeKeyInput();
		logTree.OnEdited += this.OnEdited;
		logTrees_.Add(logTree);
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