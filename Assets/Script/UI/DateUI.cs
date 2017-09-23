using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DateUI : MonoBehaviour {

	public Text MonthText;
	public Text DayText;
	public Text WeekDayText;
	public Button AddDateButton;

	private DateTime date_;
	private LogNote ownerLogNote_;


	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void AddDate()
	{
		ownerLogNote_.AddDate(date_.AddDays(-1.0));
		SetEnableAddDateButtton(false);
	}

	public void SetEnableAddDateButtton(bool enable)
	{
		AddDateButton.gameObject.SetActive(enable);
	}

	public void Set(LogNote owner, DateTime date, Color color)
	{
		ownerLogNote_ = owner;
		date_ = date;

		MonthText.text = date.ToString("yyyy/M");
		MonthText.color = color;
		DayText.text = date.ToString("dd").TrimStart('0');//なぜか"d"だと6/26/2017みたいにフルで出力されるので。。
		DayText.color = color;
		WeekDayText.text = date.ToString("ddd");
		WeekDayText.color = color;
		GetComponentInChildren<Image>().color = color;
	}
}
