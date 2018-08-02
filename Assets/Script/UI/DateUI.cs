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
	public Text WeekDayText;
	public Text DateText;

	public float PreferredHeight
	{
		get
		{
			if( layoutElement_ == null )
			{
				layoutElement_ = GetComponentInChildren<LayoutElement>();
				contentSizeFitter_ = GetComponent<ContentSizeFitter>();
			}
			return layoutElement_.preferredHeight;
		}
		set
		{
			if( layoutElement_ == null )
			{
				layoutElement_ = GetComponentInChildren<LayoutElement>();
				contentSizeFitter_ = GetComponent<ContentSizeFitter>();
			}
			layoutElement_.preferredHeight = value;
		}
	}
	LayoutElement layoutElement_;
	ContentSizeFitter contentSizeFitter_;

	public LogTree Tree { get { return logTree_; } }
	LogTree logTree_;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Set(LogTree tree, DateTime date, DateTime today, Color color)
	{
		SetTree(tree);
		SetDate(date, today);
		SetColor(color);
		gameObject.name = "Date " + date.ToString("yyyy/MM/dd");
	}

	void SetDate(DateTime date, DateTime today)
	{
		date_ = date;
		if( (today - date).Days < (int)today.DayOfWeek )
		{
			MonthText.text = date.ToString("M/d");
			DateText.text = date.ToString("ddd");
		}
		else if( date.Year == today.Year )
		{
			MonthText.text = date.ToString("M/");
			DayText.text = date.ToString("dd").TrimStart('0');//なぜか"d"だと6/26/2017みたいにフルで出力されるので。。
			DateText.text = date.ToString("M/d");
		}
		else
		{
			MonthText.text = date.ToString("yyyy/M/");
			DayText.text = date.ToString("dd").TrimStart('0');
			DateText.text = date.ToString("M/d");
		}
		WeekDayText.text = date.ToString("ddd");
	}

	void SetColor(Color color)
	{
		MonthText.color = color;
		DayText.color = color;
		WeekDayText.color = color;
		DateText.color = color;
		GetComponentInChildren<Image>().color = color;
	}

	void SetTree(LogTree tree)
	{
		logTree_ = tree;
		UpdateLayout();
	}

	public void UpdateLayout()
	{
		if( logTree_ != null && logTree_.TitleLine != null )
		{
			MonthText.gameObject.SetActive(true);
			DayText.gameObject.SetActive(true);
			WeekDayText.gameObject.SetActive(true);
			DateText.gameObject.SetActive(false);
			PreferredHeight = 100;
		}
		else
		{
			MonthText.gameObject.SetActive(false);
			DayText.gameObject.SetActive(false);
			WeekDayText.gameObject.SetActive(false);
			DateText.gameObject.SetActive(true);
			PreferredHeight = 30;
		}
	}

	public float UpdatePreferredHeight()
	{
		if( logTree_ != null && logTree_.TitleLine != null )
		{
			logTree_.UpdateLayoutElement();
			PreferredHeight = logTree_.Layout.preferredHeight;
			contentSizeFitter_.SetLayoutVertical();
		}

		return PreferredHeight;
	}
}
