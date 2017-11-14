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
	public DateUI EndDateUI;
	
	public string TitleText { get { return "Diary"; } }
	
	
	public override void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach( LogTree logTree in logTrees_ )
		{
			preferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight + 5;
		}
		preferredHeight += 100;
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}
	
	
	public override void LoadUntil(DateTime endDate)
	{
		DateTime date = endDate_;
		DateUI lastDateUI = logTrees_.Count == 0 ? null : logTrees_[logTrees_.Count - 1].GetComponentInChildren<DateUI>();
		while( date > endDate )
		{
			bool exist = false;
			date = date.AddDays(-1.0);
			DateUI dateUI = Instantiate(DateUIPrefab.gameObject, this.transform).GetComponent<DateUI>();
			dateUI.Set(date, ToColor(date));
			foreach( TreeNote treeNote in GameContext.Window.MainTabGroup.TreeNotes )
			{
				string filename = ToFileName(treeNote, date);
				if( File.Exists(filename) )
				{
					LogTree logTree = Instantiate(LogTreePrefab.gameObject, dateUI.transform).GetComponent<LogTree>();
					logTree.Initialize(this, actionManager_, heapFields_);
					logTree.LoadLog(new FileInfo(filename), date);
					logTree.SubscribeKeyInput();
					logTrees_.Add(logTree);

					exist = true;
				}
			}
			if( lastDateUI != null )
			{
				lastDateUI.SetEnableAddDateButtton(exist == false);
			}
			if( exist )
			{
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
}