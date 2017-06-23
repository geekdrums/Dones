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

public class LogTree : Tree
{
	public DateTime Date { get; private set; }

	#region file

	public void Load(string filename, string folderPath, DateTime date)
	{
		file_ = new FileInfo(String.Format("{0}/{1}-{2}.dones", folderPath, filename, date.ToString("yyyy-MM-dd")));
		if( file_.Exists == false )
		{
			if( Directory.Exists(file_.DirectoryName) == false )
			{
				Directory.CreateDirectory(file_.DirectoryName);
			}

			rootLine_ = new Line(file_.Name);
			rootLine_.Bind(this.gameObject);
			rootLine_.Add(new Line(""));
		}
		else
		{
			LoadInternal();
		}
	}

	#endregion
}
