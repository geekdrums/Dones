using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LogTitleText : MonoBehaviour {

	LogTree logTree_;
	bool isVisible_ = true;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Intialize(LogTree logTree, string text)
	{
		logTree_ = logTree;
		GetComponentInChildren<Text>().text = text;
	}

	public void OnPush()
	{
		isVisible_ = !isVisible_;
		logTree_.gameObject.SetActive(isVisible_);
		GetComponentInParent<DiaryNote>().UpdateLayoutElement();
	}
}
