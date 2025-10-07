using System.Collections.Generic;

namespace ZeroMQ
{
    namespace Messages
    {
        public class Header
        {
            public uint seq { get; set; } = 0u;
            public float stamp { get; set; } = 0.0f;
        }

        public class Point
        {
            public float x { get; set; } = 0.0f;
            public float y { get; set; } = 0.0f;
            public float z { get; set; } = 0.0f;
        }

        public class Quaternion
        {
            public float x { get; set; } = 0.0f;
            public float y { get; set; } = 0.0f;
            public float z { get; set; } = 0.0f;
            public float w { get; set; } = 1.0f;
        }

        public class Pose
        {
            public Point position { get; set; } = new();
            public Quaternion orientation { get; set; } = new();
        }

        public class PoseStamped
        {
            public Header header { get; set; } = new();
            public Pose pose { get; set; } = new();
        }

        public class Vector3
        {
            public float x { get; set; } = 0.0f;
            public float y { get; set; } = 0.0f;
            public float z { get; set; } = 0.0f;
        }

        public class Twist
        {
            public Vector3 linear { get; set; } = new();
            public Vector3 angular { get; set; } = new();
        }

        public class TwistStamped
        {
            public Header header { get; set; } = new();
            public Twist twist { get; set; } = new();
            public bool right_weapon { get; set; } = false;
            public bool left_weapon { get; set; } = false;
        }

        public class ItemListRequest
        {
            public Header header { get; set; } = new();
            public List<string> items { get; set; } = new();
        }

        public class ItemListResponse
        {
            public bool success { get; set; } = false;
            public string message { get; set; } = "";
        }
    }
}