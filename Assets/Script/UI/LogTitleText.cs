using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class LogTitleText : MonoBehaviour {

	public bool IsExistFile { get { return logTree_ != null; } }
	public string FilePath { get { return filepath_; } }

	DiaryNote ownerNote_;
	LogTree logTree_;
	bool isVisible_ = true;
	string filepath_;

	Graphic graphic_;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Intialize(DiaryNote owner, string filepath, LogTree logTree, string text)
	{
		ownerNote_ = owner;
		name = Path.GetFileName(filepath);
		filepath_ = filepath;
		logTree_ = logTree;
		GetComponentInChildren<Text>().text = text;
		graphic_ = GetComponent<Button>().targetGraphic;
		if( logTree_ == null )
		{
			graphic_.color = ColorManager.MakeAlpha(graphic_.color, 0.6f);
		}
	}

	public void OnLoad(LogTree logTree)
	{
		logTree_ = logTree;
		graphic_.color = ColorManager.MakeAlpha(graphic_.color, 1.0f);
	}

	public void OnPush()
	{
		if( logTree_ != null )
		{
			isVisible_ = !isVisible_;
			logTree_.gameObject.SetActive(isVisible_);
			ownerNote_.UpdateLayoutElement();
		}
		else
		{
			OnLoad(ownerNote_.InsertLogTree(this, GetComponentInParent<DateUI>().Date, filepath_));
		}
	}
}
