using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public interface IColoredObject
{
	void SetColor(Color color);
	Color GetColor();
}

public enum ParamType
{
	//Transform
	Scale,
	ScaleX,
	ScaleY,
	ScaleZ,
	RotationZ,
	Position,
	PositionX,
	PositionY,
	PositionZ,

	//MidairPrimitive
	PrimitiveRadius,
	PrimitiveWidth,
	PrimitiveArc,

	//Gauge
	GaugeLength,
	GaugeRate,
	GaugeWidth,

	//IColoredObject
	Color,
	AlphaColor,

	TextColor,
	TextAlphaColor,

	// for RemoveAnim
	Any,
}

public enum AnimType
{
	Linear,
	Time,
	BounceIn,
	BounceOut
}

public enum AnimEndOption
{
	None,
	Destroy,
	Deactivate,
}

public abstract class AnimInfoBase
{
	public GameObject Object;
	public ParamType Param;
	public AnimType Anim;
	public object Target;
	public float Factor;
	public float Delay;
	public AnimEndOption EndOption;

	protected object initialValue_;
	protected float normalizedValue_;
	protected float animValue_;

	public bool IsEnd
	{
		get
		{
			return normalizedValue_ >= 1.0f;
		}
		set
		{
			if( value )
			{
				normalizedValue_ = 1.0f;
				OnEndAnim();
			}
		}
	}
	public bool IsPlaying { get { return Delay <= 0 && !IsEnd; } }

	protected float currentValueFloat { get { return (float)initialValue_ + ((float)Target - (float)initialValue_) * animValue_; } }
	protected Vector3 currentValueVector3 { get { return (Vector3)initialValue_ + ((Vector3)Target - (Vector3)initialValue_) * animValue_; } }

	public static float overshoot = 1.70158f;

	public AnimInfoBase(GameObject obj, object target, ParamType paramType, AnimType animType, float factor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
	{
		Object = obj;
		Param = paramType;
		Anim = animType;
		Target = target;
		Factor = factor;
		Delay = delay;
		EndOption = endOption;

		normalizedValue_ = 0;
		animValue_ = 0;

		if( Target is int )
		{
			int intTarget = (int)Target;
			Target = (float)intTarget;
		}

		if( Delay <= 0 )
		{
			OnStartAnim();
		}
	}

	public void Update()
	{
		if( Object == null )
		{
			IsEnd = true;
			return;
		}

		if( Delay > 0 )
		{
			Delay -= Time.deltaTime;
			if( Delay <= 0 )
			{
				OnStartAnim();
			}
			else
			{
				return;
			}
		}

		UpdateTimeValue();

		UpdateAnimValue();

		if( IsEnd )
		{
			OnEndAnim();
		}
	}

	protected abstract void InitValue();

	protected void OnStartAnim()
	{
		AnimManager.RemoveOtherAnim(this);
		InitValue();
		if( initialValue_ is float && Target is float && (float)initialValue_ == (float)Target )
		{
			IsEnd = true;
		}
		else if( initialValue_ is Color && Color.Equals(initialValue_, Target) )
		{
			IsEnd = true;
		}
		else if( initialValue_ is Vector3 && Vector3.Equals(initialValue_, Target) )
		{
			IsEnd = true;
		}
	}


	protected void OnEndAnim()
	{
		if( Object == null ) return;

		if( EndOption == AnimEndOption.Destroy )
		{
			GameObject.Destroy(Object);
		}
		else if( EndOption == AnimEndOption.Deactivate )
		{
			Object.SetActive(false);
		}
	}

	protected virtual void UpdateTimeValue()
	{
		if( Anim == AnimType.Linear )
		{
			normalizedValue_ = Mathf.Lerp(normalizedValue_, 1.0f, Factor);
			if( Mathf.Abs(normalizedValue_ - 1.0f) < 0.01f )
			{
				normalizedValue_ = 1.0f;
			}
			animValue_ = normalizedValue_;
		}
		else
		{
			normalizedValue_ += Time.deltaTime / Factor;
			normalizedValue_ = Mathf.Clamp01(normalizedValue_);
			switch( Anim )
			{
			case AnimType.Time:
				animValue_ = normalizedValue_;
				break;
			case AnimType.BounceIn:
				{
					float r = normalizedValue_ - 1;
					animValue_ = r * r * ((overshoot + 1) * r + overshoot) + 1;
				}
				break;
			case AnimType.BounceOut:
				{
					float r = 1.0f - normalizedValue_ - 1;
					animValue_ = 1.0f - (r * r * ((overshoot + 1) * r + overshoot) + 1);
				}
				break;
			}
		}
	}

	protected abstract void UpdateAnimValue();
}

public class TransformAnimInfo : AnimInfoBase
{
	protected Transform transform_;

