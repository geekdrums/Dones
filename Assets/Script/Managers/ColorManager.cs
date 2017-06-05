using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public enum EThemeColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Black,
    White,
}

public enum EBaseColor
{
    White,
    Black,
    Red,
}

public enum EAccentColor
{
    Default,
}

[System.Serializable]
public class BaseColor
{
    public Color Back = Color.black;
    public Color MiddleBack = Color.gray;
    public Color Middle = Color.grey;
    public Color Front = Color.white;
    public bool isLightBack = false;
    public Color Dark { get { return isLightBack ? Front : Back; } }
    public Color Shade { get { return isLightBack ? Middle : MiddleBack; } }
    public Color Light { get { return isLightBack ? MiddleBack : Middle; } }
    public Color Bright { get { return isLightBack ? Back : Front; } }
}

[System.Serializable]
public class ThemeColor
{
    public Color Bright = Color.green;
	public Color Light = Color.Lerp(Color.green, Color.white, 0.5f);
	public Color Shade= Color.Lerp(Color.green, Color.black, 0.5f);
}

public class ColorManager : MonoBehaviour
{
    static ColorManager instance
    {
        get
        {
            if( instance_ == null )
            {
                instance_ = UnityEngine.Object.FindObjectOfType<ColorManager>();
            }
            return instance_;
        }
    }
    static ColorManager instance_;

	public BaseColor[] BaseColors = new BaseColor[] { new BaseColor() };

	public ThemeColor[] ThemeColors = new ThemeColor[] { new ThemeColor() };

    public Material BaseBackMaterial;
    public Material BaseMiddleBackMaterial;
    public Material BaseMiddleMaterial;
    public Material BaseFrontMaterial;

    public Material BaseDarkMaterial;
    public Material BaseShadeMaterial;
    public Material BaseLightMaterial;
    public Material BaseBrightMaterial;

    public Material ThemeBrightMaterial;
    public Material ThemeLightMaterial;
    public Material ThemeShadeMaterial;

	public MidairPrimitive[] BaseBackPrimitives;
	public MidairPrimitive[] BaseFrontPrimitives;
	public MidairPrimitive[] BaseLightPrimitives;
	public Material[] BaseBackMaterials;
	public Material[] BaseFrontMaterials;
	public TextMesh[] BaseFrontTextMeshes;

    public static BaseColor Base { get; private set; }

    public static ThemeColor Theme { get; private set; }

    struct HSV
    {
        public float h, s, v;
    }

    void Start()
    {
        instance_ = this;
        Base = this.BaseColors[0];
        Theme = this.ThemeColors[0];
    }
    void Update()
    {
    }

    public static void SetBaseColor( EBaseColor baseColor )
	{
		Base = instance.BaseColors[(int)baseColor];
        instance.BaseBackMaterial.color = Base.Back;
        instance.BaseMiddleBackMaterial.color = Base.MiddleBack;
        instance.BaseMiddleMaterial.color = Base.Middle;
        instance.BaseFrontMaterial.color = Base.Front;
        instance.BaseBrightMaterial.color = Base.Bright;
        instance.BaseLightMaterial.color = Base.Light;
        instance.BaseShadeMaterial.color = Base.Shade;
        instance.BaseDarkMaterial.color = Base.Dark;

		foreach( MidairPrimitive primitive in instance.BaseBackPrimitives )
		{
			primitive.SetColor(Base.Back);
		}
		foreach( MidairPrimitive primitive in instance.BaseFrontPrimitives )
		{
			primitive.SetColor(Base.Front);
		}
		foreach( MidairPrimitive primitive in instance.BaseLightPrimitives )
		{
			primitive.SetColor(Base.Light);
		}
		foreach( Material material in instance.BaseBackMaterials)
		{
			material.color = Base.Back;
		}
		foreach( Material material in instance.BaseFrontMaterials )
		{
			material.color = Base.Front;
		}
		foreach( TextMesh textMesh in instance.BaseFrontTextMeshes )
		{
			textMesh.color = Base.Front;
		}
    }
    public static void SetThemeColor( EThemeColor themeColor )
	{
		Theme = instance.ThemeColors[(int)themeColor];
        instance.ThemeBrightMaterial.color = Theme.Bright;
        instance.ThemeLightMaterial.color = Theme.Light;
        instance.ThemeShadeMaterial.color = Theme.Shade;
    }
    public static ThemeColor GetThemeColor( EThemeColor themeColor )
	{
		return instance.ThemeColors[(int)themeColor];
    }

    //http://ja.wikipedia.org/wiki/HSV%E8%89%B2%E7%A9%BA%E9%96%93
    static HSV GetHSV( Color c )
    {
        HSV hsv;
        float max = Mathf.Max( c.r, c.g, c.b );
        float min = Mathf.Min( c.r, c.g, c.b );
        hsv.h = max - min;
        if( hsv.h > 0.0f )
        {
            if( max == c.r )
            {
                hsv.h = (c.g - c.b) / hsv.h;
                if( hsv.h < 0.0f )
                {
                    hsv.h += 6.0f;
                }
            }
            else if( max == c.g )
            {
                hsv.h = 2.0f + (c.b - c.r) / hsv.h;
            }
            else
            {
                hsv.h = 4.0f + (c.r - c.g) / hsv.h;
            }
        }
        hsv.h /= 6.0f;
        hsv.s = (max - min);
        if( max != 0.0f )
            hsv.s /= max;
        hsv.v = max;

        return hsv;
    }
    static Color GetRGB( HSV hsv )
    {
        float r = hsv.v;
        float g = hsv.v;
        float b = hsv.v;
        if (hsv.s > 0.0f) {
            hsv.h *= 6.0f;
            int i = (int)hsv.h;
            float f = hsv.h - (float)i;
            switch (i) {
                default:
                case 0:
                    g *= 1 - hsv.s * (1 - f);
                    b *= 1 - hsv.s;
                    break;
                case 1:
                    r *= 1 - hsv.s * f;
                    b *= 1 - hsv.s;
                    break;
                case 2:
                    r *= 1 - hsv.s;
                    b *= 1 - hsv.s * (1 - f);
                    break;
                case 3:
                    r *= 1 - hsv.s;
                    g *= 1 - hsv.s * f;
                    break;
                case 4:
                    r *= 1 - hsv.s * (1 - f);
                    g *= 1 - hsv.s;
                    break;
                case 5:
                    g *= 1 - hsv.s;
                    b *= 1 - hsv.s * f;
                    break;
            }
        }
        return new Color( r, g, b );
    }

    public static Color MakeAlpha( Color color, float alpha )
    {
        return new Color( color.r, color.g, color.b, color.a * alpha );
    }
    public static float Distance( Color color1, Color color2 )
    {
        return Mathf.Abs( color1.r - color2.r ) + Mathf.Abs( color1.g - color2.g )
            + Mathf.Abs( color1.b - color2.b ) + Mathf.Abs( color1.a - color2.a );
    }
}

