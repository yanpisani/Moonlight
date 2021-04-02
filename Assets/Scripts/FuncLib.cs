using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


//static keyword means there is only one special singleton thingy for the class. It can't be instanced into multiple objects
public static class FuncLib {
    public static void RandomizeList<T>(ref List<T> list) { //yay T for any type yay T
        int currentIndex = list.Count;
        T temporaryValue;
        int randomIndex;

        // While there remain elements to shuffle...
        while (0 != currentIndex) {

            // Pick a remaining element...
            randomIndex = Mathf.FloorToInt(Random.value * currentIndex);
            currentIndex -= 1;

            // And swap it with the current element.
            temporaryValue = list[currentIndex];
            list[currentIndex] = list[randomIndex];
            list[randomIndex] = temporaryValue;
        }
    }

    public static float RemapRange(float v, float a, float b, float x, float y) {
        return (((Mathf.Clamp(v, a, b) - a) * (y - x)) / (b - a)) + x;
    }

    //returns true or false on random percentage chance c, 0 being 0% and 1 being 100% chance
    public static bool Chance(float c) {
        if (c <= 0.0f) return false;
        if (c >= 1.0f) return true;
        if (float.IsNaN(c)) return false;
        if (Random.value <= c)
            return true;
        else
            return false;
    }

    public static int ClampInt(int n, int min, int max) {
        if (n < min) return min;
        else if (n > max) return max;
        else return n;
    }


    //loads a texture from a file
    public static string crosshairPath = Application.persistentDataPath + "/crosshairs/";
    public static Texture2D LoadTexture(string filePath) {
        Texture2D tex2D;
        byte[] fileBytes;
 
        if (File.Exists(filePath)) {
            fileBytes = File.ReadAllBytes(filePath);
            tex2D = new Texture2D(2, 2, TextureFormat.RGBAFloat, false);
            tex2D.filterMode = FilterMode.Point;
            
            if (tex2D.LoadImage(fileBytes)) {
                return tex2D;
            }
        }  
        return null;
    }
    public static Texture2D LoadCrosshair(string fileName) {
        return LoadTexture(crosshairPath + fileName);
    }


    //colors as int
    public static int ColorToInt(Color color) {
        int n = (int)color.b;
        n += (int)color.g * 1000;
        n += (int)color.r * 1000000;
        return n;
    }
    public static Color IntToColor(int n) {
        Color c = new Color();
        c.r = n / 1000000;
        c.g = (n % 1000000) / 1000;
        c.b = n % 1000;
        return c;
    }
    public static Color RandomColor() {
        return Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f);
    }
    /*public static int GetRed(int n) {
        return n / 1000000;
    }
    public static int GetGreen(int n) {
        return (n % 1000000) / 1000;
    }
    public static int GetBlue(int n) {
        return n % 1000;
    }*/


    //round a float Vector3 to Vector3Int
    public static Vector3Int vectorRoundToInt(Vector3 v) {
        Vector3Int vInt = Vector3Int.zero;
        vInt.Set(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
        return vInt;
    }
}
