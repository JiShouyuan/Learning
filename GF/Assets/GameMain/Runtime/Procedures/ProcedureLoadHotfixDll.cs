using System;
using System.IO;
using System.Linq;
using GameFramework;
using GameFramework.Event;
using GameFramework.Procedure;
using GameFramework.Resource;
using UnityEngine;
using UnityGameFramework.Runtime;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GameMain
{
    public class ProcedureLoadHotfixDll : ProcedureBase
    {
        /// <summary>
        /// 全部的预加载热更脚本dll
        /// </summary>
        private System.Collections.Generic.List<string> hotfixDlls;

        private bool hotfixListIsLoaded;
        private int totalProgress;
        private int loadedProgress;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
#if !DISABLE_HYBRIDCLR
            GameEntry.Event.Subscribe(LoadHotfixDllEventArgs.EventId, OnLoadHotfixDllCallback);
#endif
            PreloadAndInitData();
        }


        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
#if !DISABLE_HYBRIDCLR
            GameEntry.Event.Unsubscribe(LoadHotfixDllEventArgs.EventId, OnLoadHotfixDllCallback);
#endif
            base.OnLeave(procedureOwner, isShutdown);
        }


        protected override void OnUpdate(ProcedureOwner procedureOwner, float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(procedureOwner, elapseSeconds, realElapseSeconds);
            if (!hotfixListIsLoaded)
            {
                return;
            }

            //加载热更新Dll完成,进入热更逻辑
            if (loadedProgress >= totalProgress)
            {
                loadedProgress = -1;
                var entryFunc = Utility.Assembly.GetType("HotfixEntry")
                    ?.GetMethod("StartHotfixLogic", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (entryFunc == null)
                {
                    Log.Fatal("游戏启动失败, 未找到HotfixEntry.StartHotfixLogic入口函数");
                    return;
                }
#if !DISABLE_HYBRIDCLR
                entryFunc.Invoke(null, new object[] { true });
#else
            entryFunc.Invoke(null, new object[] { false });
#endif
            }
        }

        /// <summary>
        /// 加载热更新dll
        /// </summary>
        private void PreloadAndInitData()
        {
            //显示进度条
            GameEntry.StartView.ShowLoadingProgress();
            totalProgress = 0;
            loadedProgress = 0;
            hotfixListIsLoaded = true;

#if !UNITY_EDITOR && !DISABLE_HYBRIDCLR
        hotfixListIsLoaded = false;
        LoadAotDlls();
        LoadHotfixDlls();
#endif
        }
#if !DISABLE_HYBRIDCLR
        /// <summary>
        /// 补充元数据
        /// </summary>
        private void LoadAotDlls()
        {
            var aotMetaDlls = Resources.LoadAll<TextAsset>(SettingConst.AOT_DLL_DIR);
            totalProgress += aotMetaDlls.Length;
            LoadMetadata(aotMetaDlls);
        }

        private void LoadMetadata(TextAsset[] aotMetaDlls)
        {
            foreach (var dll in aotMetaDlls)
            {
                var resultCode = LoadMetadataForAOT(dll.bytes);
                Debug.Log(Utility.Text.Format("补充元数据:{0}. ret:{1}", dll.name, resultCode));
                if (resultCode == HybridCLR.LoadImageErrorCode.OK)
                {
                    loadedProgress++;
                }
            }
        }

        private void LoadHotfixDlls()
        {
            Debug.Log("开始加载热更新dll");
            var hotfixListFile = AssetsPath.GetCombinePath("Assets", SettingConst.HOT_FIX_DLL_DIR, "HotfixFileList.txt");
            if (GameEntry.Resource.HasAsset(hotfixListFile) == GameFramework.Resource.HasAssetResult.NotExist)
            {
                Log.Fatal("热更新dll列表文件不存在:{0}", hotfixListFile);
                return;
            }

            GameEntry.Resource.LoadAsset(hotfixListFile, new GameFramework.Resource.LoadAssetCallbacks((string assetName, object asset, float duration, object userData) =>
            {
                var textAsset = asset as TextAsset;
                if (textAsset != null)
                {
                    hotfixListIsLoaded = true;
                    hotfixDlls = JsonEx.ToObject<System.Collections.Generic.List<string>>(textAsset.text);
                    totalProgress += hotfixDlls.Count;
                    if (hotfixDlls.Count == 1)
                    {
                        var mainDll = AssetsPath.GetHotfixDll(hotfixDlls.Last());
                        LoadHotfixDll(mainDll, this);
                    }
                    else
                    {
                        for (int i = 0; i < hotfixDlls.Count - 1; i++)
                        {
                            var dllName = hotfixDlls[i];
                            var dllAsset = AssetsPath.GetHotfixDll(dllName);
                            LoadHotfixDll(dllAsset, this);
                        }
                    }
                }
            }));
        }


        private void OnLoadHotfixDllCallback(object sender, GameEventArgs e)
        {
            var args = e as LoadHotfixDllEventArgs;
            if (args.UserData != this)
            {
                return;
            }

            if (args.Assembly == null)
            {
                Debug.LogError($"加载dll失败:{args.DllName}");
                return;
            }

            loadedProgress++;
            GameEntry.StartView.SetLoadingProgress(loadedProgress / (float)totalProgress);

            //所有依赖dll加载完成后再加载Hotfix.dll
            if (hotfixDlls.Contains(args.DllName))
            {
                hotfixDlls.Remove(args.DllName);
                if (hotfixDlls.Count == 1)
                {
                    var mainDll = AssetsPath.GetHotfixDll(hotfixDlls.Last());
                    LoadHotfixDll(mainDll, this);
                }
            }
        }

        /// <summary>
        /// 加载热更文件
        /// </summary>
        /// <param name="dllAssetName"></param>
        /// <param name="userData"></param>
        public void LoadHotfixDll(string dllAssetName, object userData)
        {
            GameEntry.Resource.LoadAsset(dllAssetName, typeof(TextAsset), new LoadAssetCallbacks(OnLoadDllSuccess, OnLoadDllFail), userData);
        }

        /// <summary>
        /// 加载并初始化元数据
        /// </summary>
        /// <param name="dllAssetName"></param>
        /// <param name="loadCallback"></param>
        public void LoadMetadataForAOTAssembly(string dllAssetName, GameFrameworkAction<string, int> loadCallback)
        {
            GameEntry.Resource.LoadAsset(dllAssetName, new LoadAssetCallbacks((assetName, asset, duration, userData) =>
            {
                var textAsset = asset as TextAsset;
                if (textAsset == null) loadCallback.Invoke(dllAssetName, (int)HybridCLR.LoadImageErrorCode.AOT_ASSEMBLY_NOT_FIND);
                else
                {
                    var resultCode = LoadMetadataForAOT(textAsset.bytes);
                    loadCallback.Invoke(dllAssetName, (int)resultCode);
                }
            }, (assetName, status, errorMessage, userData) => { loadCallback.Invoke(dllAssetName, (int)HybridCLR.LoadImageErrorCode.AOT_ASSEMBLY_NOT_FIND); }));
        }

        private void OnLoadDllFail(string assetName, LoadResourceStatus status, string errorMessage, object userData)
        {
            Log.Error("加载{0}失败! Error:{1}", assetName, errorMessage);
            GameEntry.Event.Fire(this, ReferencePool.Acquire<LoadHotfixDllEventArgs>().Fill(Path.GetFileNameWithoutExtension(assetName), null, userData));
        }

        private void OnLoadDllSuccess(string assetName, object asset, float duration, object userData)
        {
            var dllTextAsset = asset as TextAsset;
            System.Reflection.Assembly dllAssembly = null;
            if (dllTextAsset != null)
            {
                try
                {
                    dllAssembly = System.Reflection.Assembly.Load(dllTextAsset.bytes);
                }
                catch (Exception e)
                {
                    Log.Error("Assembly.Load加载热更dll失败:{0},Error:{1}", assetName, e.Message);
                    throw;
                }
            }

            var dllName = Path.GetFileNameWithoutExtension(assetName);
            GameEntry.Event.Fire(this, ReferencePool.Acquire<LoadHotfixDllEventArgs>().Fill(dllName, dllAssembly, userData));
        }

        /// <summary>
        /// 为aot assembly加载原始metadata， 这个代码放aot或者热更新都行。
        /// 一旦加载后，如果AOT泛型函数对应native实现不存在，则自动替换为解释模式执行
        /// </summary>
        private HybridCLR.LoadImageErrorCode LoadMetadataForAOT(byte[] dllBytes)
        {
            return HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HybridCLR.HomologousImageMode.SuperSet);
        }
#endif
    }
}