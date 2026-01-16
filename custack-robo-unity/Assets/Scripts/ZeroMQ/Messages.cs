using System.Collections.Generic;
using System;

namespace ZeroMQ
{
    namespace Messages
    {
        [Serializable]
        public class Header
        {
            public uint seq { get; set; } = 0u;
            public float stamp { get; set; } = 0.0f;
        }

        [Serializable]
        public class Point
        {
            public float x { get; set; } = 0.0f;
            public float y { get; set; } = 0.0f;
            public float z { get; set; } = 0.0f;
        }

        [Serializable]
        public class Quaternion
        {
            public float x { get; set; } = 0.0f;
            public float y { get; set; } = 0.0f;
            public float z { get; set; } = 0.0f;
            public float w { get; set; } = 1.0f;
        }

        [Serializable]
        public class Pose
        {
            public Point position { get; set; } = new();
            public Quaternion orientation { get; set; } = new();
        }

        [Serializable]
        public class PoseStamped
        {
            public Header header { get; set; } = new();
            public Pose pose { get; set; } = new();
        }

        [Serializable]
        public class Vector3
        {
            public float x { get; set; } = 0.0f;
            public float y { get; set; } = 0.0f;
            public float z { get; set; } = 0.0f;
        }

        [Serializable]
        public class Twist
        {
            public Vector3 linear { get; set; } = new();
            public Vector3 angular { get; set; } = new();
        }

        [Serializable]
        public class TwistStamped
        {
            public Header header { get; set; } = new();
            public Twist twist { get; set; } = new();
            public bool right_weapon { get; set; } = false;
            public bool left_weapon { get; set; } = false;
        }

        [Serializable]
        public class ItemListRequest
        {
            public Header header { get; set; } = new();
            public List<string> items { get; set; } = new();
        }

        [Serializable]
        public class ItemListResponse
        {
            public bool success { get; set; } = false;
            public string message { get; set; } = "";
        }
    }
}