	public TransformAnimInfo(GameObject obj, object target, ParamType paramType, AnimType animType, float factor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
		: base(obj, target, paramType, animType, factor, delay, endOption)
	{
		if( ParamType.PositionZ < paramType )
		{
			throw new System.Exception("TransformAnimInfo: wrong param type! paramType = " + paramType.ToString());
		}
	}

	protected override void InitValue()
	{
		switch( Param )
		{
		case ParamType.Scale:
			transform_ = Object.transform;
			initialValue_ = transform_.localScale;
			break;
		case ParamType.ScaleX:
			transform_ = Object.transform;
			initialValue_ = (float)transform_.localScale.x;
			break;
		case ParamType.ScaleY:
			transform_ = Object.transform;
			initialValue_ = (float)transform_.localScale.y;
			break;
		case ParamType.ScaleZ:
			transform_ = Object.transform;
			initialValue_ = (float)transform_.localScale.z;
			break;
		case ParamType.Position:
			transform_ = Object.transform;
			initialValue_ = transform_.localPosition;
			break;
		case ParamType.PositionX:
			transform_ = Object.transform;
			initialValue_ = (float)transform_.localPosition.x;
			break;
		case ParamType.PositionY:
			transform_ = Object.transform;
			initialValue_ = (float)transform_.localPosition.y;
			break;
		case ParamType.PositionZ:
			transform_ = Object.transform;
			initialValue_ = (float)transform_.localPosition.z;
			break;
		case ParamType.RotationZ:
			transform_ = Object.transform;
			initialValue_ = (float)(transform_.rotation.eulerAngles.z + 360) % 360;
			break;
		}
	}

	protected override void UpdateAnimValue()
	{
		switch( Param )
		{
		case ParamType.Scale:
			transform_.localScale = currentValueVector3;
			break;
		case ParamType.ScaleX:
			transform_.localScale = new Vector3(currentValueFloat, transform_.localScale.y, transform_.localScale.z);
			break;
		case ParamType.ScaleY:
			transform_.localScale = new Vector3(transform_.localScale.x, currentValueFloat, transform_.localScale.z);
			break;
		case ParamType.ScaleZ:
			transform_.localScale = new Vector3(transform_.localScale.x, transform_.localScale.y, currentValueFloat);
			break;
		case ParamType.Position:
			transform_.localPosition = currentValueVector3;
			break;
		case ParamType.PositionX:
			transform_.localPosition = new Vector3(currentValueFloat, transform_.localPosition.y, transform_.localPosition.z);
			break;
		case ParamType.PositionY:
			transform_.localPosition = new Vector3(transform_.localPosition.x, currentValueFloat, transform_.localPosition.z);
			break;
		case ParamType.PositionZ:
			transform_.localPosition = new Vector3(transform_.localPosition.x, transform_.localPosition.y, currentValueFloat);
			break;
		case ParamType.RotationZ:
			transform_.localRotation = Quaternion.AngleAxis(currentValueFloat, Vector3.forward);
			break;
		}
	}
}

public class PrimitiveAnimInfo : AnimInfoBase
{
	protected MidairPrimitive primitive_;
	protected UIMidairPrimitive uiprimitive_;

