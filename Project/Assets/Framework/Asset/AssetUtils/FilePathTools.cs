using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public static class FilePathTools
{
    /*========================== streamingAssets目录缓存 =============================*/
#if !UNITY_EDITOR && UNITY_ANDROID && ENABLE_ASSET_DELIVERY
        public static readonly string streamingAssetsPath_Platform = Application.streamingAssetsPath + "/" + ResourcesManager.ASSET_PACK_NAME;
#else
    public static readonly string streamingAssetsPath_Platform = Application.streamingAssetsPath + "/" + targetName;
#endif
#if !UNITY_EDITOR && UNITY_ANDROID
        public static readonly string streamingAssetsPath_Platform_ForWWWLoad = streamingAssetsPath_Platform;
#else
    public static readonly string streamingAssetsPath_Platform_ForWWWLoad = "file:///" + streamingAssetsPath_Platform;
#endif
#if UNITY_ANDROID
    public static readonly string targetName = "android";
#elif UNITY_IPHONE
        public static readonly string targetName = "iphone";
#elif UNITY_STANDALONE_OSX
        public static readonly string targetName = "mac";
#else
    public static readonly string targetName = "win";
#endif
    /*======================== assetbundle打完包后的存放路径 ===========================*/
#if UNITY_EDITOR
    public static readonly string assetbundlePatchPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets")) + "/AssetBundlePatch/" + targetName;
    public static readonly string assetBundleOutPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets")) + "/AssetBundleOut/" + targetName;
    public static readonly string assetbundleMonitorPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets")) + "/AssetBundleMonitor/Data";
    public static readonly string assetBundleOutPath_ForWWWLoad = "file:///" + Application.dataPath + "/AssetBundleOut/" + targetName;
#endif

    /*==================== persistent目录缓存,避免多次字符串拼接 ========================*/
#if UNITY_EDITOR || !UNITY_STANDALONE
    public static readonly string downLoadPath = Application.persistentDataPath + "/DownLoad/";
#else
        public static readonly string downLoadPath = Directory.GetCurrentDirectory() + "/DownLoad/";
#endif
    public static readonly string persistentDataPath_Platform = downLoadPath + targetName;
    public static readonly string persistentDataPath_Platform_ForWWWLoad = "file:///" + persistentDataPath_Platform;

    // 获取文件夹下的所有文件，包括子文件夹 不包含.meta文件
    public static FileInfo[] GetFiles(string path)
    {
        DirectoryInfo folder = new DirectoryInfo(path);

        DirectoryInfo[] subFolders = folder.GetDirectories();
        List<FileInfo> filesList = new List<FileInfo>();

        foreach (DirectoryInfo subFolder in subFolders)
        {
            filesList.AddRange(GetFiles(subFolder.FullName));
        }

        FileInfo[] files = folder.GetFiles();
        foreach (FileInfo file in files)
        {
            if (file.Extension != ".meta")
            {
                filesList.Add(file);
            }
        }

        return filesList.ToArray();
    }

    // 根据正则表达式获取目录下的文件
    public static string[] GetFiles(string path, string regexPattern, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        try
        {
            string[] files = Directory.GetFiles(path, "*", searchOption);
            return SelectEntries(files, regexPattern);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Get files failed from : " + path + "\n" + e);
            return new string[0];
        }
    }

    // 根据名字的正则表达式筛选
    public static string[] SelectEntries(string[] entries, string selectPattern)
    {
        List<string> selectedEntries = new List<string>();
        char[] slashes = "/".ToCharArray();
        foreach (string entry in entries)
        {
            string path = NormalizePath(entry);
            string name = Path.GetFileName(path.TrimEnd(slashes));
            if (Regex.IsMatch(name, selectPattern, RegexOptions.IgnoreCase))
            {
                selectedEntries.Add(path);
            }
        }

        return selectedEntries.ToArray();
    }

    // 获取相对路径
    public static string GetRelativePath(string fullPath)
    {
        string path = NormalizePath(fullPath);
        //path = path.Replace(Application.dataPath,"Assets");
        path = ReplaceFirst(path, Application.dataPath, "Assets");
        return path;
    }

    // 规范化路径名称 修正路径中的正反斜杠
    public static string NormalizePath(string path)
    {
        return path.Replace(@"\", "/");
    }

    // 将短路径拼接成编辑器下的全路径
    public static string GetAssetEditorPath(string path)
    {
        return "Assets/Export/" + path;
    }
    /// <summary>
    /// // 返回bundle包的绝对路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns></returns>
    public static string GetBundleLoadPath(string relativePath)
    {
        return persistentDataPath_Platform + "/" + relativePath;
    }
    // 替换掉第一个遇到的指定字符串
    public static string ReplaceFirst(string str, string oldValue, string newValue)
    {
        int i = str.IndexOf(oldValue, System.StringComparison.Ordinal);
        str = str.Remove(i, oldValue.Length);
        str = str.Insert(i, newValue);
        return str;
    }

    public static void CreateFile(string path, string filename, string info)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        //文件流信息
        StreamWriter sw;
        FileInfo t = new FileInfo(path + "/" + filename);
        sw = t.CreateText();

        //DebugUtil.Log("Version path=" + t);

        //以行的形式写入信息
        sw.WriteLine(info);
        //关闭流
        sw.Close();
        //销毁流
        sw.Dispose();
    }

    //拷贝文件夹
    public static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        DirectoryInfo dir = new DirectoryInfo(sourceDirectory);
        FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();
        foreach (FileSystemInfo i in fileinfo)
        {
            if (i is DirectoryInfo)
            {
                if (!Directory.Exists(Path.Combine(targetDirectory, i.Name)))
                {
                    Directory.CreateDirectory(Path.Combine(targetDirectory, i.Name));
                }

                CopyDirectory(i.FullName, Path.Combine(targetDirectory, i.Name));
            }
            else
            {
                File.Copy(i.FullName, Path.Combine(targetDirectory, i.Name), true);
            }
        }
    }

    // 创建文件目录前的文件夹，保证创建文件的时候不会出现文件夹不存在的情况
    public static void CreateFolderByFilePath(string path)
    {
        FileInfo fi = new FileInfo(path);
        DirectoryInfo dir = fi.Directory;
        if (!dir.Exists)
        {
            dir.Create();
        }
    }

    // 创建目录
    public static void CreateDirectory(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            string dirName = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
        }
    }
    // AssetBundle的下载路径
    public static string AssetBundleDownloadPath
    {
        get
        {

#if UNITY_IPHONE
                return ConfigurationController.Instance.ResServerURL + "iphone";
#elif UNITY_ANDROID
            return ConfigurationController.Instance.ResServerURL + "android";
#else
                return ConfigurationController.Instance.ResServerURL + "mac";
#endif
        }
    }
}