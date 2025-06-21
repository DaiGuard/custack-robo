using UnityEngine;
using System.Text.Json;

namespace ZeroMQ
{
    using Messages;

    public class PoseStampedSubscriber : Subscriber
    {
        [SerializeField, ReadOnly]
        public Header Header { get; private set; } = new();
        [SerializeField, ReadOnly]
        public Pose Pose { get; private set; } = new();


        protected override void Start()
        {
            base.Start();
        }

        protected override void Update()
        {
            base.Update();
        }

        protected override void DeserializeData()
        {
            var message = lastMessage;
            if (message != null)
            {
                try
                {
                    var json = JsonSerializer.Deserialize<PoseStamped>(message);
                    Header = json.header;
                    Pose = json.pose;
                }
                catch (JsonException e)
                {
                    Debug.LogError($"Json Exception: {e.Message}");
                }
            }
        }
    }
}