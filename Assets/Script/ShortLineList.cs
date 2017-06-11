using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShortLineList : MonoBehaviour {

	public ShortLine ShortLinePrefab;
	public float LineHeight = 30;

	List<ShortLine> lines_ = new List<ShortLine>();

	// Use this for initialization
	void Start () {
		InstantiateShortLine(new Line("Hello World!"));
		InstantiateShortLine(new Line("Hello1"));
		InstantiateShortLine(new Line("Hello2"));
		InstantiateShortLine(new Line("Hello3"));
	}

	// Update is called once per frame
	void Update () {
		
	}

	public void InstantiateShortLine(Line line = null)
	{
		ShortLine shortLine = Instantiate(ShortLinePrefab.gameObject, this.transform).GetComponent<ShortLine>();
		lines_.Add(shortLine);
		if( line != null )
		{
			shortLine.Bind(line);
		}
		shortLine.transform.localPosition = Vector3.down * LineHeight * (lines_.Count - 1);
	}
}