	public PrimitiveAnimInfo(GameObject obj, object target, ParamType paramType, AnimType animType, float factor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
		: base(obj, target, paramType, animType, factor, delay, endOption)
	{
		if( paramType < ParamType.PrimitiveRadius || ParamType.PrimitiveArc < paramType )
		{
			throw new System.Exception("PrimitiveAnimInfo: wrong param type! paramType = " + paramType.ToString());
		}
	}

	protected override void InitValue()
	{
		primitive_ = Object.GetComponentInChildren<MidairPrimitive>();
		uiprimitive_ = Object.GetComponentInChildren<UIMidairPrimitive>();
		if( primitive_ != null )
		{
			switch( Param )
			{
			case ParamType.PrimitiveRadius:
				initialValue_ = (float)primitive_.Radius;
				break;
			case ParamType.PrimitiveWidth:
				initialValue_ = (float)primitive_.Width;
				break;
			case ParamType.PrimitiveArc:
				initialValue_ = (float)primitive_.ArcRate;
				break;
			}
		}
		else if( uiprimitive_ != null )
		{
			switch( Param )
			{
			case ParamType.PrimitiveRadius:
				initialValue_ = (float)uiprimitive_.Radius;
				break;
			case ParamType.PrimitiveWidth:
				initialValue_ = (float)uiprimitive_.Width;
				break;
			case ParamType.PrimitiveArc:
				initialValue_ = (float)uiprimitive_.ArcRate;
				break;
			}
		}
	}

	protected override void UpdateAnimValue()
	{
		if( primitive_ != null )
		{
			switch( Param )
			{
			case ParamType.PrimitiveRadius:
				primitive_.SetSize(currentValueFloat);
				break;
			case ParamType.PrimitiveWidth:
				primitive_.SetWidth(currentValueFloat);
				break;
			case ParamType.PrimitiveArc:
				primitive_.SetArc(currentValueFloat);
				break;
			}
		}
		else if( uiprimitive_ != null )
		{
			switch( Param )
			{
			case ParamType.PrimitiveRadius:
				uiprimitive_.SetSize(currentValueFloat);
				break;
			case ParamType.PrimitiveWidth:
				uiprimitive_.SetWidth(currentValueFloat);
				break;
			case ParamType.PrimitiveArc:
				uiprimitive_.SetArc(currentValueFloat);
				break;
			}
		}
	}
}

public class GaugeAnimInfo : AnimInfoBase
{
	protected GaugeRenderer gauge_;
	protected UIGaugeRenderer uigauge_;

	public GaugeAnimInfo(GameObject obj, object target, ParamType paramType, AnimType animType, float factor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
		: base(obj, target, paramType, animType, factor, delay, endOption)
	{
		if( paramType < ParamType.GaugeLength || ParamType.GaugeWidth < paramType )
		{
			throw new System.Exception("GaugeAnimInfo: wrong param type! paramType = " + paramType.ToString());
		}
	}

	protected override void InitValue()
	{
		gauge_ = Object.GetComponentInChildren<GaugeRenderer>();
		uigauge_ = Object.GetComponentInChildren<UIGaugeRenderer>();
		if( gauge_ != null )
		{
			switch( Param )
			{
			case ParamType.GaugeLength:
				initialValue_ = (float)gauge_.Length;
				break;
			case ParamType.GaugeRate:
				initialValue_ = (float)gauge_.Rate;
				break;
			case ParamType.GaugeWidth:
				initialValue_ = (float)gauge_.Width;
				break;
			}
		}
		else if( uigauge_ != null )
		{
			switch( Param )
			{
			case ParamType.GaugeLength:
				initialValue_ = (float)uigauge_.Length;
				break;
			case ParamType.GaugeRate:
				initialValue_ = (float)uigauge_.Rate;
				break;
			case ParamType.GaugeWidth:
				initialValue_ = (float)uigauge_.Width;
				break;
			}
		}
	}

	protected override void UpdateAnimValue()
	{
		if( gauge_ != null )
		{
			switch( Param )
			{
			case ParamType.GaugeLength:
				gauge_.Length = currentValueFloat;
				break;
			case ParamType.GaugeRate:
				gauge_.SetRate(currentValueFloat);
				break;
			case ParamType.GaugeWidth:
				gauge_.SetWidth(currentValueFloat);
				break;
			}
		}
		else if( uigauge_ != null )
		{
			switch( Param )
			{
			case ParamType.GaugeLength:
				uigauge_.SetLength(currentValueFloat);
				break;
			case ParamType.GaugeRate:
				uigauge_.SetRate(currentValueFloat);
				break;
			case ParamType.GaugeWidth:
				uigauge_.SetWidth(currentValueFloat);
				break;
			}
		}
	}
}

public class ColorAnimInfo : AnimInfoBase
{
	protected IColoredObject coloredObj_;
	protected Color baseColor_;

