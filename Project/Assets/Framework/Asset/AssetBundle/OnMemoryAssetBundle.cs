﻿/*-------------------------------------------------------------------------------------------
// 模块描述：AssetBundle包在内存里的缓存情况
//-------------------------------------------------------------------------------------------*/
using UnityEngine;

namespace Framework.Asset
{
    public class OnMemoryAssetBundle
    {
        public string AssetBundleName { get; private set; }

        public AssetBundle AssetBundle { get; private set; }

        public int RefCount { get; private set; }

        public OnMemoryAssetBundle(AssetBundle assetBundle)
        {
            this.AssetBundleName = assetBundle.name;
            this.AssetBundle = assetBundle;
            this.RefCount = 0;
        }

        public void AddRef()
        {
            this.RefCount++;
        }

        public void Unload()
        {
            this.RefCount--;
        }
    }
}