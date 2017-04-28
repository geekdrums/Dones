using UnityEngine;
using System.Collections;

[ExecuteInEditMode]
public class GaugeRenderer : MonoBehaviour, IColoredObject
{

	public GameObject LineMesh;
	public GameObject LineParent;
	public Color LineColor;
	public float Length = 2.0f;
	public float Rate = 1.0f;
	public float Width = 1;
	public Vector3 Direction = Vector3.right;

	bool IsHorizontal { get { return (Direction.x == 0 && Direction.y == 0 ? Direction.z == 0 : Mathf.Abs(Direction.x) > 0); } }

	void OnValidate()
	{
		if( LineParent == null && LineMesh != null )
		{
			LineParent = new GameObject("LineParent");
			LineParent.transform.parent = this.transform;
			LineMesh.transform.parent = LineParent.transform;
			LineParent.transform.localPosition = Vector3.zero;
			LineMesh.transform.localPosition = Vector3.zero;
		}

		if( LineParent != null )
		{
			UpdateLine();
		}
	}

	// Use this for initialization
	void Start()
	{
#if UNITY_EDITOR
		if( !UnityEditor.EditorApplication.isPlaying )
		{
			return;
		}
#endif

		LineColor = LineMesh.GetComponent<Renderer>().material.color;
	}
	
	// Update is called once per frame
	void Update () {
		if( LineParent != null )
		{
			UpdateLine();
		}
	}

	void UpdateLine()
	{
		LineMesh.transform.localPosition = Direction * 0.5f;
		if( IsHorizontal )
		{
			LineParent.transform.localScale = new Vector3(Length * Rate, Width, 1);
		}
		else
		{
			LineParent.transform.localScale = new Vector3(Width, Length * Rate, 1);
		}
	}

	public void SetRate(float rate, float animTime = 0)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, rate, ParamType.GaugeRate, AnimType.Time, animTime);
		}
		else
		{
			Rate = rate;
			UpdateLine();
		}
	}

	public void SetWidth(float width, float animTime = 0)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, width, ParamType.GaugeWidth, AnimType.Time, animTime);
		}
		else
		{
			Width = width;
			UpdateLine();
		}
	}

	public void SetColor(Color color, float animTime)
	{
		if( animTime > 0 )
		{
			AnimManager.AddAnim(gameObject, color, ParamType.Color, AnimType.Time, animTime);
		}
		else
		{
			LineColor = color;
			LineMesh.GetComponent<Renderer>().material.color = LineColor;
		}
	}

	public void SetColor(Color color)
	{
		SetColor(color, 0);
	}

	public Color GetColor()
	{
		return LineColor;
	}
}
