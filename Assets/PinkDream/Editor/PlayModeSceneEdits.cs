using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PinkDream.Editor
{
    // Only editor Undo transactions are recorded. Gameplay transforms, tweens,
    // particles and completion state never enter this journal by themselves.
    [InitializeOnLoad]
    public static class PlayModeSceneEdits
    {
        private const string Menu = "Pink Dream/运行时场景修改/自动保留手动修改";
        private const string JournalKey = "PinkDream.PlayModeSceneEdits.Journal";
        private const string ActiveKey = "PinkDream.PlayModeSceneEdits.Active";
        private const string ReportKey = "PinkDream.PlayModeSceneEdits.Report";
        private static readonly string EnabledKey = "PinkDream.KeepPlayEdits." + Hash128.Compute(Application.dataPath);
        private static Journal journal;
        private static bool applying, applyQueued;

        [Serializable] private sealed class Journal { public List<Change> changes = new List<Change>(); }
        [Serializable] private sealed class Change
        {
            public int group, type;
            public bool active = true;
            public string scene, target, name, path, value, reference, parent;
            public Vector3 worldPosition, worldScale;
            public Quaternion worldRotation;
        }
        [Serializable] private sealed class Value
        {
            public Vector2 vector2;
            public Vector3 vector3;
            public Vector4 vector4;
            public Quaternion quaternion;
            public Color color;
            public Rect rect;
            public Bounds bounds;
            public Vector2Int vector2Int;
            public Vector3Int vector3Int;
            public RectInt rectInt;
            public BoundsInt boundsInt;
            public AnimationCurve curve;
        }

        static PlayModeSceneEdits()
        {
            Undo.postprocessModifications += Record;
            Undo.undoRedoEvent += UndoRedo;
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.delayCall += RecoverAfterReload;
        }

        public static bool Enabled => EditorPrefs.GetBool(EnabledKey, true);
        private static Journal Data => journal ?? (journal = JsonUtility.FromJson<Journal>(SessionState.GetString(JournalKey, "")) ?? new Journal());
        private static string Key(Change change) => change.target + "\n" + change.path;
        private static IEnumerable<Change> Effective => Data.changes.Where(c => c.active).GroupBy(Key).Select(g => g.Last());
        public static int PendingCount => Effective.Count();
        public static string LastReport => SessionState.GetString(ReportKey, "");

        [MenuItem(Menu)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(EnabledKey, !Enabled);
            if (!Enabled) ClearPending();
        }
        [MenuItem(Menu, true)]
        private static bool ValidateToggle() { UnityEditor.Menu.SetChecked(Menu, Enabled); return true; }

        [MenuItem("Pink Dream/运行时场景修改/查看待保留修改")]
        public static void OpenWindow() => EditorWindow.GetWindow<PlayModeSceneEditsWindow>("运行修改保留").Show();

        public static string[] PendingDescriptions() => Effective.Select(c => Path.GetFileNameWithoutExtension(c.scene) + " / " + c.name + " / " + c.path).ToArray();

        public static void ClearPending()
        {
            journal = new Journal();
            SaveJournal();
            SessionState.SetBool(ActiveKey, false);
        }
        private static void SaveJournal() => SessionState.SetString(JournalKey, JsonUtility.ToJson(Data));

        private static void StateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode) return;
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ClearPending();
                SessionState.SetBool(ActiveKey, Enabled);
                SessionState.SetString(ReportKey, "");
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Undo.FlushUndoRecordObjects();
                SaveJournal();
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ActiveKey, false))
                QueueAutomaticApply();
        }

        private static void RecoverAfterReload()
        {
            if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode && SessionState.GetBool(ActiveKey, false))
                QueueAutomaticApply();
        }

        private static void QueueAutomaticApply()
        {
            if (applyQueued) return;
            applyQueued = true;
            EditorApplication.delayCall += () =>
            {
                applyQueued = false;
                if (SessionState.GetBool(ActiveKey, false)) ApplyPending();
            };
        }

        private static UndoPropertyModification[] Record(UndoPropertyModification[] modifications)
        {
            if (!Enabled || applying || Application.isBatchMode || !EditorApplication.isPlaying) return modifications;
            // The existing text browser uses Undo for temporary previews too.
            string action = Undo.GetCurrentGroupName();
            if (action == "Preview scene text" || action == "Preview hidden text") return modifications;
            bool changed = false;
            foreach (var modification in modifications)
            {
                var current = modification.currentValue;
                if (current == null || current.target == null || string.IsNullOrEmpty(current.propertyPath)) continue;
                Scene scene = SceneOf(current.target);
                if (!scene.IsValid() || !scene.isLoaded || !scene.path.StartsWith("Assets/", StringComparison.Ordinal) || !scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;
                string id = Id(current.target);
                if (string.IsNullOrEmpty(id) || EditorUtility.IsPersistent(current.target)) continue;
                // Object/component creation, destruction and reparenting are structural
                // operations, not property edits; never copy a runtime hierarchy back.
                if (Structural(current.propertyPath)) continue;
                var serialized = new SerializedObject(current.target);
                var property = serialized.FindProperty(current.propertyPath);
                if (property == null) continue;
                if (property.propertyType == SerializedPropertyType.Generic)
                {
                    var iterator = property.Copy(); var end = property.GetEndProperty();
                    while (iterator.Next(true) && !SerializedProperty.EqualContents(iterator, end))
                        if (iterator.propertyType != SerializedPropertyType.Generic)
                            changed |= RecordProperty(current.target, scene, id, iterator, null);
                }
                else changed |= RecordProperty(current.target, scene, id, property, current);
            }
            if (changed) { SessionState.SetBool(ActiveKey, true); SaveJournal(); }
            return modifications;
        }

        private static bool Structural(string path) => path == "m_Script" || path == "m_Father" || path == "m_RootOrder" ||
            path.StartsWith("m_Children", StringComparison.Ordinal) || path.StartsWith("m_Component", StringComparison.Ordinal) ||
            path.StartsWith("m_Prefab", StringComparison.Ordinal) || path == "m_CorrespondingSourceObject" || path == "m_GameObject" || path == "m_ObjectHideFlags";

        private static bool RecordProperty(Object target, Scene scene, string id, SerializedProperty property, PropertyModification modification)
        {
            var change = new Change { group = Undo.GetCurrentGroup(), scene = scene.path, target = id,
                name = target.name, path = property.propertyPath, type = (int)property.propertyType };
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: change.value = modification?.value ?? property.longValue.ToString(CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean: change.value = modification?.value ?? (property.boolValue ? "1" : "0"); break;
                case SerializedPropertyType.Float: change.value = modification?.value ?? property.doubleValue.ToString("R", CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.String: change.value = modification?.value ?? property.stringValue; break;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character: change.value = modification?.value ?? property.intValue.ToString(CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.ObjectReference:
                    var reference = modification != null ? modification.objectReference : property.objectReferenceValue;
                    change.reference = Id(reference);
                    if (reference != null && string.IsNullOrEmpty(change.reference)) return false;
                    break;
                default:
                    var value = new Value();
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Vector2: value.vector2 = property.vector2Value; break;
                        case SerializedPropertyType.Vector3: value.vector3 = property.vector3Value; break;
                        case SerializedPropertyType.Vector4: value.vector4 = property.vector4Value; break;
                        case SerializedPropertyType.Quaternion: value.quaternion = property.quaternionValue; break;
                        case SerializedPropertyType.Color: value.color = property.colorValue; break;
                        case SerializedPropertyType.Rect: value.rect = property.rectValue; break;
                        case SerializedPropertyType.Bounds: value.bounds = property.boundsValue; break;
                        case SerializedPropertyType.Vector2Int: value.vector2Int = property.vector2IntValue; break;
                        case SerializedPropertyType.Vector3Int: value.vector3Int = property.vector3IntValue; break;
                        case SerializedPropertyType.RectInt: value.rectInt = property.rectIntValue; break;
                        case SerializedPropertyType.BoundsInt: value.boundsInt = property.boundsIntValue; break;
                        case SerializedPropertyType.AnimationCurve: value.curve = property.animationCurveValue; break;
                        default: return false;
                    }
                    change.value = JsonUtility.ToJson(value); break;
            }
            if (target is Transform transform)
            {
                change.parent = Id(transform.parent);
                change.worldPosition = transform.position; change.worldRotation = transform.rotation; change.worldScale = transform.lossyScale;
            }
            // Repeated drag samples within one Undo group replace the previous sample.
            Data.changes.RemoveAll(c => c.group == change.group && c.target == change.target && c.path == change.path);
            Data.changes.Add(change);
            return true;
        }

        private static void UndoRedo(in UndoRedoInfo info)
        {
            if (!EditorApplication.isPlaying || applying || Application.isBatchMode) return;
            bool changed = false;
            int group = info.undoGroup;
            foreach (var change in Data.changes.Where(c => c.group == group))
            { change.active = info.isRedo; changed = true; }
            if (changed) SaveJournal();
        }

        private static Scene SceneOf(Object target) => target is Component component ? component.gameObject.scene : target is GameObject go ? go.scene : default;
        private static string Id(Object target)
        {
            if (target == null) return "";
            if (PlayModeSceneObjectIds.TryGet(target, out string sourceId)) return sourceId;
            var id = GlobalObjectId.GetGlobalObjectIdSlow(target);
            return id.identifierType == 0 || id.targetObjectId == 0 || id.assetGUID.ToString() == new string('0', 32) ? "" : id.ToString();
        }
        private static Object Resolve(string id) => !string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out var parsed) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) : null;

        public static void ApplyPending()
        {
            if (applying || Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += ApplyPending; return; }
            SessionState.SetBool(ActiveKey, false);
            var changes = Effective.ToArray();
            if (changes.Length == 0) return;
            applying = true;
            var opened = new List<Scene>();
            var activeScene = SceneManager.GetActiveScene();
            var savedKeys = new HashSet<string>();
            int savedScenes = 0, applied = 0;
            string backup = Path.Combine("Library", "PinkDream", "PlayModeEditBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            try
            {
                // Resolve all referenced scene objects only after their scenes are loaded.
                var paths = new HashSet<string>(changes.Select(c => c.scene));
                foreach (var change in changes.Where(c => !string.IsNullOrEmpty(c.reference)))
                    if (GlobalObjectId.TryParse(change.reference, out var reference) && reference.identifierType == 2)
                        paths.Add(AssetDatabase.GUIDToAssetPath(reference.assetGUID.ToString()));
                foreach (string path in paths)
                {
                    if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                    var scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded) opened.Add(EditorSceneManager.OpenScene(path, OpenSceneMode.Additive));
                }
                Undo.IncrementCurrentGroup(); int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("保留运行时手动场景修改");
                foreach (var group in changes.GroupBy(c => c.scene))
                {
                    var scene = SceneManager.GetSceneByPath(group.Key);
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    Directory.CreateDirectory(backup);
                    File.Copy(group.Key, Path.Combine(backup, AssetDatabase.AssetPathToGUID(group.Key) + ".unity"), false);
                    var completed = new List<Change>();
                    foreach (var targetChanges in group.GroupBy(c => c.target).OrderBy(g => Depth(Resolve(g.Key))))
                    {
                        var target = Resolve(targetChanges.Key);
                        if (target == null || SceneOf(target).path != scene.path) continue;
                        Undo.RegisterCompleteObjectUndo(target, "保留运行时手动场景修改");
                        var serialized = new SerializedObject(target);
                        var worldEdits = new List<Change>();
                        foreach (var change in targetChanges.OrderBy(c => c.path.EndsWith(".Array.size", StringComparison.Ordinal) ? 0 : 1))
                        {
                            if (target is Transform transform && !(target is RectTransform) && IsPose(change.path) && Id(transform.parent) != (change.parent ?? ""))
                            { worldEdits.Add(change); continue; }
                            var property = serialized.FindProperty(change.path);
                            if (property != null && ApplyValue(property, change)) completed.Add(change);
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        if (target is Transform pose)
                        {
                            // Held items can have a temporary hand parent during Play.
                            // Convert only explicitly edited pose channels to the saved parent.
                            var position = worldEdits.LastOrDefault(c => c.path.StartsWith("m_LocalPosition", StringComparison.Ordinal));
                            var rotation = worldEdits.LastOrDefault(c => c.path.StartsWith("m_LocalRotation", StringComparison.Ordinal));
                            var scale = worldEdits.LastOrDefault(c => c.path.StartsWith("m_LocalScale", StringComparison.Ordinal));
                            if (position != null) pose.position = position.worldPosition;
                            if (rotation != null) pose.rotation = rotation.worldRotation;
                            if (scale != null)
                            {
                                var parentScale = pose.parent == null ? Vector3.one : pose.parent.lossyScale;
                                if (Mathf.Abs(parentScale.x) > .00001f && Mathf.Abs(parentScale.y) > .00001f && Mathf.Abs(parentScale.z) > .00001f)
                                    pose.localScale = new Vector3(scale.worldScale.x / parentScale.x, scale.worldScale.y / parentScale.y, scale.worldScale.z / parentScale.z);
                                else worldEdits.RemoveAll(c => c.path.StartsWith("m_LocalScale", StringComparison.Ordinal));
                            }
                            completed.AddRange(worldEdits);
                        }
                        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                        EditorUtility.SetDirty(target);
                    }
                    if (completed.Count == 0) continue;
                    foreach (var presentation in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ScenePresentation>(true))) presentation.Refresh();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (EditorSceneManager.SaveScene(scene))
                    {
                        savedScenes++; applied += completed.Count;
                        foreach (var change in completed) savedKeys.Add(Key(change));
                    }
                }
                Undo.CollapseUndoOperations(undoGroup);
                Data.changes.RemoveAll(c => savedKeys.Contains(Key(c)));
                SaveJournal();
                string report = "已保留 " + applied + " 项手动修改，保存 " + savedScenes + " 个 Scene。";
                if (PendingCount > 0) report += " 另有 " + PendingCount + " 项未写入，可在运行时场景修改菜单中查看。";
                SessionState.SetString(ReportKey, report);
                if (PendingCount > 0) Debug.LogWarning(report); else Debug.Log(report);
            }
            catch (Exception error)
            {
                SessionState.SetString(ReportKey, "部分修改未保存，记录仍保留，可停止运行后重试。" + error.Message);
                Debug.LogException(error);
            }
            finally
            {
                if (savedKeys.Count > 0)
                {
                    Data.changes.RemoveAll(c => savedKeys.Contains(Key(c)));
                    SaveJournal();
                }
                if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
                foreach (var scene in opened) if (scene.IsValid() && scene.isLoaded && !scene.isDirty) EditorSceneManager.CloseScene(scene, true);
                applying = false;
                SceneView.RepaintAll();
            }
        }

        private static bool IsPose(string path) => path.StartsWith("m_LocalPosition", StringComparison.Ordinal) || path.StartsWith("m_LocalRotation", StringComparison.Ordinal) || path.StartsWith("m_LocalScale", StringComparison.Ordinal);
        private static int Depth(Object target)
        { int depth = 0; for (var transform = target is Component c ? c.transform : (target as GameObject)?.transform; transform != null; transform = transform.parent) depth++; return depth; }

        private static bool ApplyValue(SerializedProperty property, Change change)
        {
            if ((int)property.propertyType != change.type) return false;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (!long.TryParse(change.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return false;
                    property.longValue = integer; return true;
                case SerializedPropertyType.Boolean: property.boolValue = change.value == "1" || string.Equals(change.value, "true", StringComparison.OrdinalIgnoreCase); return true;
                case SerializedPropertyType.Float:
                    if (!double.TryParse(change.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false;
                    property.doubleValue = number; return true;
                case SerializedPropertyType.String: property.stringValue = change.value ?? ""; return true;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                    if (!int.TryParse(change.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)) return false;
                    property.intValue = index; return true;
                case SerializedPropertyType.ObjectReference:
                    Object reference = Resolve(change.reference);
                    if (!string.IsNullOrEmpty(change.reference) && reference == null) return false;
                    property.objectReferenceValue = reference; return true;
                default:
                    var value = JsonUtility.FromJson<Value>(change.value);
                    if (value == null) return false;
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.Vector2: property.vector2Value = value.vector2; break;
                        case SerializedPropertyType.Vector3: property.vector3Value = value.vector3; break;
                        case SerializedPropertyType.Vector4: property.vector4Value = value.vector4; break;
                        case SerializedPropertyType.Quaternion: property.quaternionValue = value.quaternion; break;
                        case SerializedPropertyType.Color: property.colorValue = value.color; break;
                        case SerializedPropertyType.Rect: property.rectValue = value.rect; break;
                        case SerializedPropertyType.Bounds: property.boundsValue = value.bounds; break;
                        case SerializedPropertyType.Vector2Int: property.vector2IntValue = value.vector2Int; break;
                        case SerializedPropertyType.Vector3Int: property.vector3IntValue = value.vector3Int; break;
                        case SerializedPropertyType.RectInt: property.rectIntValue = value.rectInt; break;
                        case SerializedPropertyType.BoundsInt: property.boundsIntValue = value.boundsInt; break;
                        case SerializedPropertyType.AnimationCurve: property.animationCurveValue = value.curve; break;
                        default: return false;
                    }
                    return true;
            }
        }
    }

    public sealed class PlayModeSceneEditsWindow : EditorWindow
    {
        private Vector2 scroll;
        private void OnInspectorUpdate() => Repaint();
        private void OnGUI()
        {
            EditorGUILayout.LabelField(PlayModeSceneEdits.Enabled ? "自动保留已开启" : "自动保留已关闭", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("运行时通过 Scene 或 Inspector 手动修改已有场景物体的属性，停止运行后自动写回原 Scene。位置、旋转、大小、颜色、文字、排序及组件参数均可记录。游戏自动运行的变化不会记录。", MessageType.Info);
            EditorGUILayout.LabelField("待保留属性", PlayModeSceneEdits.PendingCount.ToString());
            if (!string.IsNullOrEmpty(PlayModeSceneEdits.LastReport)) EditorGUILayout.HelpBox(PlayModeSceneEdits.LastReport, MessageType.None);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var description in PlayModeSceneEdits.PendingDescriptions()) EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || PlayModeSceneEdits.PendingCount == 0))
                if (GUILayout.Button("停止运行后重试保留")) PlayModeSceneEdits.ApplyPending();
            using (new EditorGUI.DisabledScope(PlayModeSceneEdits.PendingCount == 0))
                if (GUILayout.Button("清空本次待保留记录")) PlayModeSceneEdits.ClearPending();
        }
    }
}
