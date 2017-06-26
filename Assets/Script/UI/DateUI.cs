using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DateUI : MonoBehaviour {

	public Text MonthText;
	public Text DayText;
	public Text WeekDayText;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Set(DateTime date)
	{
		MonthText.text = date.ToString("yyyy/M");
		DayText.text = date.ToString("dd").TrimStart('0');//なぜか"d"だと6/26/2017みたいにフルで出力されるので。。
		WeekDayText.text = date.ToString("ddd");
	}
}
