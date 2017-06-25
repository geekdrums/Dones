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

	List<LogTree> logTrees_ = new List<LogTree>();
	DateTime today_;
	DateTime endDate_;

	ActionManager actionManager_;
	GameObject heapParent_;
	LayoutElement layout_;
	ContentSizeFitter contentSizeFitter_;

	void Awake()
	{
		heapParent_ = new GameObject("heap");
		heapParent_.transform.parent = this.transform;
		heapParent_.SetActive(false);

		layout_ = GetComponentInParent<LayoutElement>();
		contentSizeFitter_ = GetComponentInParent<ContentSizeFitter>();
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

	public void UpdateLayoutElement()
	{
		float preferredHeight = 0.0f;
		foreach(LogTree logTree in logTrees_)
		{
			preferredHeight += logTree.GetComponent<LayoutElement>().preferredHeight + 5;
		}
		layout_.preferredHeight = preferredHeight;
		contentSizeFitter_.SetLayoutVertical();
	}

	#region file

	public void LoadToday(TreeNote treeNote)
	{
		treeNote_ = treeNote;
		actionManager_ = treeNote_.ActionManager;

		today_ = DateTime.Now.Date;
		endDate_ = today_;

		todayTree_ = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
		DateUI dateUI = Instantiate(DateUIPrefab.gameObject, todayTree_.transform).GetComponent<DateUI>();
		dateUI.Set(today_);
		todayTree_.Initialize(actionManager_, heapParent_);
		if( treeNote_.File != null )
		{
			string filename = ToFileName(treeNote_.File, today_);
			if( File.Exists(filename) == false )
			{
				string folderName = treeNote_.File.FullName.Replace(".dtml", ".dones");
				if( Directory.Exists(folderName) == false )
				{
					Directory.CreateDirectory(folderName);
				}
				StreamWriter writer = File.CreateText(filename);
				writer.Flush();
				writer.Close();
			}
			todayTree_.Load(filename, today_);
		}
		else
		{
			todayTree_.NewTree(today_);
		}
		logTrees_.Add(todayTree_);

		// test
		LoadUntil(today_.AddDays(-LoadDateCount));
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

		string folderName = treeNote_.File.FullName.Replace(".dtml", ".dones");
		if( Directory.Exists(folderName) == false )
		{
			Directory.CreateDirectory(folderName);
		}

		foreach( LogTree logTree in logTrees_ )
		{
			if( logTree.IsEdited )
			{
				logTree.Save();
			}
		}
	}

	public static string ToFileName(FileInfo treeFile, DateTime date)
	{
		return String.Format("{0}/{1}.dones/{1}{2}.dtml", treeFile.DirectoryName, treeFile.Name.Replace(".dtml", ""), date.ToString("-yyyy-MM-dd"));
	}

	#endregion
}
