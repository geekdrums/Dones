using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SaveText : MonoBehaviour {

	public string Text { get { return textComponent_.text; } set { textComponent_.text = value; } } 
	Text textComponent_;
	CheckMark check_;
	UIMidairPrimitive circle_;
	bool isSaving_ = false;

	// Use this for initialization
	void Awake () {
		textComponent_ = GetComponent<Text>();
		check_ = GetComponentInChildren<CheckMark>(includeInactive: true);
		circle_ = GetComponentInChildren<UIMidairPrimitive>(includeInactive: true);
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void Saved()
	{
		if( isSaving_ )
		{
			isSaving_ = false;
			Text = "saved!";
			AnimManager.AddAnim(textComponent_, 0.0f, ParamType.TextAlphaColor, AnimType.Time, 0.6f, 0.2f);

			check_.gameObject.SetActive(true);
			AnimManager.RemoveOtherAnim(check_);
			check_.Check();
			AnimManager.AddAnim(check_, 0.0f, ParamType.AlphaColor, AnimType.Time, 0.6f, initValue: 1.0f);

			circle_.gameObject.SetActive(false);
			AnimManager.RemoveOtherAnim(circle_);
		}
	}

	public void StartSaving()
	{
		if( isSaving_ == false )
		{
			isSaving_ = true;
			this.gameObject.SetActive(true);
			Text = "saving...";
			AnimManager.RemoveOtherAnim(textComponent_);
			AnimManager.AddAnim(textComponent_, 1.0f, ParamType.TextAlphaColor, AnimType.Time, 0.0f);

			circle_.gameObject.SetActive(true);
			AnimManager.RemoveOtherAnim(circle_);
			AnimManager.AddAnim(circle_, 360.0f, ParamType.RotationZ, AnimType.Time, 1.0f, endOption: AnimEndOption.Loop, initValue: 0.0f);

			check_.gameObject.SetActive(false);
			AnimManager.RemoveOtherAnim(check_);
		}
	}
}
