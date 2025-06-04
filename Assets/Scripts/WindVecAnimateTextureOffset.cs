using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WindVecAnimateTextureOffset : MonoBehaviour
{
    public Material[] mats;
    public float scrollSpeed = -0.5f;
    public bool animate = true;
    [Range(0,1)]
    public float alfa;
    [Range(0, 1)]
    public float altitude;

    [Range(0, 1)]
    public float altitude_bottom;

    float prevAlfa;
    float prevAltitude;
    float prevAltitude_bottom;
    [Range(0, 1)]
    public float left = 0, right=1, front=0, back=1, speedtrim = 0.85f;
    float prevleft=-1, prevright=-1, prevfront=-1, prevback=-1, prev_speedtrim = -1;
    bool showSpeedTrim = false;
    bool prev_showSpeedTrim = true;

    float[] offsets= { -0.35f,-0.45f,-0.55f};
    //public Text textWindSpeedRange;

    public float windSpeedRangeLower=0;
    public float windSpeedRangeUpper = 0;
    private void Start()
    {
       
    }
    void Update()
    {
        float offset = Time.time * scrollSpeed;
        offsets[0] = Time.time * (scrollSpeed);
        offsets[1] = Time.time * (scrollSpeed);
        offsets[2] = Time.time * (scrollSpeed);

        if (animate)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetTextureOffset("_MainTex", new Vector2(0, -offsets[i] + i*0.1f));
        }

        if(prevAlfa!=alfa)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_AlfaCorrection", alfa);
            prevAlfa = alfa;
        }else
        if (prevAltitude != altitude)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_MaxAltutude", altitude);
            prevAltitude = altitude;
        }
        else
        if (prevAltitude_bottom != altitude_bottom)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_MinAltutude", altitude_bottom);
            prevAltitude_bottom = altitude_bottom;
        }
        else
        if (prevleft != left)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_xLeft", left);
            prevleft = left;
        }else
        if (prevright != right)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_xRight", right);
            prevright = right;
        }else
        if (prevfront != front)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_zFront", front);
            prevfront = front;
        }else
        if (prevback != back)
        {
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_zBack", back);
            prevback = back;
        }else
        if (prev_showSpeedTrim!=showSpeedTrim || prev_speedtrim != speedtrim)
        {
            int show;
            if (showSpeedTrim) show = 1;
            else show = 0;
            for (int i = 0; i < mats.Length; i++)
                mats[i].SetInt("_showSpeedRange", show);
            prev_showSpeedTrim= showSpeedTrim;

            for (int i = 0; i < mats.Length; i++)
                mats[i].SetFloat("_rangeStart", speedtrim);
            prev_speedtrim = speedtrim;

            windSpeedRangeLower = (speedtrim - 0.1f) * 50;
            windSpeedRangeUpper = (speedtrim + 0.1f) * 50;
            //textWindSpeedRange.text = windSpeedRangeLower.ToString("0.0") + "[mph]  -  "+ windSpeedRangeUpper.ToString("0.0") + "[mph]";
        }

        
    }
    public void ToggleSpeedTrim()
    {
        showSpeedTrim = !showSpeedTrim;
    }

    public void ChangeSpeedTrim(float _speedTrim)
    {
        speedtrim = _speedTrim;
    }

    public void ChangeAlfa(float _alfa)
    {
        alfa = _alfa;
    }
    public void ChangeAltitude(float _alt)
    {
        altitude = _alt;
    }
    public void ChangeAltitudeBottom(float _alt)
    {
        altitude_bottom= _alt;
    }
    /*
    public void Show50mga(bool show)
    {
        if (show)
            altitude = 1f;
        else
        {
            altitude = 0.8f;
            
        }
    }

    public void Show40mga(bool show)
    {
        if (show)
            altitude = 0.8f;
        else
            altitude = 0.6f;
    }

    public void Show30mga(bool show)
    {
        if (show)
            altitude = 0.6f;
        else
            altitude = 0.4f;
    }

    public void Show20mga(bool show)
    {
        if (show)
            altitude = 0.4f;
        else
            altitude = 0.2f;
    }

    public void Show10mga(bool show)
    {
        if (show)
            altitude = 0.2f;
        else
            altitude = 0.0f;
    }*/
}
