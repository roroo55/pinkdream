using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PinkDream.Tests
{
    public sealed class LevelFlowTests
    {
        [UnityTest]
        public IEnumerator StartHandDragLoadsBirthSceneInsteadOfPrototype()
        {
            yield return SceneManager.LoadSceneAsync("Start");
            yield return null;
            FrozenRagdoll character = Object.FindObjectOfType<FrozenRagdoll>();
            Assert.IsNotNull(Object.FindObjectOfType<StartSceneController>());
            character.ReadMouseInput = false;
            Assert.IsTrue(Object.FindObjectsOfType<Text>().Any(t => t.text == "拖动手到 START，开始游戏"));
            Assert.IsTrue(Object.FindObjectsOfType<Text>().Any(t => t.text == "START"));
            AssertSceneTextCanRender();
            Capture("Start-tested.png");

            Collider2D target = Field<Collider2D>(Object.FindObjectOfType<StartSceneController>(), "startTarget");
            Vector2 start = character.HandPosition(5);
            character.BeginControlledDrag(5, start);
            for (int i = 1; i <= 30 && character != null && SceneManager.GetActiveScene().name == "Start"; i++)
            {
                character.MoveControlledDrag(Vector2.Lerp(start, target.transform.position, i / 30f));
                yield return null;
            }
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != "Level01_BeforeBirth"; i++)
                yield return null;
            Assert.AreEqual("Level01_BeforeBirth", SceneManager.GetActiveScene().name);
            yield return null;
            Assert.AreEqual(2, Object.FindObjectsOfType<FrozenRagdoll>().Length);
            Assert.IsTrue(Object.FindObjectsOfType<Text>().Any(t => t.text == "手牵手，一起握刀切开蛋糕"));
            Capture("BeforeBirth-loaded-tested.png");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator BirthSceneCanOpenDirectlyJoinHandsGripKnifeAndReveal()
        {
            yield return SceneManager.LoadSceneAsync("Level01_BeforeBirth");
            yield return null;
            var level = Object.FindObjectOfType<BeforeBirthLevel>();
            FrozenRagdoll mother = Field<FrozenRagdoll>(level, "mother");
            FrozenRagdoll father = Field<FrozenRagdoll>(level, "father");
            Transform knife = Field<Transform>(level, "knife");
            Assert.AreEqual(2, Object.FindObjectsOfType<FrozenRagdoll>().Length);
            AssertSceneTextCanRender();
            Capture("BeforeBirth-direct-tested.png");

            Gesture(level, mother.HandPosition(5), father.HandPosition(3));
            Assert.AreEqual("GripKnife", Field<object>(level, "step").ToString());
            Gesture(level, mother.HandPosition(Field<int>(level, "motherHand")), knife.position);
            Assert.AreEqual("CutCake", Field<object>(level, "step").ToString());
            Collider2D cake = Field<Collider2D>(level, "cakeCutZone");
            Gesture(level, knife.position, new Vector2(knife.position.x, cake.bounds.min.y + 0.04f));
            Assert.AreEqual("Revealed", Field<object>(level, "step").ToString());
            Assert.IsTrue(Field<GameObject>(level, "pinkFilling").activeSelf);
            Assert.IsTrue(Field<ParticleSystem[]>(level, "pinkConfetti").All(p => p.isPlaying));
            Assert.IsTrue(Field<Transform[]>(level, "pinkBalloons").All(p => p.gameObject.activeSelf));
            Vector2[] motherPose = mother.Model.Parts.Select(p => p.Position).ToArray();
            Vector2[] fatherPose = father.Model.Parts.Select(p => p.Position).ToArray();
            for (int i = 0; i < 10; i++)
                yield return null;
            CollectionAssert.AreEqual(motherPose, mother.Model.Parts.Select(p => p.Position).ToArray());
            CollectionAssert.AreEqual(fatherPose, father.Model.Parts.Select(p => p.Position).ToArray());
            Capture("BeforeBirth-revealed-tested.png");
            LogAssert.NoUnexpectedReceived();
        }

        private static T Field<T>(object owner, string name)
            => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void AssertSceneTextCanRender()
        {
            foreach (Text text in Object.FindObjectsOfType<Text>())
            {
                Assert.IsNotNull(text.font, text.name + " is missing its font");
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                Assert.IsNotNull(text.font.material, text.name + " is missing its font material");
                Assert.IsNotNull(text.font.material.mainTexture, text.name + " is missing its font atlas");
                Assert.IsTrue(text.font.HasCharacter('手'), "The scene font must contain Chinese characters");
            }
        }

        private static void Gesture(BeforeBirthLevel level, Vector2 from, Vector2 to)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo begin = typeof(BeforeBirthLevel).GetMethod("BeginGesture", flags);
            MethodInfo move = typeof(BeforeBirthLevel).GetMethod("MoveGesture", flags);
            MethodInfo end = typeof(BeforeBirthLevel).GetMethod("EndGesture", flags);
            begin.Invoke(level, new object[] { from });
            for (int i = 1; i <= 32; i++)
                move.Invoke(level, new object[] { Vector2.Lerp(from, to, i / 32f) });
            end.Invoke(level, null);
        }

        private static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            Camera camera = Camera.main;
            Canvas[] overlays = Object.FindObjectsOfType<Canvas>().Where(c => c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var target = new RenderTexture(1600, 900, 24);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                foreach (Canvas canvas in overlays)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(Path.Combine("Logs", name), image.EncodeToPNG());
            }
            finally
            {
                foreach (Canvas canvas in overlays)
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }
    }
}