	public ColorAnimInfo(GameObject obj, object target, ParamType paramType, AnimType animType, float factor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
		: base(obj, target, paramType, animType, factor, delay, endOption)
	{
		if( paramType != ParamType.Color && paramType != ParamType.AlphaColor )
		{
			throw new System.Exception("ColorAnimInfo: wrong param type! paramType = " + paramType.ToString());
		}
	}

	protected override void InitValue()
	{
		coloredObj_ = Object.GetComponentInChildren<IColoredObject>();
		initialValue_ = coloredObj_.GetColor();
		if( Param == ParamType.AlphaColor )
		{
			baseColor_ = (Color)initialValue_;
			initialValue_ = baseColor_.a;
			baseColor_.a = 1.0f;
		}
	}

	protected override void UpdateAnimValue()
	{
		if( Param == ParamType.Color )
		{
			coloredObj_.SetColor(Color.Lerp((Color)initialValue_, (Color)Target, animValue_));
		}
		else if( Param == ParamType.AlphaColor )
		{
			coloredObj_.SetColor(ColorManager.MakeAlpha(baseColor_, Mathf.Lerp((float)initialValue_, (float)Target, animValue_)));
		}
	}
}

public class TextAnimInfo : AnimInfoBase
{
	protected TextMesh text_;
	protected Text uitext_;
	protected Color baseColor_;

	public TextAnimInfo(GameObject obj, object target, ParamType paramType, AnimType animType, float factor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
		: base(obj, target, paramType, animType, factor, delay, endOption)
	{
		if( paramType != ParamType.TextColor && paramType != ParamType.TextAlphaColor )
		{
			throw new System.Exception("TextAnimInfo: wrong param type! paramType = " + paramType.ToString());
		}
	}

	protected override void InitValue()
	{
		uitext_ = Object.GetComponentInChildren<Text>();
		if( uitext_ != null )
		{
			initialValue_ = uitext_.color;
			if( Param == ParamType.TextAlphaColor )
			{
				baseColor_ = (Color)initialValue_;
				initialValue_ = baseColor_.a;
				baseColor_.a = 1.0f;
			}
			return;
		}
		text_ = Object.GetComponentInChildren<TextMesh>();
		if( text_ != null )
		{
			initialValue_ = text_.color;
			if( Param == ParamType.TextAlphaColor )
			{
				baseColor_ = (Color)initialValue_;
				initialValue_ = baseColor_.a;
				baseColor_.a = 1.0f;
			}
			return;
		}
	}

	protected override void UpdateAnimValue()
	{
		if( text_ != null )
		{
			if( Param == ParamType.TextColor )
			{
				text_.color = Color.Lerp((Color)initialValue_, (Color)Target, animValue_);
			}
			else if( Param == ParamType.TextAlphaColor )
			{
				text_.color = ColorManager.MakeAlpha(baseColor_, Mathf.Lerp((float)initialValue_, (float)Target, animValue_));
			}
		}
		else if( uitext_ != null )
		{
			if( Param == ParamType.TextColor )
			{
				uitext_.color = Color.Lerp((Color)initialValue_, (Color)Target, animValue_);
			}
			else if( Param == ParamType.TextAlphaColor )
			{
				uitext_.color = ColorManager.MakeAlpha(baseColor_, Mathf.Lerp((float)initialValue_, (float)Target, animValue_));
			}
		}
	}
}

public class ShakeAnimInfo : TransformAnimInfo
{
	protected float updateTime_;
	protected float timer_;

	public ShakeAnimInfo(GameObject obj, object target, float time, float updateTime, ParamType paramType, AnimType animType, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
		: base(obj, target, paramType, animType, time, delay, endOption)
	{
		if( paramType < ParamType.Position || ParamType.PositionZ < paramType )
		{
			throw new System.Exception("ShakeAnimInfo: wrong param type! paramType = " + paramType.ToString());
		}

		timer_ = 0;
		updateTime_ = updateTime;
	}

	protected override void UpdateTimeValue()
	{
		base.UpdateTimeValue();

		animValue_ = Mathf.Sqrt(1.0f - normalizedValue_);
	}

	protected override void UpdateAnimValue()
	{
		timer_ += Time.deltaTime;
		if( timer_ >= updateTime_ || animValue_ <= 0.0f )
		{
			switch( Param )
			{
			case ParamType.Position:
				transform_.localPosition = (Vector3)initialValue_ + Random.insideUnitSphere * (float)Target * animValue_;
				break;
			case ParamType.PositionX:
				transform_.localPosition = new Vector3((float)initialValue_ + Random.Range(-(float)Target, (float)Target) * animValue_, transform_.localPosition.y, transform_.localPosition.z);
				break;
			case ParamType.PositionY:
				transform_.localPosition = new Vector3(transform_.localPosition.x, (float)initialValue_ + Random.Range(-(float)Target, (float)Target) * animValue_, transform_.localPosition.z);
				break;
			case ParamType.PositionZ:
				transform_.localPosition = new Vector3(transform_.localPosition.x, transform_.localPosition.y, (float)initialValue_ + Random.Range(-(float)Target, (float)Target) * animValue_);
				break;
			}
			timer_ %= updateTime_;
		}
	}
}


public class AnimManager : MonoBehaviour
{
	static AnimManager Instance
	{
		get
		{
			if( instance_ == null )
			{
				instance_ = UnityEngine.Object.FindObjectOfType<AnimManager>();
			}
			return instance_;
		}
	}
	static AnimManager instance_;

