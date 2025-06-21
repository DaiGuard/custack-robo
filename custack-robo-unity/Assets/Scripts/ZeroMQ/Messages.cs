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
    }
}