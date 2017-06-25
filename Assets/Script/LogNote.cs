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

	public LogTree TodayTree { get { return todayTree_; } }
	private LogTree todayTree_;

	public TreeNote TreeNote { get { return treeNote_; } }
	TreeNote treeNote_;

	List<LogTree> logTrees_;
	DateTime today_;
	DateTime endDate_;


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

	#region file

	public void LoadToday(TreeNote treeNote)
	{
		treeNote_ = treeNote;
		today_ = DateTime.Now.Date;
		endDate_ = today_;

		todayTree_ = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
		if( treeNote_.File != null )
		{
			todayTree_.Load(ToFileName(treeNote_.File, today_), today_);
		}
		else
		{
			todayTree_.NewTree(today_);
		}
		logTrees_.Add(todayTree_);
	}

	public void LoadUntil(DateTime endDate)
	{
		DateTime date = endDate_;
		while( date > endDate )
		{
			string filename = ToFileName(treeNote_.File, date);
			if( File.Exists(filename) )
			{
				LogTree logTree = Instantiate(LogTreePrefab.gameObject, this.transform).GetComponent<LogTree>();
				logTree.Load(filename, date);
				logTrees_.Add(logTree);
			}
			date = date.AddDays(-1.0);
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
		return String.Format("{0}/{1}.dones/{1}-{2}.dones", treeFile.DirectoryName, treeFile.Name, date.ToString("yyyy-MM-dd"));
	}

	#endregion
}
