using UnityEditor;
using UnityEngine;

public static class UnitySetHelper
{
    [MenuItem("Tools/�򿪴浵�ļ���")]
    public static void OpenSavingPath()
    {
        System.Diagnostics.Process.Start("Explorer.exe", Application.persistentDataPath.Replace("/", "\\"));
    }
    [MenuItem("Tools/���PlayerPrefs")]
    public static void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
    }
}