	public List<AnimInfoBase> Animations = new List<AnimInfoBase>();
	List<AnimInfoBase> removeAnims_ = new List<AnimInfoBase>();

	// Use this for initialization
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{
		foreach( AnimInfoBase removeAnim in removeAnims_ )
		{
			removeAnim.IsEnd = true;
			Animations.Remove(removeAnim);
		}
		removeAnims_.Clear();

		foreach( AnimInfoBase anim in Animations )
		{
			anim.Update();
		}
		Animations.RemoveAll((AnimInfoBase anim) => anim.IsEnd);
	}

	private static GameObject ToGameObject(Object obj)
	{
		GameObject gameObject = obj as GameObject;
		if( gameObject == null )
		{
			if( obj is Component )
			{
				gameObject = (obj as Component).gameObject;
			}
			else
			{
				throw new System.Exception("obj is not GameObject or Component");
			}
		}
		return gameObject;
	}

	public static void AddAnim(Object obj, object target, ParamType paramType, AnimType animType = AnimType.Linear, float timeFactor = 0.1f, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
	{
		GameObject gameObject = ToGameObject(obj);

		switch( paramType )
		{
		case ParamType.Scale:
		case ParamType.ScaleX:
		case ParamType.ScaleY:
		case ParamType.ScaleZ:
		case ParamType.RotationZ:
		case ParamType.Position:
		case ParamType.PositionX:
		case ParamType.PositionY:
		case ParamType.PositionZ:
			Instance.Animations.Add(new TransformAnimInfo(gameObject, target, paramType, animType, timeFactor, delay, endOption));
			break;
		case ParamType.PrimitiveRadius:
		case ParamType.PrimitiveWidth:
		case ParamType.PrimitiveArc:
			Instance.Animations.Add(new PrimitiveAnimInfo(gameObject, target, paramType, animType, timeFactor, delay, endOption));
			break;

		case ParamType.GaugeLength:
		case ParamType.GaugeRate:
		case ParamType.GaugeWidth:
			Instance.Animations.Add(new GaugeAnimInfo(gameObject, target, paramType, animType, timeFactor, delay, endOption));
			break;
		case ParamType.Color:
		case ParamType.AlphaColor:
			Instance.Animations.Add(new ColorAnimInfo(gameObject, target, paramType, animType, timeFactor, delay, endOption));
			break;
		case ParamType.TextColor:
		case ParamType.TextAlphaColor:
			Instance.Animations.Add(new TextAnimInfo(gameObject, target, paramType, animType, timeFactor, delay, endOption));
			break;
		case ParamType.Any:
			break;
		}
	}

	public static void AddShakeAnim(Object obj, object range, float time, float updateTime, ParamType paramType, AnimType animType = AnimType.Time, float delay = 0.0f, AnimEndOption endOption = AnimEndOption.None)
	{
		GameObject gameObject = ToGameObject(obj);
		Instance.Animations.Add(new ShakeAnimInfo(gameObject, range, time, updateTime, paramType, animType, delay, endOption));
	}

	public static bool IsAnimating(Object obj)
	{
		GameObject gameObject = ToGameObject(obj);
		return Instance.Animations.Find((AnimInfoBase anim) => anim.Object == gameObject) != null;
	}

	public static void RemoveOtherAnim(AnimInfoBase animInfo)
	{
		Instance.removeAnims_.AddRange(from other in Instance.Animations
									   where other != animInfo && other.Object == animInfo.Object && other.Param == animInfo.Param && other.IsPlaying
									   select other);
	}

	public static void RemoveOtherAnim(Object obj, ParamType type = ParamType.Any)
	{
		Instance.removeAnims_.AddRange(from other in Instance.Animations
									   where other.Object == ToGameObject(obj) && (type == ParamType.Any || type == other.Param) && other.IsPlaying
									   select other);
	}
}
