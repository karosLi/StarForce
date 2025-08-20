using System;
using System.Collections.Generic;
using System.IO;
using GameFramework;
using GameFramework.Resource;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace UnityGameFramework.Editor.ResourceTools
{
    [System.Serializable]
    public class GFWVersionList
    {
        public GFWVersionAsset[] Assets;
        public GFWVersionResource[] Resources;
        public GFWVersionFileSystem[] FileSystems;
        public GFWVersionResourceGroup[] ResourceGroups;

        public GFWVersionList(UpdatableVersionList list)
        {
            var assets = list.GetAssets();
            var resources = list.GetResources();
            
            Assets = new GFWVersionAsset[assets.Length];
            for (int i = 0; i < assets.Length; i++)
            {
                // 简化处理，直接使用asset名称
                Assets[i] = new GFWVersionAsset(assets[i]);
            }
            
            Resources = new GFWVersionResource[resources.Length];
            for (int i = 0; i < resources.Length; i++)
            {
                Resources[i] = new GFWVersionResource(resources[i]);
            }
            
            var fileSystems = list.GetFileSystems();
            FileSystems = new GFWVersionFileSystem[fileSystems.Length];
            for (int i = 0; i < fileSystems.Length; i++)
            {
                FileSystems[i] = new GFWVersionFileSystem(fileSystems[i]);
            }
            
            var resourceGroups = list.GetResourceGroups();
            ResourceGroups = new GFWVersionResourceGroup[resourceGroups.Length];
            for (int i = 0; i < resourceGroups.Length; i++)
            {
                ResourceGroups[i] = new GFWVersionResourceGroup(resourceGroups[i]);
            }
        }
    }

    [System.Serializable]
    public class GFWVersionAsset
    {
        public string Name;

        public GFWVersionAsset(UpdatableVersionList.Asset a)
        {
            Name = a.Name;
        }
    }

    [System.Serializable]
    public class GFWVersionResource
    {
        public string Name;
        public string Extension;

        public string Variant;
        public byte LoadType;
        public int Length;
        public int HashCode;


        public GFWVersionResource(UpdatableVersionList.Resource a)
        {
            Name = a.Name;
            Extension = a.Extension;
            Variant = a.Variant;
            LoadType = a.LoadType;
            Length = a.Length;
            HashCode = a.HashCode;
        }
    }

    [System.Serializable]
    public class GFWVersionFileSystem
    {
        public string Name;
        public int[] ResourceIndexes;

        public GFWVersionFileSystem(UpdatableVersionList.FileSystem a)
        {
            Name = a.Name;
            ResourceIndexes = a.GetResourceIndexes();
        }
    }

    [System.Serializable]
    public class GFWVersionResourceGroup
    {
        public string Name;
        public int[] ResourceIndexes;

        public GFWVersionResourceGroup(UpdatableVersionList.ResourceGroup a)
        {
            Name = a.Name;
            ResourceIndexes = a.GetResourceIndexes();
        }
    }
    
    [System.Serializable]
    public class GFWList
    {
        public GFWListResource[] Resources;
        public GFWListFilesystem[] Filesystems;

        public GFWList(LocalVersionList list)
        {
            var resources = list.GetResources();
            Resources = new GFWListResource[resources.Length];
            for (int i = 0; i < resources.Length; i++)
            {
                Resources[i] = new GFWListResource(resources[i]);
            }
            
            var fileSystems = list.GetFileSystems();
            Filesystems = new GFWListFilesystem[fileSystems.Length];
            for (int i = 0; i < fileSystems.Length; i++)
            {
                Filesystems[i] = new GFWListFilesystem(fileSystems[i]);
            }
        }
    }
    
    [System.Serializable]
    public class GFWListResource
    {
        public string Name;
        public string Extension;

        public string Variant;
        public byte LoadType;
        public int Length;
        public int HashCode;

        public GFWListResource(LocalVersionList.Resource rs)
        {
            Name = rs.Name;
            Extension = rs.Extension;
            Variant = rs.Variant;
            LoadType = rs.LoadType;
            Length = rs.Length;
            HashCode = rs.HashCode;
        }
    }
    
    [System.Serializable]
    public class GFWListFilesystem
    {
        public string Name;
        public int[] ResourceIndexes;
        
        public GFWListFilesystem(LocalVersionList.FileSystem fs)
        {
            Name = fs.Name;
            ResourceIndexes = fs.GetResourceIndexes();
        }
    }

    public class ParseDat
    {
        [MenuItem("Assets/GF资源配置解析/解析GameFrameworkList")]
        public static void DeserializeGameFrameworkList()
        {
            var select = Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(select);
            if (!path.EndsWith(".dat"))
            {
                Debug.LogError("所选文件格式不正确！必须是.dat文件");
                return;
            }
            
            try
            {
                var bytes = File.ReadAllBytes(path);
                ReadOnlyVersionListSerializer serializer = new ReadOnlyVersionListSerializer();
                AddGameFrameworkListEvent(serializer);
                var memoryStream = new MemoryStream(bytes, false);
                LocalVersionList versionList = serializer.Deserialize(memoryStream);
                
                if (!versionList.IsValid)
                {
                    Debug.LogError("GameFrameworkList 反序列化失败！");
                    return;
                }

                Debug.Log($"GameFrameworkList 反序列化成功，资源数量: {versionList.GetResources().Length}, 文件系统数量: {versionList.GetFileSystems().Length}");

                string directory = Path.GetDirectoryName(path);
                var data = new GFWList(versionList);
                
                Debug.Log($"转换后数据 - 资源数量: {data.Resources?.Length ?? 0}, 文件系统数量: {data.Filesystems?.Length ?? 0}");
                
                string json = JsonUtility.ToJson(data, true);
                
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    Debug.LogError("JSON序列化结果为空！可能是数据结构问题。");
                    return;
                }
                
                File.WriteAllText(Path.Combine(directory, select.name + ".json"), json);

                Debug.Log($"GameFrameworkList解析完成，输出文件: {Path.Combine(directory, select.name + ".json")}");
                Debug.Log($"JSON内容预览: {json.Substring(0, Math.Min(200, json.Length))}...");
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析GameFrameworkList失败: {e.Message}");
            }
        }

        [MenuItem("Assets/GF资源配置解析/解析GameFrameworkVersion")]
        public static void DeserializeGameFrameworkVersion()
        {
            var select = Selection.activeObject;
            string path = AssetDatabase.GetAssetPath(select);
            if (!path.EndsWith(".dat"))
            {
                Debug.LogError("所选文件格式不正确！必须是.dat文件");
                return;
            }

            try
            {
                // 初始化压缩助手
                string compressionHelperTypeName = "UnityGameFramework.Runtime.DefaultCompressionHelper";
                System.Type compressionHelperType = Utility.Assembly.GetType(compressionHelperTypeName);
                if (compressionHelperType == null)
                {
                    Debug.LogError($"无法找到压缩助手类型: {compressionHelperTypeName}");
                    return;
                }
                
                Utility.Compression.ICompressionHelper compressionHelper = 
                    (Utility.Compression.ICompressionHelper)Activator.CreateInstance(compressionHelperType);
                if (compressionHelper == null)
                {
                    Debug.LogError($"无法创建压缩助手实例: {compressionHelperTypeName}");
                    return;
                }
                Utility.Compression.SetCompressionHelper(compressionHelper);

                var bytes = File.ReadAllBytes(path);
                MemoryStream stream = new MemoryStream(bytes, false);
                
                // 尝试解压缩
                MemoryStream decompressStream = new MemoryStream();
                bool isCompressed = false;
                if (Utility.Compression.Decompress(stream, decompressStream))
                {
                    isCompressed = true;
                    decompressStream.Position = 0;
                    stream = decompressStream;
                }
                else
                {
                    // 如果解压缩失败，使用原始数据
                    stream.Position = 0;
                }

                UpdatableVersionListSerializer serializer = new UpdatableVersionListSerializer();
                AddGameFrameworkVersionEvent(serializer);
                UpdatableVersionList versionList = serializer.Deserialize(stream);
                
                if (!versionList.IsValid)
                {
                    Debug.LogError("GameFrameworkVersion 反序列化失败！");
                    return;
                }

                Debug.Log($"GameFrameworkVersion 反序列化成功，资产数量: {versionList.GetAssets().Length}, 资源数量: {versionList.GetResources().Length}, 文件系统数量: {versionList.GetFileSystems().Length}, 资源组数量: {versionList.GetResourceGroups().Length}");

                string directory = Path.GetDirectoryName(path);
                var data = new GFWVersionList(versionList);
                
                Debug.Log($"转换后数据 - 资产数量: {data.Assets?.Length ?? 0}, 资源数量: {data.Resources?.Length ?? 0}, 文件系统数量: {data.FileSystems?.Length ?? 0}, 资源组数量: {data.ResourceGroups?.Length ?? 0}");
                
                string json = JsonUtility.ToJson(data, true);
                
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    Debug.LogError("JSON序列化结果为空！可能是数据结构问题。");
                    return;
                }
                
                File.WriteAllText(Path.Combine(directory, select.name + ".json"), json);

                Debug.Log($"GameFrameworkVersion解析完成{(isCompressed ? "（已解压缩）" : "")}，输出文件: {Path.Combine(directory, select.name + ".json")}");
                Debug.Log($"JSON内容预览: {json.Substring(0, Math.Min(200, json.Length))}...");
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"解析GameFrameworkVersion失败: {e.Message}");
            }
        }

        private static void AddGameFrameworkListEvent(ReadOnlyVersionListSerializer s)
        {
            s.RegisterDeserializeCallback(0, BuiltinVersionListSerializer.LocalVersionListDeserializeCallback_V0);
            s.RegisterDeserializeCallback(1, BuiltinVersionListSerializer.LocalVersionListDeserializeCallback_V1);
            s.RegisterDeserializeCallback(2, BuiltinVersionListSerializer.LocalVersionListDeserializeCallback_V2);
            // 移除V3回调，如果不存在的话
        }

        private static void AddGameFrameworkVersionEvent(UpdatableVersionListSerializer s)
        {
            s.RegisterDeserializeCallback(0, BuiltinVersionListSerializer.UpdatableVersionListDeserializeCallback_V0);
            s.RegisterDeserializeCallback(1, BuiltinVersionListSerializer.UpdatableVersionListDeserializeCallback_V1);
            s.RegisterDeserializeCallback(2, BuiltinVersionListSerializer.UpdatableVersionListDeserializeCallback_V2);
            // 移除V3回调，如果不存在的话

            s.RegisterTryGetValueCallback(0, BuiltinVersionListSerializer.UpdatableVersionListTryGetValueCallback_V0);
        }
    }
}