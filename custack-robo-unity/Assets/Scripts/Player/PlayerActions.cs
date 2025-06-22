using System.Collections.Generic;
using UnityEngine;
using ZeroMQ;

[RequireComponent(typeof(PoseStampedSubscriber))]
[RequireComponent(typeof(TwistStampedPublisher))]
[RequireComponent(typeof(ItemListService))]
public class PlayerActions : MonoBehaviour
{
    private PoseStampedSubscriber _posestampedSubscriber = null;
    private TwistStampedPublisher _twistStampedPublisher = null;
    private ItemListService _itemListService = null;

    private uint _lastSeq = 0u;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _posestampedSubscriber = GetComponent<PoseStampedSubscriber>();
        if (_posestampedSubscriber == null)
        {
            Debug.LogError("PoseSubscriber not found.");
            return;
        }

        _twistStampedPublisher = GetComponent<TwistStampedPublisher>();
        if (_twistStampedPublisher == null)
        {
            Debug.LogError("TwistPublisher not found.");
            return;
        }

        _itemListService = GetComponent<ItemListService>();
        if (_itemListService == null)
        {
            Debug.LogError("ItemListService not found.");
            return;
        }
        _itemListService.ProcessData = SpawnItems;
    }

    // Update is called once per frame
    void Update()
    {
        if (_posestampedSubscriber != null)
        {
            var header = _posestampedSubscriber.Header;
            var pose = _posestampedSubscriber.Pose;

            var position = Vector3.zero;
            var rotation = Quaternion.identity;

            if (_lastSeq != header.seq)
            {
                position = new Vector3(
                    pose.position.x,
                    pose.position.y,
                    pose.position.z
                );

                rotation = new Quaternion(
                    pose.orientation.x,
                    pose.orientation.y,
                    pose.orientation.z,
                    pose.orientation.w
                );

                _lastSeq = header.seq;
            }

            transform.Translate(position * Time.deltaTime);
            transform.Rotate(rotation.eulerAngles * Time.deltaTime);
        }

        if (_twistStampedPublisher != null)
        {
            var twist = _twistStampedPublisher.Twist;
            twist.linear.x += 0.01f;
            twist.linear.y += 0.02f;
            twist.linear.z += 0.03f;

            _twistStampedPublisher.Twist = twist;
        }
    }

    public bool SpawnItems(List<string> items)
    {
        Debug.Log($"{items}");

        return true;
    }
}
