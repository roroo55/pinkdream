using UnityEngine;

namespace PinkDream
{
    // Rest measurements only. The same frozen pose solver handles art and prototypes.
    public sealed class RagdollArtProfile : ScriptableObject
    {
        [System.Serializable]
        public sealed class BodyPart
        {
            public string name;
            public Vector2 position;
            public float angleDegrees;
            public float length;
            public float width;
            public float mass;
            public bool isCircle;
        }

        [System.Serializable]
        public sealed class Connection
        {
            public int parent, child;
            public Vector2 parentAnchor, childAnchor;
        }

        public BodyPart[] parts;
        public Connection[] joints;
    }
}
