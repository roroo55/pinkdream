using System;
using System.Collections.Generic;
using UnityEngine;

namespace PinkDream
{
    // Visual cut-to-cut surfaces. Refreshed by an explicit pose change, never by an idle update.
    [DisallowMultipleComponent]
    public sealed class RagdollJointOverlap : MonoBehaviour
    {
        [Serializable]
        public sealed class Bridge
        {
            public string name;
            public Transform first, second;
            public Vector2[] firstCut, secondCut;
            public Color color = Color.white;
            public MeshFilter surface;
            [NonSerialized] internal Mesh instance;
        }

        public Bridge[] bridges = Array.Empty<Bridge>();
        private readonly List<Vector2> points = new List<Vector2>(8);
        private readonly List<Vector2> hull = new List<Vector2>(16);

        public void RefreshVisuals()
        {
            foreach (Bridge bridge in bridges)
            {
                if (bridge.instance == null)
                {
                    bridge.instance = new Mesh { name = bridge.name + " · pose surface" };
                    bridge.instance.MarkDynamic();
                    bridge.surface.sharedMesh = bridge.instance;
                }
                BuildSurface(bridge, bridge.instance);
            }
        }

        public void BuildSurface(Bridge bridge, Mesh mesh)
        {
            points.Clear();
            foreach (Vector2 point in bridge.firstCut)
                points.Add(bridge.surface.transform.InverseTransformPoint(bridge.first.TransformPoint(point)));
            foreach (Vector2 point in bridge.secondCut)
                points.Add(bridge.surface.transform.InverseTransformPoint(bridge.second.TransformPoint(point)));
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            hull.Clear();
            foreach (Vector2 point in points)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], point - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            int lower = hull.Count;
            for (int i = points.Count - 2; i >= 0; i--)
            {
                Vector2 point = points[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], point - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }
            if (hull.Count > 1) hull.RemoveAt(hull.Count - 1);
            mesh.Clear();
            if (hull.Count < 3) return;
            int count = hull.Count;
            var vertices = new Vector3[count * 2];
            var colors = new Color[count * 2];
            var triangles = new int[(count - 2) * 6];
            Vector2 centre = Vector2.zero;
            foreach (Vector2 point in hull) centre += point;
            centre /= count;
            Color ink = new Color32(43, 42, 40, 255), fill = bridge.color;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) { ink = ink.linear; fill = fill.linear; }
            for (int i = 0; i < count; i++)
            {
                vertices[i] = hull[i];
                Vector2 inward = centre - hull[i];
                vertices[count + i] = hull[i] + Vector2.ClampMagnitude(inward, 0.009f);
                colors[i] = ink;
                colors[count + i] = fill;
            }
            int cursor = 0;
            for (int layer = 0; layer < 2; layer++)
                for (int i = 1; i < count - 1; i++)
                {
                    triangles[cursor++] = layer * count;
                    triangles[cursor++] = layer * count + i;
                    triangles[cursor++] = layer * count + i + 1;
                }
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // Painted connections are part of the character when checking for a blank restart click.
        public bool ContainsWorldPoint(Vector2 world)
        {
            foreach (Bridge bridge in bridges)
            {
                if (!bridge.surface.gameObject.activeInHierarchy) continue;
                Mesh mesh = bridge.surface.sharedMesh;
                if (mesh == null || mesh.vertexCount < 6) continue;
                Vector2 point = bridge.surface.transform.InverseTransformPoint(new Vector3(world.x, world.y, bridge.surface.transform.position.z));
                Vector3[] vertices = mesh.vertices;
                int count = vertices.Length / 2;
                for (int i = 1; i < count - 1; i++)
                {
                    Vector2 a = vertices[0], b = vertices[i], c = vertices[i + 1];
                    float first = Cross(b - a, point - a), second = Cross(c - b, point - b), third = Cross(a - c, point - c);
                    if ((first >= 0 && second >= 0 && third >= 0) || (first <= 0 && second <= 0 && third <= 0)) return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            foreach (Bridge bridge in bridges)
                if (bridge.instance != null) Destroy(bridge.instance);
        }
    }
}
