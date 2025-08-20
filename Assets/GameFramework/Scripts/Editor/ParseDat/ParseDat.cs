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

    public class GFWVersionAsset
    {
        public string m_Name;

        public GFWVersionAsset(UpdatableVersionList.Asset a)
        {
            m_Name = a.Name;
        }
    }

    public class GFWVersionResource
    {
        public string m_Name;
        public byte m_LoadType;
        public int m_Length;
        public int m_HashCode;

        public GFWVersionResource(UpdatableVersionList.Resource a)
        {
            m_Name = a.Name;
            m_LoadType = a.LoadType;
            m_Length = a.Length;
            m_HashCode = a.HashCode;
        }
    }

    public class GFWVersionFileSystem
    {
        public string m_Name;
        public int[] m_ResourceIndexes;

        public GFWVersionFileSystem(UpdatableVersionList.FileSystem a)
        {
            m_Name = a.Name;
            m_ResourceIndexes = a.GetResourceIndexes();
        }
    }

    public class GFWVersionResourceGroup
    {
        public string m_Name;
        public int[] m_ResourceIndexes;

        public GFWVersionResourceGroup(UpdatableVersionList.ResourceGroup a)
        {
            m_Name = a.Name;
            m_ResourceIndexes = a.GetResourceIndexes();
        }
    }
    
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
    
    public class GFWListResource
    {
        public string m_Name;
        public byte m_LoadType;
        public int m_Length;
        public int m_HashCode;

        public GFWListResource(LocalVersionList.Resource rs)
        {
            m_Name = rs.Name;
            m_LoadType = rs.LoadType;
            m_Length = rs.Length;
            m_HashCode = rs.HashCode;
        }
    }
    
    public class GFWListFilesystem
    {
        public string m_Name;
        public int[] m_ResourceIndexes;
        
        public GFWListFilesystem(LocalVersionList.FileSystem fs)
        {
            m_Name = fs.Name;
            m_ResourceIndexes = fs.GetResourceIndexes();
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

                string directory = Path.GetDirectoryName(path);
                var data = new GFWList(versionList);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(Path.Combine(directory, select.name + ".json"), json);

                Debug.Log($"GameFrameworkList解析完成，输出文件: {Path.Combine(directory, select.name + ".json")}");
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

                string directory = Path.GetDirectoryName(path);
                var data = new GFWVersionList(versionList);
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(Path.Combine(directory, select.name + ".json"), json);

                Debug.Log($"GameFrameworkVersion解析完成{(isCompressed ? "（已解压缩）" : "")}，输出文件: {Path.Combine(directory, select.name + ".json")}");
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