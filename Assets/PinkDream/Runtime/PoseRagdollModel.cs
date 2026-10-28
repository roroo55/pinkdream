using System;
using System.Collections.Generic;
using UnityEngine;

namespace PinkDream
{
    /// <summary>
    /// A rigid, articulated 2D pose. There is deliberately no gravity, velocity,
    /// time step or idle simulation: only MoveDrag is allowed to change a pose.
    /// </summary>
    public sealed class PoseRagdollModel
    {
        public sealed class Part
        {
            public readonly string Name;
            public readonly float Length;
            public readonly float Width;
            public readonly bool IsCircle;
            public readonly float InverseMass;
            public readonly float InverseInertia;
            public Vector2 Position { get; internal set; }
            public float Angle { get; internal set; }

            internal Part(string name, Vector2 position, float angle, float length,
                float width, float mass, bool circle = false)
            {
                Name = name;
                Position = position;
                Angle = angle;
                Length = length;
                Width = width;
                IsCircle = circle;
                InverseMass = 1f / mass;
                InverseInertia = circle ? 8f / (mass * width * width)
                    : 12f / (mass * (length * length + width * width));
            }

            public Vector2 Rotate(Vector2 local)
            {
                float c = Mathf.Cos(Angle), s = Mathf.Sin(Angle);
                return new Vector2(c * local.x - s * local.y, s * local.x + c * local.y);
            }

            public Vector2 WorldPoint(Vector2 local) => Position + Rotate(local);

            public Vector2 LocalPoint(Vector2 world)
            {
                Vector2 delta = world - Position;
                float c = Mathf.Cos(Angle), s = Mathf.Sin(Angle);
                return new Vector2(c * delta.x + s * delta.y, -s * delta.x + c * delta.y);
            }
        }

        public readonly struct Joint
        {
            public readonly int A, B;
            public readonly Vector2 AnchorA, AnchorB;

            internal Joint(int a, int b, Vector2 anchorA, Vector2 anchorB)
            {
                A = a;
                B = b;
                AnchorA = anchorA;
                AnchorB = anchorB;
            }
        }

        private readonly List<Part> parts = new List<Part>();
        private readonly List<Joint> joints = new List<Joint>();
        private int grabbedPart = -1;
        private Vector2 grabLocal;
        private Vector2 lastPointer;
        private Vector2 target;
        private readonly float restScale;

        public IReadOnlyList<Part> Parts => parts;
        public IReadOnlyList<Joint> Joints => joints;
        public bool IsDragging => grabbedPart >= 0;
        public int GrabbedPart => grabbedPart;
        public Vector2 GrabWorld => IsDragging ? parts[grabbedPart].WorldPoint(grabLocal) : target;

        public PoseRagdollModel(Vector2 origin = default, float bodyScale = 1, float headScale = 1, bool seated = false, RagdollArtProfile artProfile = null)
        {
            float s = restScale = Mathf.Max(0.1f, bodyScale);
            if (artProfile != null)
            {
                foreach (RagdollArtProfile.BodyPart part in artProfile.parts)
                    parts.Add(new Part(part.name, origin + part.position * s, part.angleDegrees * Mathf.Deg2Rad,
                        part.length * s, part.width * s, part.mass, part.isCircle));
                foreach (RagdollArtProfile.Connection joint in artProfile.joints)
                    joints.Add(new Joint(joint.parent, joint.child, joint.parentAnchor * s, joint.childAnchor * s));
                return;
            }
            float faceScale = s * Mathf.Max(0.5f, headScale);
            float headAnchor = (0.8f + 0.52f * (Mathf.Max(0.5f, headScale) - 1)) * s;
            Vector2 torsoPosition = origin + new Vector2(0, 0.25f * s);
            parts.Add(new Part("Torso", torsoPosition, 0, 1.6f * s, 1.04f * s, 4f));
            parts.Add(new Part("Head", torsoPosition + new Vector2(0, 0.8f * s + headAnchor),
                0, 1.04f * faceScale, 1.04f * faceScale, 1.2f, true));
            joints.Add(new Joint(0, 1, new Vector2(0, 0.8f * s), new Vector2(0, -headAnchor)));

            AddLimb("Left upper arm", "Left forearm", new Vector2(-0.48f, 0.60f) * s,
                origin + new Vector2(-1.03f, 0.03f) * s, origin + new Vector2(-1.23f, -0.91f) * s, 0.27f * s, 0.23f * s, 0.65f);
            AddLimb("Right upper arm", "Right forearm", new Vector2(0.48f, 0.60f) * s,
                origin + new Vector2(1.03f, 0.03f) * s, origin + new Vector2(1.23f, -0.91f) * s, 0.27f * s, 0.23f * s, 0.65f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 knee = origin + new Vector2(side * 0.43f, -1.57f) * s;
                Vector2 foot = origin + new Vector2(side * 0.52f, -2.68f) * s;
                if (seated)
                {
                    Vector2 hip = torsoPosition + new Vector2(side * 0.30f, -0.68f) * s;
                    float thighLength = new Vector2(0.13f, 1.14f).magnitude * s;
                    float shinLength = new Vector2(0.09f, 1.11f).magnitude * s;
                    knee = hip + new Vector2(side * 0.8f, -0.6f) * thighLength;
                    foot = knee + new Vector2(side * 0.98f, -Mathf.Sqrt(1 - 0.98f * 0.98f)) * shinLength;
                }
                AddLimb(side < 0 ? "Left thigh" : "Right thigh", side < 0 ? "Left shin" : "Right shin",
                    new Vector2(side * 0.30f, -0.68f) * s, knee, foot, 0.34f * s, 0.28f * s, 1f);
            }
        }

