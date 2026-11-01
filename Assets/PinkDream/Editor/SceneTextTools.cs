using System;
using System.Linq;
using PinkDream;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkDream.Editor
{
    [CustomEditor(typeof(SceneTextGroup))]
    public sealed class SceneTextGroupEditor : UnityEditor.Editor
    {
        private int selected;
        public override void OnInspectorGUI()
        {
            var group=(SceneTextGroup)target;var views=group.Views;if(views==null||views.Length==0)return;
            EditorGUILayout.HelpBox("每种状态都是下面的真实 Text 对象。修改文字内容会直接保存到 Scene，游戏只切换显示，不会重写文字。",MessageType.Info);
            selected=Mathf.Clamp(selected,0,views.Length-1);
            selected=EditorGUILayout.Popup("选择文字状态",selected,views.Select((v,i)=>i.ToString("D2")+"  "+(v.text!=null?v.text.text.Replace('\n',' '):"Missing")).ToArray());
            var view=views[selected];EditorGUILayout.ObjectField("场景文字对象",view.text,typeof(Text),true);
            if(view.text!=null)
            {
                EditorGUI.BeginChangeCheck();string content=EditorGUILayout.TextArea(view.text.text,GUILayout.MinHeight(65));if(EditorGUI.EndChangeCheck())ChangeText(view.text,content);
                if(GUILayout.Button("在 Scene 中预览并选中这段文字"))Preview(view.text);
            }
        }
        public static void ChangeText(Text text,string content)
        {
            Undo.RecordObject(text,"Edit scene text");text.text=content;EditorUtility.SetDirty(text);EditorSceneManager.MarkSceneDirty(text.gameObject.scene);Canvas.ForceUpdateCanvases();SceneView.RepaintAll();
        }
        public static void Preview(Text text)
        {
            var group=text.GetComponentsInParent<SceneTextGroup>(true).FirstOrDefault();
            if(group!=null)
            {
                Undo.RecordObjects(group.Views.Select(v=>(UnityEngine.Object)v.text.gameObject).ToArray(),"Preview scene text");
                group.Show(group.Views.First(v=>v.text==text).key);
            }
            foreach(var alpha in text.GetComponentsInParent<CanvasGroup>(true))
                if(alpha.alpha==0){Undo.RecordObject(alpha,"Preview hidden text");alpha.alpha=1;}
            Selection.activeGameObject=text.gameObject;EditorGUIUtility.PingObject(text);EditorSceneManager.MarkSceneDirty(text.gameObject.scene);Canvas.ForceUpdateCanvases();SceneView.RepaintAll();
        }
    }
    [CustomEditor(typeof(ScenePresentation))]
    public sealed class ScenePresentationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();EditorGUILayout.HelpBox("背景和界面已经是场景对象。修改背景 SpriteRenderer 的 Color 会同步 Game 背景；文字可直接在 Text 组件中修改。",MessageType.Info);
            if(GUILayout.Button("Scene 视图对齐 Game 构图"))Align((ScenePresentation)target);
            if(GUILayout.Button("打开当前场景全部文字列表"))SceneTextWindow.Open();
        }
        public static void Align(ScenePresentation layout)
        {
            layout.Refresh();var view=SceneView.lastActiveSceneView;var camera=layout.SceneCamera;if(view==null||camera==null)return;
            view.in2DMode=true;view.orthographic=true;view.LookAtDirect(new Vector3(camera.transform.position.x,camera.transform.position.y,0),camera.transform.rotation,camera.orthographicSize);view.Repaint();
        }
    }
    public sealed class SceneTextWindow : EditorWindow
    {
        private Vector2 scroll;private string search="";
        [MenuItem("Pink Dream/场景文字与画面/全部文字与预览")]
        public static void Open(){GetWindow<SceneTextWindow>("场景文字").Show();}
        [MenuItem("Pink Dream/场景文字与画面/Scene 对齐 Game 构图")]
        private static void Align()
        {var presentation=FindObjectOfType<ScenePresentation>();if(presentation!=null)ScenePresentationEditor.Align(presentation);}
        private void OnGUI()
        {
            EditorGUILayout.LabelField("直接编辑当前 Scene 的文字",EditorStyles.boldLabel);
            search=EditorGUILayout.TextField("搜索",search);
            if(GUILayout.Button("恢复初始文字预览"))
            {
                foreach(var group in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SceneTextGroup>(true)))group.Show(group.InitialKey);
                var start=FindObjectOfType<StartSceneController>();if(start!=null){var data=new SerializedObject(start);var found=(CanvasGroup)data.FindProperty("discoveredStart").objectReferenceValue;if(found!=null)found.alpha=0;}
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());SceneView.RepaintAll();
            }
            scroll=EditorGUILayout.BeginScrollView(scroll);
            foreach(var text in SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Text>(true)).OrderBy(t=>t.transform.parent!=null?t.transform.parent.name:t.name))
            {
                if(search.Length>0&&text.text.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0&&text.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);EditorGUILayout.LabelField(text.transform.parent!=null?text.transform.parent.name+" / "+text.name:text.name,EditorStyles.miniLabel);
                EditorGUI.BeginChangeCheck();string content=EditorGUILayout.TextArea(text.text,GUILayout.MinHeight(35));if(EditorGUI.EndChangeCheck())SceneTextGroupEditor.ChangeText(text,content);
                if(GUILayout.Button("预览 / 选中",GUILayout.Width(100)))SceneTextGroupEditor.Preview(text);EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }
    }
    [InitializeOnLoad]
    internal static class SceneViewAlignment
    {
        static SceneViewAlignment(){EditorSceneManager.sceneOpened+=Opened;}
        private static void Opened(Scene scene,OpenSceneMode mode)
        {
            EditorApplication.delayCall+=()=>{if(EditorApplication.isPlayingOrWillChangePlaymode)return;var p=scene.IsValid()?scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ScenePresentation>(true)).FirstOrDefault():null;if(p!=null)ScenePresentationEditor.Align(p);};
        }
    }
}
