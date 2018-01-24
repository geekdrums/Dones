using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitleLine : MonoBehaviour {
	
	public string Text
	{
		get { return textComponent_.text; }
		set
		{
			if( textComponent_ == null )
				textComponent_ = GetComponentInChildren<Text>();
			textComponent_.text = value;
		}
	}
	Text textComponent_;

	// Use this for initialization
	void Start ()
	{
		textComponent_ = GetComponentInChildren<Text>();
	}
	
	// Update is called once per frame
	void Update () {
		
	}
}