        private void AddLimb(string upperName, string lowerName, Vector2 torsoAnchor,
            Vector2 elbow, Vector2 tip, float upperWidth, float lowerWidth, float mass)
        {
            int upper = parts.Count;
            AddSegment(upperName, parts[0].WorldPoint(torsoAnchor), elbow, upperWidth, mass);
            AddSegment(lowerName, elbow, tip, lowerWidth, mass * 0.8f);
            joints.Add(new Joint(0, upper, torsoAnchor, new Vector2(0, parts[upper].Length * 0.5f)));
            joints.Add(new Joint(upper, upper + 1, new Vector2(0, -parts[upper].Length * 0.5f),
                new Vector2(0, parts[upper + 1].Length * 0.5f)));
        }

        private void AddSegment(string name, Vector2 start, Vector2 end, float width, float mass)
        {
            Vector2 up = start - end;
            parts.Add(new Part(name, (start + end) * 0.5f,
                Mathf.Atan2(up.y, up.x) - Mathf.PI * 0.5f, up.magnitude, width, mass));
        }

        // The view supplies the actual visible collider. Keep the exact clicked
        // point, so grabbing away from a pivot never snaps the body to the mouse.
        public void BeginDrag(int partIndex, Vector2 pointer)
        {
            if (partIndex < 0 || partIndex >= parts.Count)
                throw new ArgumentOutOfRangeException(nameof(partIndex));
            AlignHeadWithTorso();
            grabbedPart = partIndex;
            grabLocal = parts[partIndex].LocalPoint(pointer);
            target = lastPointer = pointer;
        }

        public bool MoveDrag(Vector2 pointer)
        {
            if (!IsDragging || pointer == lastPointer)
                return false;

            // Small spatial steps handle fast mouse motion without tearing joints.
            // This is distance-based, never frame-rate- or time-based.
            Vector2 start = lastPointer;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(start, pointer) / (0.09f * restScale)), 1, 96);
            for (int step = 1; step <= steps; step++)
            {
                target = Vector2.Lerp(start, pointer, step / (float)steps);
                PinGrab();
                for (int iteration = 0; iteration < 100; iteration++)
                {
                    for (int j = 0; j < joints.Count; j++)
                        SolveJoint(joints[j]);
                    PinGrab();
                    for (int j = joints.Count - 1; j >= 0; j--)
                        SolveJoint(joints[j]);
                    PinGrab();
                }
            }

            // An exact final pin keeps the clicked point at the mouse, while
            // translating the entire assembly preserves every joint separation.
            Vector2 correction = pointer - GrabWorld;
            foreach (Part part in parts)
                part.Position += correction;
            AlignHeadWithTorso();
            lastPointer = pointer;
            return true;
        }

        public void EndDrag()
        {
            grabbedPart = -1;
        }

        private void PinGrab()
        {
            AlignHeadWithTorso();
            Part part = parts[grabbedPart];
            // The head is carried by the torso, not an independent hinge. A
            // head grab therefore moves their shared frame while preserving
            // the exact clicked point, just like grabbing the torso itself.
            Part moved = grabbedPart == 1 ? parts[0] : part;
            moved.Position += target - part.WorldPoint(grabLocal);
            AlignHeadWithTorso();
        }

        public void AlignHeadWithTorso()
        {
            Joint neck = joints[0];
            Part torso = parts[neck.A], head = parts[neck.B];
            head.Angle = torso.Angle;
            head.Position = torso.Position + torso.Rotate(neck.AnchorA - neck.AnchorB);
        }

        private void SolveJoint(Joint joint)
        {
            // Keep the neck record and anchors, but do not solve it as a free
            // hinge: the head's pose is derived exactly from the torso above.
            if (joint.A == 0 && joint.B == 1)
                return;
            Part a = parts[joint.A], b = parts[joint.B];
            Vector2 ra = a.Rotate(joint.AnchorA), rb = b.Rotate(joint.AnchorB);
            Vector2 error = b.Position + rb - a.Position - ra;
            Vector2 pa = new Vector2(-ra.y, ra.x), pb = new Vector2(-rb.y, rb.x);
            float mass = a.InverseMass + b.InverseMass;
            float xx = mass + a.InverseInertia * pa.x * pa.x + b.InverseInertia * pb.x * pb.x;
            float xy = a.InverseInertia * pa.x * pa.y + b.InverseInertia * pb.x * pb.y;
            float yy = mass + a.InverseInertia * pa.y * pa.y + b.InverseInertia * pb.y * pb.y;
            float determinant = xx * yy - xy * xy;
            Vector2 impulse = new Vector2(yy * error.x - xy * error.y,
                xx * error.y - xy * error.x) / determinant;
            a.Position += impulse * a.InverseMass;
            b.Position -= impulse * b.InverseMass;
            a.Angle += Mathf.Clamp(a.InverseInertia * Vector2.Dot(pa, impulse), -0.18f, 0.18f);
            b.Angle -= Mathf.Clamp(b.InverseInertia * Vector2.Dot(pb, impulse), -0.18f, 0.18f);
        }

        public float MaxJointSeparation()
        {
            float maximum = 0;
            foreach (Joint joint in joints)
                maximum = Mathf.Max(maximum, Vector2.Distance(parts[joint.A].WorldPoint(joint.AnchorA),
                    parts[joint.B].WorldPoint(joint.AnchorB)));
            return maximum;
        }
    }
}
