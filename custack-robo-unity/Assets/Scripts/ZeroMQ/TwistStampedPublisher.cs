using UnityEngine;
using System.Text.Json;

namespace ZeroMQ
{
    using Messages;

    public class TwistStampedPublisher : Publisher
    {
        [SerializeField, ReadOnly] public Header Header { get; private set; } = new();
        [SerializeField, ReadOnly] public Twist Twist { get; set; } = new();
        [SerializeField, ReadOnly] public bool RightWeapon { get;  set; } = false;
        [SerializeField, ReadOnly] public bool LeftWeapon { get;  set; } = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected override void Start()
        {
            base.Start();
        }

        // Update is called once per frame
        protected override void Update()
        {
            base.Update();
        }

        protected override void SerializeData()
        {
            var header = Header;

            header.seq++;
            header.stamp = Time.time;

            var message = new TwistStamped();
            message.header = header;
            message.twist = Twist;
            message.right_weapon = RightWeapon;
            message.left_weapon = LeftWeapon;

            _lastMessage = JsonSerializer.Serialize(message);
        }
    }

}