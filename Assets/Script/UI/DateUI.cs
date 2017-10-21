using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DateUI : MonoBehaviour {

	public DateTime Date { get { return date_; } }
	private DateTime date_;

	public Text MonthText;
	public Text DayText;
	public DateTextBox DayTextBox;
	public Text WeekDayText;
	public Button AddDateButton;


	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void AddDate()
	{
		GetComponentInParent<LogNote>().AddDate(date_.AddDays(-1.0));
		SetEnableAddDateButtton(false);
	}

	public void SetEnableAddDateButtton(bool enable)
	{
		AddDateButton.gameObject.SetActive(enable);
	}

	public void Set(DateTime date, Color color)
	{
		date_ = date;
		SetDate(date_);
		SetColor(color);
	}

	public void OnDateChanged(DateTime date)
	{
		date_ = date;
		SetDate(date_);
		SetColor(LogNote.ToColor(date_));
		GetComponentInParent<LogTree>().OnDateChanged(date);
	}

	void SetDate(DateTime date)
	{
		MonthText.text = date.ToString("yyyy/M");
		DayText.text = date.ToString("dd").TrimStart('0');//なぜか"d"だと6/26/2017みたいにフルで出力されるので。。
		if( DayTextBox != null )
		{
			DayTextBox.text = date.ToString("dd").TrimStart('0');
		}
		WeekDayText.text = date.ToString("ddd");
	}

	void SetColor(Color color)
	{
		MonthText.color = color;
		DayText.color = color;
		WeekDayText.color = color;
		GetComponentInChildren<Image>().color = color;
		if( DayTextBox != null )
		{
			DayTextBox.Foreground = color;
		}
	}
}
