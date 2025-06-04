using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Utility
{
    public static Vector3 minBoundsWorld = new Vector3(-25.45025f, 0, -31.67083f);
    public static Vector3 maxBoundsWorld = new Vector3(28.11399f, 6.11709f, 33.50433f);

    public static Vector3 minBoundsEarth = new Vector3(-76.3463540900901f, 2.81885f, 37.02204256756757f);
    public static Vector3 maxBoundsEarth = new Vector3(-76.34003974774775f, 54.4616f, 37.027126828828834f);

    public static Vector3 posEarth = new Vector3();

    static double centerEarthLon= -76.343196918918925d;
    static double centerEarthLat = 37.024584698198202d;
    static double centerEarthAlt = (2.81885f + 54.4616f)/2;

    public static Vector3 nPos = new Vector3();

    public static Vector3 GlobalToEarth(Vector3 posGlobal)
    {
        Vector3 res = new Vector3();
        

        Vector3 localPosWorld = (posGlobal - minBoundsWorld);
        Vector3 localBoundsWorld = maxBoundsWorld - minBoundsWorld;
        Vector3 localBoundsEarth = maxBoundsEarth - minBoundsEarth;
       
        nPos = new Vector3(localPosWorld.x / localBoundsWorld.x, localPosWorld.y / localBoundsWorld.y, localPosWorld.z / localBoundsWorld.z);

        Vector3 localPosEarth = new Vector3(nPos.x * localBoundsEarth.x, nPos.y * localBoundsEarth.y, nPos.z * localBoundsEarth.z);
        
        posEarth = localPosEarth + minBoundsEarth;


        Debug.Log(nPos);
        Debug.Log(posEarth);

        return posEarth;
    }


}
