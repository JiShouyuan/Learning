using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace Framework.Asset
{
    public static partial class BuildAssetBundles
    {
        #region CheckPatch

        /// <summary>
        /// 检查Patch资源
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="Exception"></exception>
        static void CheckPatch(string path)
        {
            string[] allAB = FilePathTools.GetFiles(path, @"(.*)(\.ab)$", SearchOption.AllDirectories);
            foreach (var p in allAB)
            {
                string manifest = p + ".manifest";
                string back = p + ".back";
                string patch = p + ".patch";
                if (!File.Exists(manifest)) throw new Exception(string.Format("{0} not exist!!!", manifest));
                if (!File.Exists(back)) throw new Exception(string.Format("{0} not exist!!!", back));
                if (!File.Exists(patch)) throw new Exception(string.Format("{0} not exist!!!", patch));
                string md5 = AssetUtils.BuildFileMd5(p);
                string md5_back = AssetUtils.BuildFileMd5(back);
                if (!md5.Equals(md5_back)) throw new Exception(string.Format("{0} not euqal back!!!", p));
                JObject jobj = JObject.Parse(File.ReadAllText(patch));
                string md5_patch = jobj["md5"].ToString();
                if (!md5.Equals(md5_patch)) throw new Exception(string.Format("{0} md5 not equal persisten value!!!", p));
            }

            Debug.Log(string.Format("Patch {0} check success!!!", path));
        }

        #endregion

        #region MarkSpriteAtlasName

        /// <summary>
        /// 记录所有Sprite的atlas名字
        /// 用于sprite查询归属与哪个图集
        /// </summary>
        static Dictionary<string, HashSet<string>> allSpriteAtlasName = new Dictionary<string, HashSet<string>>();

        public static void MarkSpriteAtlasName()
        {
            allSpriteAtlasName.Clear();

            List<string> allABPaths = new List<string>();
            foreach (var g in AssetConfigController.Instance.Groups)
            {
                allABPaths.AddRange(g.GetBundlePaths(true));
            }

            if (AssetConfigController.Instance.ActivityResPaths != null)
            {
                foreach (var p in AssetConfigController.Instance.ActivityResPaths)
                {
                    allABPaths.Add(p);
                }
            }

            foreach (var abPath in allABPaths)
            {
                FileInfo[] files = FilePathTools.GetFiles(string.Format("{0}/{1}/{2}", Application.dataPath, "Export", abPath));
                for (int j = 0; j < files.Length; j++)
                {
                    if (files[j].Extension.Equals(".spriteatlas"))
                    {
                        string path = FilePathTools.GetRelativePath(files[j].FullName);
                        string[] deps = GetDependencies(new[] { path }, true);
                        foreach (var p in deps)
                        {
                            if (!Path.GetExtension(p).Equals(".spriteatlas") && File.Exists(p))
                            {
                                HashSet<string> tempSet;
                                if (!allSpriteAtlasName.TryGetValue(p, out tempSet))
                                {
                                    tempSet = new HashSet<string>();
                                    allSpriteAtlasName.Add(p, tempSet);
                                }

                                tempSet.Add(path);
                            }
                        }
                    }
                }
            }

            string info = JsonConvert.SerializeObject(allSpriteAtlasName);
            FilePathTools.CreateFile(FilePathTools.assetbundlePatchPath, "AllSpriteAtlasName.txt", info);
        }

        #endregion

        #region MarkFilesMD5ForBuildAssetbundle

        static string[] GetDependencies(string[] pathNames, bool recursive)
        {
            List<string> allDeps = new List<string>();
            allDeps.AddRange(AssetDatabase.GetDependencies(pathNames, recursive));
            //补充处理
            foreach (var asset in pathNames)
            {
                string strExt = Path.GetExtension(asset);
                switch (strExt)
                {
                    //图集依赖文件夹，GetDependencies不一定能获取到真实依赖，但文件夹一定能获取到
                    case ".spriteatlas":
                    {
                        string[] deps = AssetDatabase.GetDependencies(asset, false);
                        foreach (var dep in deps)
                        {
                            if (Directory.Exists(dep))
                            {
                                string[] depSprites = AssetDatabase.FindAssets("t:Sprite", new[] { dep });
                                foreach (var tex in depSprites)
                                {
                                    string path = AssetDatabase.GUIDToAssetPath(tex);
                                    if (!allDeps.Exists(x => { return x.Equals(path); }))
                                    {
                                        allDeps.Add(path);
                                    }
                                }
                            }
                        }
                    }
                        break;
                }
            }

            allDeps.Sort();
            return allDeps.ToArray();
        }

        /// <summary>
        /// 记录所有用于打包的文件的md5值，这个要在BuildPipeline.BuildAssetBundles之前调用
        /// 保证计算hash的数据为git上的数据
        /// </summary>
        static Dictionary<string, string> allFilesMD5 = new Dictionary<string, string>();

        static Dictionary<string, string[]> allDepsWithABPathKey = new Dictionary<string, string[]>();

        public static void MarkFilesMD5ForBuildAssetbundle()
        {
            allFilesMD5.Clear();
            allDepsWithABPathKey.Clear();

            List<string> allABPaths = new List<string>();
            foreach (var g in AssetConfigController.Instance.Groups)
            {
                allABPaths.AddRange(g.GetBundlePaths(true));
            }

            if (AssetConfigController.Instance.ActivityResPaths != null)
            {
                foreach (var p in AssetConfigController.Instance.ActivityResPaths)
                {
                    allABPaths.Add(p);
                }
            }

            foreach (var abPath in allABPaths)
            {
                List<string> assetnames = new List<string>();

                FileInfo[] files = FilePathTools.GetFiles(string.Format("{0}/{1}/{2}", Application.dataPath, "Export", abPath));
                for (int j = 0; j < files.Length; j++)
                {
                    string path = FilePathTools.GetRelativePath(files[j].FullName);
                    if (path.Contains(".DS_Store") || path.Contains(".gitkeep"))
                    {
                        continue;
                    }

                    assetnames.Add(path);
                }

                string[] allDeps = GetDependencies(assetnames.ToArray(), true);
                allDepsWithABPathKey[abPath] = allDeps;
                foreach (var p in allDeps)
                {
                    if (!File.Exists(p)) continue;

                    if (p.Substring(p.LastIndexOf(".")) == ".cs") continue;

                    {
                        string path = p;
                        if (!allFilesMD5.ContainsKey(path))
                        {
                            string md5 = AssetUtils.BuildFileMd5(path);
                            allFilesMD5.Add(path, md5);
                        }
                    }

                    {
                        string path = p + ".meta";
                        if (!allFilesMD5.ContainsKey(path))
                        {
                            string md5 = AssetUtils.BuildFileMd5(path);
                            allFilesMD5.Add(path, md5);
                        }
                    }
                }
            }

            string info = JsonConvert.SerializeObject(allFilesMD5);
            FilePathTools.CreateFile(FilePathTools.assetbundlePatchPath, "AllFilesMD5.txt", info);

            info = JsonConvert.SerializeObject(allDepsWithABPathKey);
            FilePathTools.CreateFile(FilePathTools.assetbundlePatchPath, "AllDepsWithABPathKey.txt", info);
        }

        #endregion

        #region MarkAllHashProfile

        /// <summary>
        /// 所有文件的profile
        /// </summary>
        static Dictionary<string, string> allHashProfile = new Dictionary<string, string>();

        public static void MarkAllHashProfile()
        {
            allHashProfile.Clear();

            List<string> allABPaths = new List<string>();
            foreach (var g in AssetConfigController.Instance.Groups)
            {
                allABPaths.AddRange(g.GetBundlePaths(true));
            }

            if (AssetConfigController.Instance.ActivityResPaths != null)
            {
                foreach (var p in AssetConfigController.Instance.ActivityResPaths)
                {
                    allABPaths.Add(p);
                }
            }

            foreach (var abPath in allABPaths)
            {
                FileInfo[] files = FilePathTools.GetFiles(string.Format("{0}/{1}/{2}", Application.dataPath, "Export", abPath));
                for (int j = 0; j < files.Length; j++)
                {
                    if (files[j].Extension.Equals(".spriteatlas"))
                    {
                        string path = FilePathTools.GetRelativePath(files[j].FullName);
                        if (!allHashProfile.ContainsKey(path))
                        {
                            SpriteAtlasHashProfile hashProfile = new SpriteAtlasHashProfile(path);
                            var str = JsonUtility.ToJson(hashProfile);
                            allHashProfile.Add(path, str);
                        }
                    }
                }
            }

            string[] sprites = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets" });
            List<string> errorSprites = new List<string>();
            foreach (var p in sprites)
            {
                string path = AssetDatabase.GUIDToAssetPath(p);
                if (path.LastIndexOf(".asset") != -1)
                {
                    errorSprites.Add(path);
                    continue;
                }

                if (!allHashProfile.ContainsKey(path))
                {
                    SpriteHashPeofile hashProfile = new SpriteHashPeofile(path);
                    var str = JsonUtility.ToJson(hashProfile);
                    allHashProfile.Add(path, str);
                }
            }

            if (errorSprites.Count > 0)
            {
                foreach (var p in errorSprites)
                {
                    Debug.Log($"异常sprite ：{p}");
                }

                throw new Exception("存在异常sprite，详细请查看前面的日志。");
            }

            string info = JsonConvert.SerializeObject(allHashProfile);
            FilePathTools.CreateFile(FilePathTools.assetbundlePatchPath, "AllHashProfile.txt", info);
        }

        #endregion

        #region ReadAllFileMD5File

        public static void ReadAllFileMD5File()
        {
            string path = FilePathTools.assetbundlePatchPath + "/AllFilesMD5.txt";
            if (File.Exists(path))
            {
                allFilesMD5 = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));

                Debug.Log(string.Format("ReadAllFileMD5File size : {0}", allFilesMD5.Count));
            }

            path = FilePathTools.assetbundlePatchPath + "/AllDepsWithABPathKey.txt";
            if (File.Exists(path))
            {
                allDepsWithABPathKey = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(File.ReadAllText(path));

                Debug.Log(string.Format("ReadAllDepsWithABPathKeyFile size : {0}", allDepsWithABPathKey.Count));
            }
        }

        #endregion

        #region ReadAllHashProfile

        public static void ReadAllHashProfile()
        {
            string path = FilePathTools.assetbundlePatchPath + "/AllHashProfile.txt";
            if (File.Exists(path))
            {
                allHashProfile = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path));

                Debug.Log(string.Format("ReadAllHashProfile size : {0}", allHashProfile.Count));
            }
        }

        #endregion

        #region ReadAllSpriteAtlasNameFile

        public static void ReadAllSpriteAtlasNameFile()
        {
            string path = FilePathTools.assetbundlePatchPath + "/AllSpriteAtlasName.txt";
            if (File.Exists(path))
            {
                allSpriteAtlasName = JsonConvert.DeserializeObject<Dictionary<string, HashSet<string>>>(File.ReadAllText(path));

                Debug.Log(string.Format("ReadAllSpriteAtlasNameFile size : {0}", allSpriteAtlasName.Count));
            }
        }

        #endregion

        #region GetAllSpritePaths

        /// <summary>
        /// 获取所有的Sprite路径，单独获取会形成时间瓶颈
        /// </summary>
        private static HashSet<string> allSpritePaths = new HashSet<string>();

        public static void GetAllSpritePaths()
        {
            allSpritePaths.Clear();

            string[] sprites = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets" });
            foreach (var p in sprites)
            {
                string path = AssetDatabase.GUIDToAssetPath(p);
                allSpritePaths.Add(path);
            }
        }

        #endregion

        #region PatchBundleHash

        /// <summary>
        /// 迭代bundle的hash值
        /// </summary>
        /// <param name="buildPathsList">一次BuildPipeline.BuildAssetBundles所有打包路径</param>
        /// <param name="curBuildPath">本次打包的路径</param>
        /// <param name="assets">本次打包包含的所有assets</param>
        /// <param name="bundleHash">一次BuildPipeline.BuildAssetBundles所有bundle的hash值</param>
        /// <param name="needPatchFileInfo">一次BuildPipeline.BuildAssetBundles所有需要迭代的patch文件信息</param>
        static StringBuilder markData = new StringBuilder();

        static void PatchBundleHash(List<string> buildPathsList, string curBuildPath, string bundleName, string[] assets, string absoluteRootPath,
            Dictionary<string, string> bundleHash, Dictionary<string, JObject> needPatchFileInfo, BuildTarget buildTarget)
        {
            string patchFile = string.Format("{0}/{1}.patch", absoluteRootPath, bundleName);
            string hash_persistent = "";
            //string mad_persistent = "";
            string hash_cur = "";
            if (File.Exists(patchFile))
            {
                JObject jobj = JObject.Parse(File.ReadAllText(patchFile));
                hash_persistent = jobj["hash"].ToString();
                //mad_persistent = jobj["md5"].ToString();
            }

            string[] allDeps = allDepsWithABPathKey[curBuildPath];
            List<string> validDeps = new List<string>();
            foreach (var p in allDeps)
            {
                if (!File.Exists(p))
                {
                    continue;
                }

                //依赖的资源如果在本次的其它ab中，则不能算作计算hash的依赖项
                if (null == buildPathsList.Find(x =>
                        x != curBuildPath && 0 == p.IndexOf(string.Format("Assets/Export/{0}/", x))))
                {
                    validDeps.Add(p);
                }
            }

            markData.Clear();
            Dictionary<string, string> validDepsMD5 = new Dictionary<string, string>();
            foreach (var p in validDeps)
            {
                string depExt = Path.GetExtension(p);
                if (depExt.Equals(".cs"))
                {
                    markData.Append(p);
                }
                else if (depExt.Equals(".spriteatlas"))
                {
                    if (!allHashProfile.ContainsKey(p))
                    {
                        Debug.LogFormat("{0} not in allHashProfile with PatchBundleHash oper!!! - param : {1}", p, bundleName);
                        Debug.Log("AssetConfigController.asset在某些项目中是动态配置，这个有可能影响打包，试试将最终配置上传git，可以解决这个问题");
                    }

                    markData.Append(p + allHashProfile[p]);
                }
                else if (allSpritePaths.Contains(p))
                {
                    if (!allFilesMD5.ContainsKey(p))
                    {
                        Debug.LogFormat("{0} not in allFilesMD5 with PatchBundleHash oper!!! - param : {1}", p, bundleName);
                        Debug.Log("AssetConfigController.asset在某些项目中是动态配置，这个有可能影响打包，试试将最终配置上传git，可以解决这个问题");
                    }

                    if (!allHashProfile.ContainsKey(p))
                    {
                        Debug.LogFormat("{0} not in allHashProfile with PatchBundleHash oper!!! - param : {1}", p, bundleName);
                        Debug.Log("AssetConfigController.asset在某些项目中是动态配置，这个有可能影响打包，试试将最终配置上传git，可以解决这个问题");
                    }

                    string md5 = allFilesMD5[p];
                    validDepsMD5.Add(p, md5);
                    markData.Append(p + md5);
                    markData.Append(p + allHashProfile[p]);
                }
                else
                {
                    {
                        string path = p;

                        if (!allFilesMD5.ContainsKey(path))
                        {
                            Debug.LogFormat("{0} not in allFilesMD5 with PatchBundleHash oper!!! - param : {1}", path, bundleName);
                            Debug.Log("AssetConfigController.asset在某些项目中是动态配置，这个有可能影响打包，试试将最终配置上传git，可以解决这个问题");
                        }

                        string md5 = allFilesMD5[path];
                        validDepsMD5.Add(path, md5);
                        markData.Append(path + md5);
                    }

                    {
                        string path = p + ".meta";

                        if (!allFilesMD5.ContainsKey(path))
                        {
                            Debug.LogFormat("{0} not in allFilesMD5 with PatchBundleHash oper!!! - param : {1}", path, bundleName);
                            Debug.Log("AssetConfigController.asset在某些项目中是动态配置，这个有可能影响打包，试试将最终配置上传git，可以解决这个问题");
                        }

                        string md5 = allFilesMD5[path];
                        validDepsMD5.Add(path, md5);
                        markData.Append(path + md5);
                    }
                }
            }

            //预制件依赖的图集
            //这里只需要关心依赖了哪些图集，不需要关心图集的修改
            List<string> atlasDeps = new List<string>();
            foreach (var p in validDeps)
            {
                string strExt = Path.GetExtension(p);
                if (strExt.Equals(".prefab"))
                {
                    string[] deps = AssetDatabase.GetDependencies(p, true);
                    foreach (var dep in deps)
                    {
                        if (File.Exists(dep))
                        {
                            HashSet<string> tempSet;
                            if (allSpriteAtlasName.TryGetValue(dep, out tempSet))
                            {
                                foreach (var atlasPath in tempSet)
                                {
                                    if (!validDeps.Exists(x => { return x.Equals(atlasPath); }) && !atlasDeps.Exists(x => { return x.Equals(atlasPath); }))
                                    {
                                        atlasDeps.Add(atlasPath);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            atlasDeps.Sort();
            foreach (var p in atlasDeps)
            {
                markData.Append(p);
            }

            //asset patch version config
            List<AssetPatchVersion> patchVersion = buildTarget == BuildTarget.Android
                ? AssetPatchVersionConfig.Instance?.AndroidAssetsPatchVersion
                : AssetPatchVersionConfig.Instance?.IOSAssetsPatchVersion;
            int version = 1;
            if (null != patchVersion)
            {
                AssetPatchVersion data = patchVersion.Find(x => x.name == curBuildPath);
                if (null != data)
                {
                    version = data.version;
                }
            }

            markData.Append(version.ToString());

            markData.Append(Application.unityVersion.Substring(0, Application.unityVersion.IndexOf('.')));

            hash_cur = AssetUtils.FormatMD5(AssetUtils.CreateMD5(Encoding.ASCII.GetBytes(markData.ToString())));

            if (hash_persistent != hash_cur)
            {
                string path_old = string.Format("{0}/{1}", absoluteRootPath, bundleName);
                if (File.Exists(path_old))
                    File.Delete(path_old);
                path_old = path_old + ".back";
                if (File.Exists(path_old))
                    File.Delete(path_old);
                path_old = string.Format("{0}/{1}.manifest", absoluteRootPath, bundleName);
                if (File.Exists(path_old))
                    File.Delete(path_old);
                path_old = string.Format("{0}/{1}.patch", absoluteRootPath, bundleName);
                if (File.Exists(path_old))
                    File.Delete(path_old);

                JObject jobRoot = new JObject();
                JObject jobjDeps = new JObject();
                foreach (var p in validDeps)
                {
                    JObject jobItem = new JObject();
                    string strExt = Path.GetExtension(p);
                    if (strExt.Equals(".cs"))
                    {
                    }
                    else if (strExt.Equals(".spriteatlas"))
                    {
                        jobItem["profile"] = JObject.Parse(allHashProfile[p]);
                    }
                    else if (allSpritePaths.Contains(p))
                    {
                        jobItem["md5_self"] = validDepsMD5[p];
                        jobItem["profile"] = JObject.Parse(allHashProfile[p]);
                    }
                    else
                    {
                        jobItem["md5_self"] = validDepsMD5[p];
                        jobItem["md5_meta"] = validDepsMD5[p + ".meta"];
                    }

                    jobjDeps[p] = jobItem;
                }

                foreach (var p in atlasDeps)
                {
                    JObject jobItem = new JObject();
                    jobjDeps[p] = jobItem;
                }

                jobRoot["assets"] = jobjDeps;
                jobRoot["version"] = version;
                jobRoot["hash"] = hash_cur;

                needPatchFileInfo.Add(bundleName, jobRoot);

                Debug.Log(string.Format("asset : {0} need patch", bundleName));
            }

            bundleHash.Add(bundleName, hash_cur);
        }

        #endregion

        #region BuildGroup

        static void BuildGroup(BundleGroup singleGroup, BuildAssetBundleOptions bundleOptions)
        {
            Debug.Log($"BuildGroup : {singleGroup.GroupName}");

            //工程内资源
            List<string> paths = singleGroup.GetBundlePaths(true);
            if (paths != null && paths.Count > 0)
            {
                string atomicPath = $"{FilePathTools.assetbundlePatchPath}/Atomic{singleGroup.GroupName}.txt";
                File.Create(atomicPath).Close();

                string groupName = singleGroup.GroupName;
                string version = singleGroup.Version;
                bool updateWholeGroup = singleGroup.UpdateWholeAB;

                Dictionary<string, string> bundleHash = new Dictionary<string, string>();
                Dictionary<string, JObject> needPatchFileInfo = new Dictionary<string, JObject>();

                List<AssetBundleBuild> buildMap = new List<AssetBundleBuild>();

                //---------------------- 打成AssetBundle包 ----------------------------//
                for (int i = 0; i < paths.Count; i++)
                {
                    List<string> assetnames = new List<string>();
                    AssetBundleBuild build = new AssetBundleBuild
                    {
                        assetBundleName = paths[i] + ".ab"
                    };

                    FileInfo[] files = FilePathTools.GetFiles(string.Format("{0}/{1}/{2}", Application.dataPath, "Export", paths[i]));
                    for (int j = 0; j < files.Length; j++)
                    {
                        //DebugUtil.Log((files[j]);
                        string path = FilePathTools.GetRelativePath(files[j].FullName);
                        if (path.Contains(".DS_Store") || path.Contains(".gitkeep"))
                        {
                            continue;
                        }

                        assetnames.Add(path);
                    }

                    build.assetNames = assetnames.ToArray();
                    buildMap.Add(build);

                    PatchBundleHash(paths, paths[i], paths[i].ToLower() + ".ab", build.assetNames, FilePathTools.assetbundlePatchPath, bundleHash, needPatchFileInfo,
                        EditorUserBuildSettings.activeBuildTarget);
                }

                AssetBundleManifest assetBundleManifest = BuildPipeline.BuildAssetBundles(FilePathTools.assetbundlePatchPath, buildMap.ToArray(), bundleOptions,
                    EditorUserBuildSettings.activeBuildTarget);
                if (assetBundleManifest == null)
                {
                    throw new Exception($"assetBundleManifest is null in {singleGroup.GroupName} group!!!");
                }
                //------------------------------- end ---------------------------------//


                //-------------------- 更新远端和本地的version.txt -----------------------//
                VersionItemInfo remoteVii = new VersionItemInfo
                {
                    Version = version,
                    UpdateWholeGroup = updateWholeGroup
                };

                VersionItemInfo localVii = new VersionItemInfo
                {
                    Version = version,
                    UpdateWholeGroup = updateWholeGroup
                };

                string[] builtAssetBundleNames = assetBundleManifest.GetAllAssetBundles();
                for (var i = 0; i < builtAssetBundleNames.Length; ++i)
                {
                    string assetBundlePath = string.Format("{0}/{1}", FilePathTools.assetbundlePatchPath, builtAssetBundleNames[i]);

                    if (needPatchFileInfo.ContainsKey(builtAssetBundleNames[i]))
                    {
                        //压缩 ab
                        //string tempPath = assetBundlePath + ".temp";
                        //File.Move(assetBundlePath, tempPath);
                        //Zip.Tool.CompressFileLZMA(tempPath, assetBundlePath);
                        //File.Delete(tempPath);

                        //备份 ab
                        string back_ab_path = assetBundlePath + ".back";
                        if (File.Exists(back_ab_path))
                        {
                            File.Delete(back_ab_path);
                        }

                        File.Copy(assetBundlePath, back_ab_path);

                        //生成ab的 patch文件
                        needPatchFileInfo[builtAssetBundleNames[i]]["md5"] = AssetUtils.BuildFileMd5(assetBundlePath);
                        StreamWriter writer = File.CreateText(assetBundlePath + ".patch");
                        writer.Write(needPatchFileInfo[builtAssetBundleNames[i]].ToString());
                        writer.Close();
                        writer.Dispose();
                    }

                    //必须保证hash和ab的md5是一一对应的
                    //自己计算的hash没变，但是unity重新生成了ab，那么需要将back ab覆盖生成的ab，但是保留新的manifest
                    //以上这么做的原因是：为了让ab兼容不同版本的unity，每次发版时都要检查本次打包资源是否有效，那么就需要做到ab资源版本只有唯一一份
                    {
                        string cur_ab_file_md5 = AssetUtils.BuildFileMd5(assetBundlePath);
                        string back_ab_path = assetBundlePath + ".back";
                        string persisten_ab_file_md5 = AssetUtils.BuildFileMd5(back_ab_path);
                        if (cur_ab_file_md5 != persisten_ab_file_md5)
                        {
                            File.Delete(assetBundlePath);
                            File.Copy(back_ab_path, assetBundlePath);

                            Debug.Log(string.Format("force keep old ab : {0}", assetBundlePath));
                        }
                    }

                    string destPath = string.Format("{0}/{1}", exportPath, builtAssetBundleNames[i]);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                    File.Copy(assetBundlePath, destPath);

                    string hash = string.Empty;
                    string md5 = string.Empty;
                    string[] dpBundleNames = { };

                    if (string.IsNullOrEmpty(hash))
                    {
                        hash = bundleHash[builtAssetBundleNames[i]];
                        md5 = AssetUtils.BuildFileMd5(assetBundlePath);
                    }

                    if (dpBundleNames.Length == 0)
                    {
                        dpBundleNames = assetBundleManifest.GetAllDependencies(builtAssetBundleNames[i]);
                    }
                    //DebugUtil.Log("assetBundlePath : " + assetBundlePath + ", md5 : " + md5);

                    AssetBundleInfo remoteAssetBundleInfo = new AssetBundleInfo
                    {
                        AssetBundleName = builtAssetBundleNames[i],
                        DependenciesBundleNames = dpBundleNames,
                        HashString = hash,
                        Md5 = md5,
                        State = AssetState.ExistInDownLoad
                    };
                    remoteVii.Add(remoteAssetBundleInfo.AssetBundleName, remoteAssetBundleInfo);

                    /*
                     * author：qibo.li
                     * date：2020-6-23
                     * des：
                     * 1.因为需要优化覆盖安装后，继续使用下载目录的ab资源，所以需要标记资源的多种状态
                     * 2.废弃以前用HashString为""与否，来判断资源是否已下载。
                     * 3.采用AssetState来标记
                     */
                    AssetBundleInfo localAssetBundleInfo = new AssetBundleInfo
                    {
                        AssetBundleName = builtAssetBundleNames[i],
                        DependenciesBundleNames = dpBundleNames,
                        HashString = hash,
                        Md5 = md5
                    };
                    if (singleGroup.IsInInitialPacket(builtAssetBundleNames[i]))
                    {
                        localAssetBundleInfo.State = AssetState.ExistInDownLoad;
                    }
                    else
                    {
                        localAssetBundleInfo.State = AssetState.NotExist;
                    }

                    localVii.Add(localAssetBundleInfo.AssetBundleName, localAssetBundleInfo);
                }

                RemoteVersion.Add(groupName, remoteVii);
                LocalVersion.Add(groupName, localVii);

                File.Delete(atomicPath);
                //------------------------------- end ---------------------------------//
            }

            //工程外资源
            List<BundleState> outsideBundles = singleGroup.GetOutsideBundles();
            if (outsideBundles != null && outsideBundles.Count > 0)
            {
                VersionItemInfo remoteVii = new VersionItemInfo
                {
                    Version = singleGroup.Version,
                    UpdateWholeGroup = singleGroup.UpdateWholeAB
                };

                VersionItemInfo localVii = new VersionItemInfo
                {
                    Version = singleGroup.Version,
                    UpdateWholeGroup = singleGroup.UpdateWholeAB
                };
                foreach (var p in outsideBundles)
                {
                    string group_name = string.IsNullOrEmpty(p.GroupOutside) ? singleGroup.GroupName : p.GroupOutside;
                    string path_name = string.IsNullOrEmpty(p.PathOutside) ? p.Path : p.PathOutside;

                    VersionItemInfo outsideVersionItem = OutsideAssetVersion[p.NameOutside].GetVersionItemInfo(group_name);
                    if (null == outsideVersionItem) throw new Exception(string.Format("can not find : {0} group in outsideVersion", group_name));

                    string name = path_name.ToLower() + ".ab";
                    if (!outsideVersionItem.AssetBundles.ContainsKey(name)) throw new Exception(string.Format("can not find : {0} asset in outsideVersion", name));
                    AssetBundleInfo outsideAssetBundleInfo = outsideVersionItem.AssetBundles[name];

                    string validName = p.Path.ToLower() + ".ab";
                    AssetBundleInfo remoteAssetBundleInfo = new AssetBundleInfo
                    {
                        AssetBundleName = validName,
                        DependenciesBundleNames = new String[outsideAssetBundleInfo.DependenciesBundleNames.Length],
                        HashString = outsideAssetBundleInfo.HashString,
                        Md5 = outsideAssetBundleInfo.Md5,
                        FromOutside = true,
                        AssetBundleNameOutside = p.NameOutside,
                        AssetBundleGroupOutside = group_name,
                        AssetBundlePathOutside = name,
                        State = AssetState.ExistInDownLoad,
                    };
                    outsideAssetBundleInfo.DependenciesBundleNames.CopyTo(remoteAssetBundleInfo.DependenciesBundleNames, 0);
                    for (int i = 0; i < remoteAssetBundleInfo.DependenciesBundleNames.Length; i++)
                    {
                        BundleState temp_state = singleGroup.GetBundleByPathOutside(remoteAssetBundleInfo.DependenciesBundleNames[i]);
                        if (null != temp_state)
                        {
                            remoteAssetBundleInfo.DependenciesBundleNames[i] = temp_state.Path.ToLower() + ".ab";
                        }
                        else
                        {
                            throw new Exception(string.Format("{0} 依赖 {1},但是在配置中却没有依赖资源。", p.Path, remoteAssetBundleInfo.DependenciesBundleNames[i]));
                        }
                    }

                    remoteVii.Add(remoteAssetBundleInfo.AssetBundleName, remoteAssetBundleInfo);

                    AssetBundleInfo localAssetBundleInfo = new AssetBundleInfo
                    {
                        AssetBundleName = validName,
                        DependenciesBundleNames = new String[outsideAssetBundleInfo.DependenciesBundleNames.Length],
                        HashString = outsideAssetBundleInfo.HashString,
                        Md5 = outsideAssetBundleInfo.Md5,
                        FromOutside = true,
                        AssetBundleNameOutside = p.NameOutside,
                        AssetBundleGroupOutside = group_name,
                        AssetBundlePathOutside = name,
                    };
                    outsideAssetBundleInfo.DependenciesBundleNames.CopyTo(localAssetBundleInfo.DependenciesBundleNames, 0);
                    for (int i = 0; i < localAssetBundleInfo.DependenciesBundleNames.Length; i++)
                    {
                        BundleState temp_state = singleGroup.GetBundleByPathOutside(localAssetBundleInfo.DependenciesBundleNames[i]);
                        if (null != temp_state)
                        {
                            localAssetBundleInfo.DependenciesBundleNames[i] = temp_state.Path.ToLower() + ".ab";
                        }
                        else
                        {
                            throw new Exception(string.Format("{0} 依赖 {1},但是在配置中却没有依赖资源。", p.Path, localAssetBundleInfo.DependenciesBundleNames[i]));
                        }
                    }

                    if (singleGroup.IsInInitialPacket(localAssetBundleInfo.AssetBundleName))
                    {
                        localAssetBundleInfo.State = AssetState.ExistInDownLoad;
                    }
                    else
                    {
                        localAssetBundleInfo.State = AssetState.NotExist;
                    }

                    localVii.Add(localAssetBundleInfo.AssetBundleName, localAssetBundleInfo);
                }

                RemoteVersion.Refresh(singleGroup.GroupName, remoteVii);
                LocalVersion.Refresh(singleGroup.GroupName, localVii);
            }
        }

        #endregion

        #region AfterBuildAllAssetBundleCheck

        /// <summary>
        /// 通用资源打包后检查
        /// </summary>
        static void AfterBuildAllAssetBundleCheck()
        {
            CheckPatch(FilePathTools.assetbundlePatchPath);

            //配置和out一一对应，并且都在patch里能找到
            string[] allOutAB = FilePathTools.GetFiles(exportPath, @"(.*)(\.ab)$", SearchOption.AllDirectories);
            int cnt = 0;
            foreach (BundleGroup group in AssetConfigController.Instance.Groups)
            {
                List<string> paths = group.GetBundlePaths(true);
                foreach (var p in paths)
                {
                    string path_patch = FilePathTools.assetbundlePatchPath + "/" + p.ToLower() + ".ab";
                    string path_out = exportPath + "/" + p.ToLower() + ".ab";
                    if (!File.Exists(path_patch)) throw new Exception(string.Format("{0} not exist!!!", path_patch));
                    if (!File.Exists(path_out)) throw new Exception(string.Format("{0} not exist!!!", path_out));
                    string md5_patch = AssetUtils.BuildFileMd5(path_patch);
                    string md5_out = AssetUtils.BuildFileMd5(path_out);
                    if (!md5_patch.Equals(md5_out)) throw new Exception(string.Format("{0} not equal src patch!!!", path_out));
                }

                cnt += paths.Count;
            }

            if (allOutAB.Length != cnt) throw new Exception("size not equal!!!");

            //检查初包一一对应
            string[] allInitAB = FilePathTools.GetFiles(FilePathTools.streamingAssetsPath_Platform, @"(.*)(\.ab)$", SearchOption.AllDirectories);
            cnt = 0;
            foreach (BundleGroup group in AssetConfigController.Instance.Groups)
            {
                List<string> paths = group.GetInInitialPacketPaths(true);
                foreach (string item in paths)
                {
                    string srcFile = FilePathTools.assetbundlePatchPath + "/" + item.ToLower() + ".ab";
                    string destFile = FilePathTools.streamingAssetsPath_Platform + "/" + item.ToLower() + ".ab";
                    if (!File.Exists(srcFile)) throw new Exception(string.Format("{0} not exist!!!", srcFile));
                    if (!File.Exists(destFile)) throw new Exception(string.Format("{0} not exist!!!", destFile));
                    string md5_src = AssetUtils.BuildFileMd5(srcFile);
                    string md5_dest = AssetUtils.BuildFileMd5(destFile);
                    if (!md5_src.Equals(md5_dest)) throw new Exception(string.Format("{0} not equal src patch!!!", destFile));
                }

                paths.AddRange(group.GetInInitialPacketPaths(false));
                cnt += paths.Count;
            }

            if (allInitAB.Length != cnt) throw new Exception("size not equal!!!");

            //检查2个Version
            string localPath = Application.dataPath + "/Resources/versionfile/Version.txt";
#if UNITY_IOS
            string remotePath = exportPath + "/../" + string.Format("Version.{0}.txt", AssetConfigController.Instance.IOSVersionCode);
#else
            string remotePath = exportPath + "/../" + string.Format("Version.{0}.txt", AssetConfigController.Instance.VersionCode);
#endif

            if (!File.Exists(localPath)) throw new Exception(string.Format("{0} not exist!!!", localPath));
            if (!File.Exists(remotePath)) throw new Exception(string.Format("{0} not exist!!!", remotePath));
            VersionInfo localVersion = JsonConvert.DeserializeObject<VersionInfo>(File.ReadAllText(localPath));
            VersionInfo remoteVersion = JsonConvert.DeserializeObject<VersionInfo>(File.ReadAllText(remotePath));
            foreach (var p in localVersion.ResGroups)
            {
                List<AssetBundleInfo> tempList = new List<AssetBundleInfo>();
                foreach (var a in p.Value.AssetBundles)
                {
                    AssetBundleInfo temp = a.Value;
                    temp.State = AssetState.ExistInDownLoad;
                    tempList.Add(temp);
                }

                foreach (var t in tempList)
                {
                    p.Value.AssetBundles[t.AssetBundleName] = t;
                }
            }

            string localJson = JsonConvert.SerializeObject(localVersion);
            string remoteJson = JsonConvert.SerializeObject(remoteVersion);
            if (!localJson.Equals(remoteJson))
            {
                string checkVersionPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets")) + "/CheckVersion";
                FilePathTools.CreateFile(checkVersionPath, "checkLocalVersion.txt", localJson);
                FilePathTools.CreateFile(checkVersionPath, "checkRemoteVersion.txt", remoteJson);

                throw new Exception("two version not equal in compare with remote!!!");
            }

            VersionInfo checkVersionLocal = JsonConvert.DeserializeObject<VersionInfo>(File.ReadAllText(localPath));
            foreach (var p in checkVersionLocal.ResGroups)
            {
                List<AssetBundleInfo> tempList = new List<AssetBundleInfo>();
                foreach (var a in p.Value.AssetBundles)
                {
                    AssetBundleInfo temp = a.Value;
                    temp.DependenciesBundleNames = null;
                    temp.HashString = "";
                    tempList.Add(temp);
                }

                tempList.Sort((AssetBundleInfo a, AssetBundleInfo b) => { return a.AssetBundleName.CompareTo(b.AssetBundleName); });
                p.Value.AssetBundles.Clear();
                foreach (var t in tempList)
                {
                    p.Value.AssetBundles.Add(t.AssetBundleName, t);
                }
            }

            VersionInfo checkVersionCalculate = new VersionInfo
            {
#if UNITY_IOS
                Version = AssetConfigController.Instance.IOSRootVersion,
#else
                Version = AssetConfigController.Instance.RootVersion,
#endif
                UniqueID = checkVersionLocal.UniqueID,

                UnityVersion = checkVersionLocal.UnityVersion
            };
            foreach (var p in GetCommits(EditorUserBuildSettings.activeBuildTarget))
            {
                checkVersionCalculate.ResPublicLibraryCommits.Add(p);
            }

            foreach (BundleGroup group in AssetConfigController.Instance.Groups)
            {
                VersionItemInfo item = new VersionItemInfo
                {
                    Version = group.Version,
                    UpdateWholeGroup = group.UpdateWholeAB
                };
                List<AssetBundleInfo> list = new List<AssetBundleInfo>();
                foreach (BundleState p in group.Paths)
                {
                    string bundleName = p.Path.ToLower() + ".ab";
                    AssetBundleInfo bundleInfo = new AssetBundleInfo
                    {
                        AssetBundleName = bundleName,
                        DependenciesBundleNames = null,
                        HashString = "",
                    };
                    if (!p.FromOutside)
                    {
                        bundleInfo.Md5 = AssetUtils.BuildFileMd5(string.Format("{0}/{1}", FilePathTools.assetbundlePatchPath, bundleName));
                    }
                    else
                    {
                        string group_name = string.IsNullOrEmpty(p.GroupOutside) ? group.GroupName : p.GroupOutside;
                        string path_name = string.IsNullOrEmpty(p.PathOutside) ? p.Path : p.PathOutside;

                        VersionItemInfo outsideVersionItem = OutsideAssetVersion[p.NameOutside].GetVersionItemInfo(group_name);
                        if (null == outsideVersionItem) throw new Exception(string.Format("can not find : {0} group in AfterBuildAllAssetBundleCheck", group_name));

                        string name = path_name.ToLower() + ".ab";
                        if (!outsideVersionItem.AssetBundles.ContainsKey(name))
                            throw new Exception(string.Format("can not find : {0} asset in AfterBuildAllAssetBundleCheck", name));

                        AssetBundleInfo remoteAssetBundleInfo = outsideVersionItem.AssetBundles[name];
                        bundleInfo.Md5 = remoteAssetBundleInfo.Md5;
                        bundleInfo.FromOutside = true;
                        bundleInfo.AssetBundleNameOutside = p.NameOutside;
                        bundleInfo.AssetBundleGroupOutside = group_name;
                        bundleInfo.AssetBundlePathOutside = name;
                    }

                    if (group.IsInInitialPacket(bundleName))
                    {
                        bundleInfo.State = AssetState.ExistInDownLoad;
                    }
                    else
                    {
                        bundleInfo.State = AssetState.NotExist;
                    }

                    list.Add(bundleInfo);
                }

                list.Sort((AssetBundleInfo a, AssetBundleInfo b) => { return a.AssetBundleName.CompareTo(b.AssetBundleName); });
                foreach (var i in list)
                {
                    item.Add(i.AssetBundleName, i);
                }

                checkVersionCalculate.Add(group.GroupName, item);
            }

            string checkLocalJson = JsonConvert.SerializeObject(checkVersionLocal);
            string checkCalculateJson = JsonConvert.SerializeObject(checkVersionCalculate);
            if (!checkLocalJson.Equals(checkCalculateJson))
            {
                string checkVersionPath = Application.dataPath.Substring(0, Application.dataPath.LastIndexOf("/Assets")) + "/CheckVersion";
                FilePathTools.CreateFile(checkVersionPath, "checkLocalVersion.txt", checkLocalJson);
                FilePathTools.CreateFile(checkVersionPath, "checkCalculateVersion.txt", checkCalculateJson);

                throw new Exception("AssetConfigController配置有问题，用打包机上Assets/CheckVersion/checkLocalVersion.txt、checkCalculateVersion.txt文件进行对比，可定位问题。");
            }

            Debug.Log("AfterBuildAllAssetBundleCheck Finish!!!");
        }

        #endregion

        #region BuildPost

        public static int IndexOfEx<T>(this IEnumerable<T> collection, Func<T, bool> condition)
        {
            int ret = -1;
            if (condition != null)
            {
                var iter = collection.GetEnumerator();
                while (iter.MoveNext())
                {
                    ret++;
                    if (condition.Invoke(iter.Current))
                        break;
                }
            }

            return ret;
        }

        /// <summary>
        /// 打包之后执行
        /// </summary>
        public static void BuildPost()
        {
            CheckPatch(FilePathTools.assetbundlePatchPath);

            string backPath = FilePathTools.assetbundlePatchPath + "_Back";

            //核实hash和md5是一一对应的
            if (Directory.Exists(backPath))
            {
                string[] allAB_Patch = FilePathTools.GetFiles(FilePathTools.assetbundlePatchPath, @"(.*)(\.ab)$", SearchOption.AllDirectories);
                string[] allAB_Back = FilePathTools.GetFiles(backPath, @"(.*)(\.ab)$", SearchOption.AllDirectories);
                foreach (var p in allAB_Patch)
                {
                    int index = allAB_Back.IndexOfEx(x => x.Replace(backPath, "").Equals(p.Replace(FilePathTools.assetbundlePatchPath, "")));
                    if (index >= 0)
                    {
                        JObject jobj_patch = JObject.Parse(File.ReadAllText(p + ".patch"));
                        string hash_patch = jobj_patch["hash"].ToString();
                        string md5_patch = jobj_patch["md5"].ToString();

                        JObject jobj_back = JObject.Parse(File.ReadAllText(allAB_Back[index] + ".patch"));
                        string hash_back = jobj_back["hash"].ToString();
                        string md5_back = jobj_back["md5"].ToString();

                        if (hash_patch.Equals(hash_back) && !md5_patch.Equals(md5_back)) throw new Exception(string.Format("{0} hash with md5 not match!!!", p));
                    }
                }
            }

            //打印patch差异
            {
                StringBuilder dump = new StringBuilder();

                dump.AppendLine("----------打印本次更新的ab文件 开始----------");

                string versionPath = Application.dataPath + "/Resources/versionfile/Version.txt";
                if (!File.Exists(versionPath)) throw new Exception(string.Format("{0} not exist!!!", versionPath));
                VersionInfo version = JsonConvert.DeserializeObject<VersionInfo>(File.ReadAllText(versionPath));
                foreach (var group in version.ResGroups)
                {
                    foreach (var ab in group.Value.AssetBundles)
                    {
                        if (!ab.Value.FromOutside)
                        {
                            string ab_patch = string.Format("{0}/{1}.patch", FilePathTools.assetbundlePatchPath, ab.Key);
                            string ab_back = string.Format("{0}/{1}.patch", backPath, ab.Key);
                            if (!File.Exists(ab_patch)) throw new Exception(string.Format("{0} not exist!!!", ab_patch));
                            if (File.Exists(ab_back))
                            {
                                JObject jobj_patch = JObject.Parse(File.ReadAllText(ab_patch));
                                string hash_patch = jobj_patch["hash"].ToString();

                                JObject jobj_back = JObject.Parse(File.ReadAllText(ab_back));
                                string hash_back = jobj_back["hash"].ToString();

                                if (!hash_patch.Equals(hash_back)) dump.AppendLine(ab.Key);
                            }
                            else
                            {
                                dump.AppendLine(ab.Key);
                            }
                        }
                    }
                }

                dump.AppendLine("----------打印本次更新的ab文件 结束----------");

                Debug.Log(dump.ToString());
            }

            //delete patch back
            if (Directory.Exists(backPath))
            {
                DirectoryInfo dir = new DirectoryInfo(backPath);
                dir.Empty();
            }

            Debug.Log("BuildPost Finish!!!");
        }

        #endregion

        #region BuildMultipleActivityAssetBundleWithCommandLine

        public static void BuildMultipleActivityAssetBundleWithCommandLine()
        {
            if (AssetConfigController.Instance.ActivityResPaths != null &&
                AssetConfigController.Instance.ActivityResPaths.Length > 0)
            {
                ReadAllFileMD5File();
                ReadAllHashProfile();
                ReadAllSpriteAtlasNameFile();
                GetAllSpritePaths();

#if !UNITY_STANDALONE && !DISABLE_AB_ENCRYPTION
                BuildPipeline.SetAssetBundleEncryptKey(AssetConfigController.Instance.EnhancedEncryptionSecret);
#endif
                for (int i = 0; i < AssetConfigController.Instance.ActivityResPaths.Length; i++)
                {
                    BuildActivityAssetBundleWithCommandLine(AssetConfigController.Instance.ActivityResPaths[i]);
                }
            }

            AfterBuildActivityAssetBundleCheck();

            Debug.Log("BuildMultipleActivityAssetBundleWithCommandLine finish");
        }

        /// <summary>
        /// 活动打包后检查
        /// </summary>
        static void AfterBuildActivityAssetBundleCheck()
        {
            CheckPatch(FilePathTools.assetbundlePatchPath);

            string patchPath = FilePathTools.assetbundlePatchPath + "/Activity/";
            string outPath = FilePathTools.assetBundleOutPath + "/Activity";

            //配置和out一一对应，并且都在patch里能找到
            string[] allOutAB = FilePathTools.GetFiles(outPath, @"(.*)(\.ab)$", SearchOption.AllDirectories);
            int cnt = 0;
            if (AssetConfigController.Instance.ActivityResPaths != null && AssetConfigController.Instance.ActivityResPaths.Length > 0)
            {
                for (int i = 0; i < AssetConfigController.Instance.ActivityResPaths.Length; i++)
                {
                    string abFileNameNoSuffix = AssetConfigController.Instance.ActivityResPaths[i].Replace('/', '_').ToLower();
                    string path = patchPath + abFileNameNoSuffix + ".ab";
                    if (!File.Exists(path)) throw new Exception(string.Format("{0} not exist!!!", path));
                    string patch = path + ".patch";
                    if (!File.Exists(patch)) throw new Exception(string.Format("{0} not exist!!!", patch));
                    JObject jobj = JObject.Parse(File.ReadAllText(patch));
                    string hash = jobj["hash"].ToString();
                    string md5_patch = AssetUtils.BuildFileMd5(path);
                    string ab_out = outPath + "/" + abFileNameNoSuffix + string.Format("_{0}_{1}.ab", hash, md5_patch);
                    if (!File.Exists(ab_out)) throw new Exception(string.Format("{0} not exist!!!", ab_out));
                    string md5_out = AssetUtils.BuildFileMd5(ab_out);
                    if (!md5_out.Equals(md5_patch)) throw new Exception(string.Format("{0} md5 not equal persisten value!!!", abFileNameNoSuffix));
                }

                cnt += AssetConfigController.Instance.ActivityResPaths.Length;
            }

            if (allOutAB.Length != cnt) throw new Exception("size not equal!!!");

            Debug.Log("AfterBuildActivityAssetBundleCheck Finish!!!");
        }

        static void BuildActivityAssetBundleWithCommandLine(string path = "")
        {
            List<AssetBundleBuild> buildMap = new List<AssetBundleBuild>();
            string topPath = Application.dataPath + "/Export/" + path;
            DirectoryInfo dir = new DirectoryInfo(topPath);
            if (!dir.Exists)
            {
                Debug.Log("该文件夹不存在 : " + topPath);
                return;
            }

            FileInfo[] filesInfo = dir.GetFiles("*.*", SearchOption.AllDirectories);
            if (filesInfo.Length <= 0)
            {
                Debug.LogError("不要对空文件夹进行打包 : " + topPath);
                return;
            }

            string outPath = FilePathTools.assetbundlePatchPath + "/Activity";
            topPath = topPath.Substring((Application.dataPath + "/Export/").Length);

            string abFileNameNoSuffix = topPath.Replace('/', '_').ToLower();
            string abFileName = string.Format("{0}.ab", abFileNameNoSuffix);

            List<string> assetnames = new List<string>();
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = abFileName
            };

            for (int i = 0; i < filesInfo.Length; i++)
            {
                string filePath = filesInfo[i].ToString();
                string objPath = "Assets/" + filePath.Substring(Application.dataPath.Length + 1);
                if (!objPath.EndsWith(".meta", StringComparison.Ordinal))
                {
                    assetnames.Add(objPath);
                }
            }

            build.assetNames = assetnames.ToArray();
            buildMap.Add(build);

            if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);

            Dictionary<string, string> bundleHash = new Dictionary<string, string>();
            Dictionary<string, JObject> needPatchFileInfo = new Dictionary<string, JObject>();

            PatchBundleHash(new List<string>() { path }, path, abFileName, build.assetNames, outPath, bundleHash, needPatchFileInfo, EditorUserBuildSettings.activeBuildTarget);

            AssetBundleManifest mainfest = BuildPipeline.BuildAssetBundles(outPath, buildMap.ToArray(), recommandBundleOptions, EditorUserBuildSettings.activeBuildTarget);

            string assetBundlePath = string.Format("{0}/{1}", outPath, abFileName);
            if (needPatchFileInfo.ContainsKey(abFileName))
            {
                //压缩 ab
                //string tempPath = assetBundlePath + ".temp";
                //File.Move(assetBundlePath, tempPath);
                //Zip.Tool.CompressFileLZMA(tempPath, assetBundlePath);
                //File.Delete(tempPath);

                //备份 ab
                string back_ab_path = assetBundlePath + ".back";
                if (File.Exists(back_ab_path))
                {
                    File.Delete(back_ab_path);
                }

                File.Copy(assetBundlePath, back_ab_path);

                //生成ab的 patch文件
                needPatchFileInfo[abFileName]["md5"] = AssetUtils.BuildFileMd5(assetBundlePath);
                StreamWriter writer = File.CreateText(assetBundlePath + ".patch");
                writer.Write(needPatchFileInfo[abFileName].ToString());
                writer.Close();
                writer.Dispose();
            }

            //必须保证hash和ab的md5是一一对应的
            //自己计算的hash没变，但是unity重新生成了ab，那么需要将back ab覆盖生成的ab，但是保留新的manifest
            {
                string cur_ab_file_md5 = AssetUtils.BuildFileMd5(assetBundlePath);
                string back_ab_path = assetBundlePath + ".back";
                string persisten_ab_file_md5 = AssetUtils.BuildFileMd5(back_ab_path);
                if (cur_ab_file_md5 != persisten_ab_file_md5)
                {
                    File.Delete(assetBundlePath);
                    File.Copy(back_ab_path, assetBundlePath);

                    Debug.Log(string.Format("force keep old ab : {0}", assetBundlePath));
                }
            }

            //拷贝ab到输出目录
            {
                string md5 = AssetUtils.BuildFileMd5(assetBundlePath);
                string hash = bundleHash[abFileName];
                string destPath = string.Format("{0}/{1}_{2}_{3}.ab", FilePathTools.assetBundleOutPath + "/Activity", abFileNameNoSuffix, hash, md5);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                if (File.Exists(destPath))
                {
                    File.Delete(destPath);
                }

                File.Copy(assetBundlePath, destPath);
            }
        }

        #endregion

        /// <summary>
        /// 打包之前执行
        /// </summary>
        public static void BuildPre()
        {
            //回滚数据
            for (int i = 0; i < AssetConfigController.Instance.Groups.Length; i++)
            {
                BundleGroup singleGroup = AssetConfigController.Instance.Groups[i];
                string atomicPath = $"{FilePathTools.assetbundlePatchPath}/Atomic{singleGroup.GroupName}.txt";
                if (!File.Exists(atomicPath))
                {
                    continue;
                }

                List<string> paths = singleGroup.GetBundlePaths(true);
                if (paths != null && paths.Count > 0)
                {
                    foreach (var p in paths)
                    {
                        string localPath = $"{FilePathTools.assetbundlePatchPath}/{p.ToLower()}";
                        string backLocalPatch = $"{FilePathTools.assetbundlePatchPath}_Back/{p.ToLower()}";
                        if (!File.Exists(backLocalPatch + ".ab") ||
                            !File.Exists(backLocalPatch + ".ab.back") ||
                            !File.Exists(backLocalPatch + ".ab.manifest") ||
                            !File.Exists(backLocalPatch + ".ab.patch"))
                        {
                            continue; //尽可能还原，走到这里可能为：刚好打包配置迭代了。
                        }

                        File.Copy(backLocalPatch + ".ab", localPath + ".ab", true);
                        File.Copy(backLocalPatch + ".ab.back", localPath + ".ab.back", true);
                        File.Copy(backLocalPatch + ".ab.manifest", localPath + ".ab.manifest", true);
                        File.Copy(backLocalPatch + ".ab.patch", localPath + ".ab.patch", true);
                    }
                }

                File.Delete(atomicPath);
            }

            if (!AssetConfigController.Instance.IsPathsNoRepetition())
            {
                throw new Exception("Duplicate path in configuration");
            }

            MarkFilesMD5ForBuildAssetbundle();

            MarkAllHashProfile();

            MarkSpriteAtlasName();

            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget, false);

            Debug.Log($"检查Patch 路径 = {FilePathTools.assetbundlePatchPath}");
            CheckPatch(FilePathTools.assetbundlePatchPath);

            string backPath = FilePathTools.assetbundlePatchPath + "_Back";
            if (Directory.Exists(backPath))
            {
                DirectoryInfo dir = new DirectoryInfo(backPath);
                dir.Empty();
            }

            //back patch
            if (Directory.Exists(FilePathTools.assetbundlePatchPath))
            {
                if (!Directory.Exists(backPath))
                {
                    Directory.CreateDirectory(backPath);
                }

                FilePathTools.CopyDirectory(FilePathTools.assetbundlePatchPath, backPath);
            }

            Debug.Log("BuildPre Finish!!!");
        }

        static void BuildAllAssetBundleWithOptions(BuildAssetBundleOptions bundleOptions)
        {
            ReadAllFileMD5File();
            ReadAllHashProfile();
            ReadAllSpriteAtlasNameFile();
            GetAllSpritePaths();

            Caching.ClearCache();

            AssetDatabase.RemoveUnusedAssetBundleNames();

            EditorUtility.DisplayCancelableProgressBar("打包中...", "0%", 0.01f);

            string uniqueID = System.Guid.NewGuid().ToString().Replace("-", "");
            RemoteVersion = new VersionInfo
            {
#if UNITY_IOS
                Version = AssetConfigController.Instance.IOSRootVersion,
#else
                Version = AssetConfigController.Instance.RootVersion,
#endif
                UniqueID = uniqueID,

                UnityVersion = Application.unityVersion
            };

            EditorUtility.DisplayCancelableProgressBar("打包中...", "5%", 0.05f);
            LocalVersion = new VersionInfo
            {
#if UNITY_IOS
                Version = AssetConfigController.Instance.IOSRootVersion,
#else
                Version = AssetConfigController.Instance.RootVersion,
#endif
                UniqueID = uniqueID,

                UnityVersion = Application.unityVersion
            };

            EditorUtility.DisplayCancelableProgressBar("打包中...", "10%", 0.1f);

            if (!Directory.Exists(FilePathTools.assetbundlePatchPath))
                Directory.CreateDirectory(FilePathTools.assetbundlePatchPath);

            if (Directory.Exists(FilePathTools.assetBundleOutPath))
                Directory.Delete(FilePathTools.assetBundleOutPath, true);
            if (!Directory.Exists(exportPath))
                Directory.CreateDirectory(exportPath);

            if (Directory.Exists(FilePathTools.streamingAssetsPath_Platform))
                Directory.Delete(FilePathTools.streamingAssetsPath_Platform, true);


            //----------------- 将公共库commit信息写入version文件 ----------------//
            foreach (var p in GetCommits(EditorUserBuildSettings.activeBuildTarget))
            {
                RemoteVersion.ResPublicLibraryCommits.Add(p);
                LocalVersion.ResPublicLibraryCommits.Add(p);
            }

            //---------------------- 获取外部version ----------------------------//
            OutsideAssetVersion = new Dictionary<string, VersionInfo>();
            foreach (var p in GetCommits(EditorUserBuildSettings.activeBuildTarget))
            {
                VersionInfo info = new VersionInfo();
                RequestResPublicLibraryVersionFile(p, EditorUserBuildSettings.activeBuildTarget, ref info);
                OutsideAssetVersion.Add(p.CustomName, info);
            }

            float deltaProgress = 0.8f / AssetConfigController.Instance.Groups.Length;
            for (int i = 0; i < AssetConfigController.Instance.Groups.Length; i++)
            {
                float currProgress = 0.1f + deltaProgress * i;
                EditorUtility.DisplayCancelableProgressBar("打包中...", currProgress * 100 + "%", currProgress);
                BuildGroup(AssetConfigController.Instance.Groups[i], bundleOptions);
            }

            //---------------------- 保存远端version.txt ----------------------------//
            string remoteVersionJsonStr = JsonConvert.SerializeObject(RemoteVersion);
            // 把version存到exportPath的上一级目录
            DirectoryInfo parent = Directory.GetParent(exportPath);
            string parentPath = parent.ToString();
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                AssetConfigController.Instance.VersionCode = PlayerSettings.Android.bundleVersionCode.ToString();
                FilePathTools.CreateFile(parentPath, string.Format("Version.{0}.txt", AssetConfigController.Instance.VersionCode), remoteVersionJsonStr);
            }
            else
            {
                AssetConfigController.Instance.IOSVersionCode = PlayerSettings.iOS.buildNumber;
                FilePathTools.CreateFile(parentPath, string.Format("Version.{0}.txt", AssetConfigController.Instance.IOSVersionCode), remoteVersionJsonStr);
            }

            EditorUtility.DisplayCancelableProgressBar("打包中...", "85%", 0.85f);

            //---------------------- 保存本地version.txt ----------------------------//
            string localPath = Application.dataPath + "/Resources/versionfile";
            string localVersionJsonStr = JsonConvert.SerializeObject(LocalVersion);
            FilePathTools.CreateFile(localPath, "Version.txt", localVersionJsonStr);
            EditorUtility.DisplayCancelableProgressBar("打包中...", "90%", 0.90f);

            //工程内资源
            //------- 拷贝 InInitialPacket==true 的包到streamingasset文件夹下 ---------//
            foreach (BundleGroup group in AssetConfigController.Instance.Groups)
            {
                List<string> paths = group.GetInInitialPacketPaths(true);
                foreach (string item in paths)
                {
                    string srcFile = FilePathTools.assetbundlePatchPath + "/" + item.ToLower() + ".ab";
                    string destFile = FilePathTools.streamingAssetsPath_Platform + "/" + item.ToLower() + ".ab";
                    FilePathTools.CreateDirectory(destFile);
                    //Zip.Tool.DecompressFileLZMA(srcFile, destFile);
                    File.Copy(srcFile, destFile);
                }
            }

            //工程外资源
            //------- 下载 InInitialPacket==true 的包到streamingasset文件夹下 ---------//
            foreach (BundleGroup group in AssetConfigController.Instance.Groups)
            {
                List<BundleState> paths = group.GetInInitialPacketOutsideBundles();
                foreach (BundleState item in paths)
                {
                    string group_name = string.IsNullOrEmpty(item.GroupOutside) ? group.GroupName : item.GroupOutside;
                    string path_name = string.IsNullOrEmpty(item.PathOutside) ? item.Path : item.PathOutside;
                    string remoteMd5 = OutsideAssetVersion[item.NameOutside].GetAssetBundleMd5(group_name, path_name.ToLower() + ".ab");
                    if (!string.IsNullOrEmpty(remoteMd5))
                    {
                        DownloadResPublicLibraryAssetBundles(
                            GetCommit(item.NameOutside, EditorUserBuildSettings.activeBuildTarget),
                            EditorUserBuildSettings.activeBuildTarget,
                            item,
                            FilePathTools.streamingAssetsPath_Platform,
                            remoteMd5);
                    }
                    else
                    {
                        throw new Exception($"The md5 value of {path_name} is null");
                    }
                }
            }

            Action endAction = () =>
            {
                EditorUtility.DisplayCancelableProgressBar("打包中...", "100%", 1f);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.ClearProgressBar();
            };

            endAction();
            AfterBuildAllAssetBundleCheck();
        }
    }
}