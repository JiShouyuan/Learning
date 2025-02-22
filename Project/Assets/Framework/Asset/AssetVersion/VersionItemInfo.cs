﻿//一组assetbundle包的描述结构

using System.Collections.Generic;
using UnityEngine;

namespace Framework.Asset
{
    [System.Serializable]
    public class VersionItemInfo
    {
        public Dictionary<string, AssetBundleInfo> AssetBundles;

        public string Version;

        public bool UpdateWholeGroup;

        public VersionItemInfo()
        {
            AssetBundles = new Dictionary<string, AssetBundleInfo>();
        }

        public void Add(string key, AssetBundleInfo value)
        {
            if (this.AssetBundles.ContainsKey(key))
            {
                Debug.Log("AssetBundle重名:" + key);
            }
            else
            {
                this.AssetBundles.Add(key, value);
            }
        }

        public void Refresh(Dictionary<string, AssetBundleInfo> remoteDict)
        {
            AssetBundles.Clear();
            foreach (KeyValuePair<string, AssetBundleInfo> kv in remoteDict)
            {
                AssetBundles.Add(kv.Key, kv.Value);
            }
        }

        public void Refresh(string key, AssetBundleInfo value)
        {
            AssetBundleInfo newABI = new AssetBundleInfo();
            newABI.AssetBundleName = value.AssetBundleName;
            newABI.DependenciesBundleNames = value.DependenciesBundleNames;
            newABI.HashString = value.HashString;
            newABI.Md5 = value.Md5;
            newABI.Crc = value.Crc;
            newABI.State = value.State;
            newABI.FromOutside = value.FromOutside;
            newABI.AssetBundleNameOutside = value.AssetBundleNameOutside;
            newABI.AssetBundleGroupOutside = value.AssetBundleGroupOutside;
            newABI.AssetBundlePathOutside = value.AssetBundlePathOutside;

            if (AssetBundles.ContainsKey(key))
            {
                AssetBundles[key] = newABI;
            }
            else
            {
                AssetBundles.Add(key, newABI);
            }
        }
    }
}