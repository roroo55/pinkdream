using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace PinkDream.Tests
{
    public sealed class FrozenRagdollTests
    {
        private readonly struct Pose
        {
            private readonly Vector2 position;
            private readonly float angle;

            public Pose(PoseRagdollModel.Part part)
            {
                position = part.Position;
                angle = part.Angle;
            }

            public void AssertUnchanged(PoseRagdollModel.Part part)
            {
                Assert.AreEqual(position.x, part.Position.x, part.Name + " x changed while frozen");
                Assert.AreEqual(position.y, part.Position.y, part.Name + " y changed while frozen");
                Assert.AreEqual(angle, part.Angle, part.Name + " angle changed while frozen");
            }
        }

        private static Pose[] Snapshot(PoseRagdollModel model)
        {
            var pose = new Pose[model.Parts.Count];
            for (int i = 0; i < pose.Length; i++)
                pose[i] = new Pose(model.Parts[i]);
            return pose;
        }

        private static void AssertSame(PoseRagdollModel model, Pose[] pose)
        {
            for (int i = 0; i < pose.Length; i++)
                pose[i].AssertUnchanged(model.Parts[i]);
        }

        [Test]
        public void InitialPoseHasConnectedJoints()
        {
            var model = new PoseRagdollModel();
            Assert.AreEqual(10, model.Parts.Count);
            Assert.AreEqual(9, model.Joints.Count);
            Assert.Less(model.MaxJointSeparation(), 0.00001f);
        }

        [Test]
        public void PressWithoutMovingDoesNotChangePose()
        {
            var model = new PoseRagdollModel();
            Pose[] original = Snapshot(model);
            Vector2 pointer = model.Parts[3].WorldPoint(new Vector2(0.08f, -0.30f));
            model.BeginDrag(3, pointer);
            for (int i = 0; i < 300; i++)
                Assert.IsFalse(model.MoveDrag(pointer));
            model.EndDrag();
            AssertSame(model, original);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(7)]
        [TestCase(9)]
        public void DragTracksExactGrabPointAndKeepsJointsTogether(int index)
        {
            var model = new PoseRagdollModel();
            Vector2 pointer = model.Parts[index].WorldPoint(new Vector2(0.06f, -0.24f));
            Vector2 originalTorso = model.Parts[0].Position;
            model.BeginDrag(index, pointer);
            for (int i = 1; i <= 48; i++)
            {
                Vector2 target = pointer + new Vector2(Mathf.Sin(i * 0.12f) * 2.8f, i * 0.065f);
                Assert.IsTrue(model.MoveDrag(target));
                Assert.Less(Vector2.Distance(target, model.GrabWorld), 0.00001f, "Grab point must follow the cursor exactly");
                Assert.Less(model.MaxJointSeparation(), 0.012f, "Joint separated while dragging part " + index);
            }
            Assert.Greater(Vector2.Distance(originalTorso, model.Parts[0].Position), 0.1f,
                "Pulling a limb should eventually carry the torso");
        }

        [Test]
        public void HeldStationaryAfterDraggingIsBitwiseFrozen()
        {
            var model = new PoseRagdollModel();
            Vector2 start = model.Parts[3].Position;
            Vector2 target = start + new Vector2(1.8f, 2.4f);
            model.BeginDrag(3, start);
            model.MoveDrag(target);
            Pose[] pose = Snapshot(model);
            for (int i = 0; i < 500; i++)
                Assert.IsFalse(model.MoveDrag(target));
            AssertSame(model, pose);
        }

        [Test]
        public void ReleasedPoseIgnoresFurtherPointerMotion()
        {
            var model = new PoseRagdollModel();
            model.BeginDrag(7, model.Parts[7].Position);
            model.MoveDrag(new Vector2(2f, 1f));
            Pose[] pose = Snapshot(model);
            model.EndDrag();
            for (int i = 0; i < 500; i++)
                Assert.IsFalse(model.MoveDrag(new Vector2(i, -i)));
            AssertSame(model, pose);
        }

        [Test]
        public void FastLongDragDoesNotTearOrProduceInvalidTransforms()
        {
            var model = new PoseRagdollModel();
            model.BeginDrag(5, model.Parts[5].Position);
            Vector2[] targets = { new Vector2(8, 4), new Vector2(-8, -4), new Vector2(6, -3), new Vector2(-6, 3) };
            foreach (Vector2 target in targets)
            {
                model.MoveDrag(target);
                Assert.Less(Vector2.Distance(target, model.GrabWorld), 0.00001f);
                Assert.Less(model.MaxJointSeparation(), 0.012f);
                foreach (PoseRagdollModel.Part part in model.Parts)
                    Assert.IsFalse(float.IsNaN(part.Position.x) || float.IsNaN(part.Position.y) || float.IsNaN(part.Angle));
            }
        }

        [UnityTest]
        public IEnumerator SceneRemainsFrozenAcrossRealFramesBeforeAndAfterDrag()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            FrozenRagdoll ragdoll = Object.FindObjectOfType<FrozenRagdoll>();
            Assert.IsNotNull(ragdoll);
            ragdoll.ReadMouseInput = false;
            Assert.AreEqual(0, ragdoll.GetComponentsInChildren<Rigidbody2D>().Length);
            Pose[] initial = Snapshot(ragdoll.Model);
            for (int i = 0; i < 30; i++)
                yield return null;
            AssertSame(ragdoll.Model, initial);

            for (int i = 0; i < ragdoll.Model.Parts.Count; i++)
                Assert.AreEqual(i, ragdoll.Pick(ragdoll.Model.Parts[i].Position), "Visible part is not draggable: " + i);
            CapturePreview("ragdoll-initial.png");

            Vector2 pointer = ragdoll.Model.Parts[3].WorldPoint(new Vector2(0, -0.4f));
            ragdoll.Model.BeginDrag(3, pointer);
            ragdoll.Model.MoveDrag(pointer + new Vector2(-1.2f, 2.0f));
            ragdoll.RefreshPose();
            Pose[] held = Snapshot(ragdoll.Model);
            for (int i = 0; i < 20; i++)
                yield return null;
            AssertSame(ragdoll.Model, held);

            ragdoll.Model.EndDrag();
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                for (int p = 0; p < held.Length; p++)
                {
                    Transform view = ragdoll.transform.Find("Geometric ragdoll/" + ragdoll.Model.Parts[p].Name);
                    Assert.AreEqual(ragdoll.Model.Parts[p].Position, (Vector2)view.position);
                }
            }
            AssertSame(ragdoll.Model, held);
            CapturePreview("ragdoll-posed.png");
            LogAssert.NoUnexpectedReceived();
        }

        private static void CapturePreview(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;
            Camera camera = Camera.main;
            var target = new RenderTexture(1280, 900, 24);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var image = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes(Path.Combine("Logs", name), image.EncodeToPNG());
                int nonBackground = 0;
                foreach (Color32 pixel in image.GetPixels32())
                    if (pixel.r < 230 && pixel.g < 210 && pixel.b < 230)
                        nonBackground++;
                Assert.Greater(nonBackground, 10000, "The camera did not render the geometric ragdoll");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(image);
            }
        }
    }
}
