﻿using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

public static class AssetUtils
{
    #region MD5文件校验

    // 生成文件的md5
    public static String BuildFileMd5(String filename)
    {
        String filemd5 = null;
        if (File.Exists(filename))
        {
            try
            {
                using (var fileStream = File.OpenRead(filename))
                {
                    var md5 = MD5.Create();
                    var fileMD5Bytes = md5.ComputeHash(fileStream);
                    filemd5 = FormatMD5(fileMD5Bytes);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
            }
        }

        return filemd5;
    }

    public static Byte[] CreateMD5(Byte[] data)
    {
        using (var md5 = MD5.Create())
        {
            return md5.ComputeHash(data);
        }
    }

    public static string FormatMD5(Byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", "").ToLower();
    }

    #endregion

    public static void Empty(this DirectoryInfo directory)
    {
        foreach (FileInfo file in directory.GetFiles()) file.Delete();
        foreach (DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
    }
}