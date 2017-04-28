using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Config : MonoBehaviour
{
	public float WidthPerLevel = 2.0f;
	public float HeightPerLine = 1.0f;
	public float AnimTime = 0.07f;

	// Use this for initialization
	void Awake()
	{
		GameContext.Config = this;
	}

	// Update is called once per frame
	void Update()
	{

	}
}
