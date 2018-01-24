using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckMark : MonoBehaviour {

	public UIGaugeRenderer Line1;
	public UIGaugeRenderer Line2;

	RectTransform rect_;

	// Use this for initialization
	void Awake () {
		rect_ = GetComponent<RectTransform>();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	public void SetPositionX(float x)
	{
		if( rect_ == null )
		{
			rect_ = GetComponent<RectTransform>();
		}
		Vector2 size = rect_.rect.size;
		rect_.offsetMin = new Vector2(x, rect_.offsetMin.y);
		rect_.offsetMax = rect_.offsetMin + size;
	}

	public void Check()
	{
		Line1.Rate = 0;
		Line2.Rate = 0;
		AnimManager.AddAnim(Line1, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.05f);
		AnimManager.AddAnim(Line2, 1.5f, ParamType.GaugeRate, AnimType.Time, 0.1f, 0.05f);
		AnimManager.AddAnim(Line2, 1.0f, ParamType.GaugeRate, AnimType.Time, 0.05f, 0.15f);
	}
}
