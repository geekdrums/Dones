using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class Config : MonoBehaviour
{
	public int DefaultFontSize = 14;
	public float DefaultWidthPerLevel = 27;
	public float DefaultHeightPerLine = 27.0f;

	//public bool DoBackUp = true;

	public int LineHeapCount = 30;
	public int FontSize = 14;
	public float WidthFactor = 1.0f;
	public float HeightFactor = 1.0f;
	public float WidthPerLevel { get { return DefaultWidthPerLevel * WidthFactor * (float)FontSize / DefaultFontSize; } }
	public float HeightPerLine { get { return DefaultHeightPerLine * HeightFactor * (float)FontSize / DefaultFontSize; } }
	public float AnimTime = 0.05f;
	public float AnimOvershoot = 1.70158f;
	public float ArrowStreamDelayTime = 0.3f;
	public float ArrowStreamIntervalTime = 0.03f;
	public float TextInputFixIntervalTime = 1.0f;
	public float DoubleClickInterval = 0.25f;
	public float AutoSaveTime = 1.5f;
	public int LogLoadUnit = 7;
	public int NumRecentFilesMenu = 9;

	public float TagLineHeight = 30;
	public float TagListWidth = 200;
	public float TagListTextMaxWidth = 140;
	public float TabTextMaxWidth = 140;
	public float LogNoteHeaderMargin = 30;
	public int LogNoteSetPathCoroutineCount = 7;
	public float LogNoteSetPathCoroutineInterval = 0.03f;

	public Color ThemeColor;
	public Color AccentColor;
	public Color DoneColor;
	public Color DiaryColor;

	public Color SelectionColor;
	public Color TextColor;
	public Color StrikeColor;
	public Color DoneTextColor;
	public Color CloneTextColor;
	public Color CommentLineColor;
	public Color CommentTextColor;
	public Color TagSelectionColor;
	public Color TagSubTextColor;

	public Color ToggleColor;
	public Color ToggleOpenedColor;

	public string TimeFormat = "HH:mm";
	public string DateFormat = "yyyy/M/d (ddd)";
	public string DefaultTag = "todo";

	FileInfo configFile_;
	ConfigXML configXml_;

	// Use this for initialization
	void Awake()
	{
		GameContext.Config = this;
	}

	// Update is called once per frame
	void Update()
	{

	}

	void OnValidate()
	{
		AnimInfoBase.overshoot = AnimOvershoot;
	}

	void OnApplicationQuit()
	{
		SaveConfig();
	}

	#region config save / load

	[XmlRoot("config")]
	public class ConfigXML
	{
		[XmlElement("TimeFormat")]
		public string TimeFormat { get; set; }

		[XmlElement("DateFormat")]
		public string DateFormat { get; set; }

		[XmlElement("FontSize")]
		public int FontSize { get; set; }
	}

	public void LoadConfig(string filepath)
	{
		configFile_ = new FileInfo(filepath);

		if( configFile_.Exists == false )
		{
			return;
		}

		XmlSerializer serializer = new XmlSerializer(typeof(ConfigXML));
		StreamReader reader = new StreamReader(configFile_.FullName);
		configXml_ = (ConfigXML)serializer.Deserialize(reader);
		reader.Close();

		TimeFormat = configXml_.TimeFormat;
		DateFormat = configXml_.DateFormat;
		FontSize = configXml_.FontSize;
	}

	void SaveConfig()
	{
		if( configFile_.Exists == false )
		{
			if( Directory.Exists(configFile_.DirectoryName) == false )
			{
				Directory.CreateDirectory(configFile_.DirectoryName);
			}
		}
		
		ConfigXML config = new ConfigXML();

		config.TimeFormat	= TimeFormat;
		config.DateFormat	= DateFormat;
		config.FontSize		= FontSize;
		
		StreamWriter writer = new StreamWriter(configFile_.FullName);
		XmlSerializer serializer = new XmlSerializer(typeof(ConfigXML));
		serializer.Serialize(writer, config);
		writer.Flush();
		writer.Close();
	}

	#endregion
}
