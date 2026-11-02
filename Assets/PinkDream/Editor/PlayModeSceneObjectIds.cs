using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PinkDream.Editor
{
    // Remember the source file IDs at scene load, before gameplay reparents or
    // renames anything. Do not resolve runtime clones by their current hierarchy.
    [InitializeOnLoad]
    internal static class PlayModeSceneObjectIds
    {
        private const string Key = "PinkDream.PlayModeSceneEdits.ObjectIds";
        [Serializable] private sealed class Entry { public int instance, scene; public string id; }
        [Serializable] private sealed class Store
        {
            public List<Entry> entries = new List<Entry>();
            public List<int> scenes = new List<int>();
        }
        private static Store store;
        private static Dictionary<int, string> ids;
        private static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+).*$", RegexOptions.Multiline);
        private static Store Data => store ?? (store = JsonUtility.FromJson<Store>(SessionState.GetString(Key, "")) ?? new Store());

        static PlayModeSceneObjectIds()
        {
            SceneManager.sceneLoaded += (scene, mode) => { if (EditorApplication.isPlaying) Capture(scene); };
            SceneManager.sceneUnloaded += scene =>
            {
                if (!EditorApplication.isPlaying) return;
                Data.entries.RemoveAll(e => e.scene == scene.handle); Data.scenes.Remove(scene.handle); Save();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) { store = new Store(); Save(); }
                else if (state == PlayModeStateChange.EnteredPlayMode)
                    for (int i = 0; i < SceneManager.sceneCount; i++) Capture(SceneManager.GetSceneAt(i));
            };
        }

        internal static bool TryGet(Object target, out string id)
        {
            id = "";
            if (!EditorApplication.isPlaying || target == null || EditorUtility.IsPersistent(target)) return false;
            Scene scene = target is Component component ? component.gameObject.scene : target is GameObject go ? go.scene : default;
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.path)) return false;
            if (!Data.scenes.Contains(scene.handle)) Capture(scene);
            if (ids == null) ids = Data.entries.GroupBy(e => e.instance).ToDictionary(g => g.Key, g => g.Last().id);
            return ids.TryGetValue(target.GetInstanceID(), out id);
        }

        private static void Save() { ids = null; SessionState.SetString(Key, JsonUtility.ToJson(Data)); }

        private static void Capture(Scene scene)
        {
            if (Application.isBatchMode || !scene.IsValid() || !scene.isLoaded || Data.scenes.Contains(scene.handle) ||
                !scene.path.StartsWith("Assets/", StringComparison.Ordinal) || !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) || !File.Exists(scene.path)) return;
            Data.scenes.Add(scene.handle);
            try
            {
                string text = File.ReadAllText(scene.path);
                var headers = Header.Matches(text);
                var blocks = new Dictionary<string, string>();
                string roots = null;
                for (int i = 0; i < headers.Count; i++)
                {
                    var match = headers[i]; int end = i + 1 < headers.Count ? headers[i + 1].Index : text.Length;
                    string block = text.Substring(match.Index, end - match.Index);
                    blocks[match.Groups[2].Value] = block;
                    if (match.Groups[1].Value == "1660057539") roots = block;
                }
                if (roots == null) return; // Other serialization formats use the editor ID fallback.
                string guid = AssetDatabase.AssetPathToGUID(scene.path);
                string[] rootIds = References(roots, @"^  - \{fileID: (-?\d+)\}");
                var liveRoots = scene.GetRootGameObjects();
                for (int i = 0; i < Math.Min(rootIds.Length, liveRoots.Length); i++)
                    Walk(rootIds[i], liveRoots[i].transform, scene.handle, guid, blocks);
            }
            catch (Exception error) { Debug.LogWarning("无法建立场景源对象映射，将使用编辑器对象标识：" + scene.path + " " + error.Message); }
            finally { Save(); }
        }

        private static void Walk(string fileId, Transform live, int scene, string guid, Dictionary<string, string> blocks)
        {
            if (!blocks.TryGetValue(fileId, out string transform)) return;
            var goRef = Regex.Match(transform, @"^  m_GameObject: \{fileID: (-?\d+)\}", RegexOptions.Multiline);
            if (!goRef.Success || !blocks.TryGetValue(goRef.Groups[1].Value, out string gameObject)) return;
            var name = Regex.Match(gameObject, @"^  m_Name: (.*)$", RegexOptions.Multiline);
            if (!name.Success) return; // Stripped prefab objects use the native ID fallback.
            string sourceName = name.Groups[1].Value.TrimEnd('\r');
            if (sourceName.StartsWith("\"", StringComparison.Ordinal) && sourceName.EndsWith("\"", StringComparison.Ordinal))
                sourceName = Regex.Unescape(sourceName.Substring(1, sourceName.Length - 2));
            if ((!sourceName.StartsWith("\"", StringComparison.Ordinal) || sourceName.EndsWith("\"", StringComparison.Ordinal)) && sourceName != live.name) return;
            Add(live.gameObject, goRef.Groups[1].Value, scene, guid);
            string[] components = References(gameObject, @"^  - component: \{fileID: (-?\d+)\}");
            var liveComponents = live.GetComponents<Component>();
            for (int i = 0; i < Math.Min(components.Length, liveComponents.Length); i++)
                if (liveComponents[i] != null) Add(liveComponents[i], components[i], scene, guid);
            var children = Regex.Match(transform, @"^  m_Children:.*?(?=^  m_Father:)", RegexOptions.Multiline | RegexOptions.Singleline);
            if (!children.Success) return;
            string[] childIds = References(children.Value, @"^  - \{fileID: (-?\d+)\}");
            for (int i = 0; i < Math.Min(childIds.Length, live.childCount); i++) Walk(childIds[i], live.GetChild(i), scene, guid, blocks);
        }

        private static string[] References(string text, string pattern) => Regex.Matches(text, pattern, RegexOptions.Multiline).Cast<Match>().Select(m => m.Groups[1].Value).ToArray();
        private static void Add(Object target, string source, int scene, string guid)
        {
            if (!long.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out long fileId) || fileId == 0) return;
            Data.entries.Add(new Entry { instance = target.GetInstanceID(), scene = scene,
                id = "GlobalObjectId_V1-2-" + guid + "-" + unchecked((ulong)fileId).ToString(CultureInfo.InvariantCulture) + "-0" });
        }
    }
}
