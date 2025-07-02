using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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

    [SerializeField]
    private Transform _rightArmPos;
    [SerializeField]
    private Transform _leftArmPos;

    private Vector2 _moveVec = Vector2.zero;
    private Vector2 _lookVec = Vector2.zero;


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

            if (_lastSeq != header.seq)
            {
                var position = new Vector3(
                    pose.position.x,
                    pose.position.y,
                    pose.position.z
                );

                var rotation = new Quaternion(
                    pose.orientation.x,
                    pose.orientation.y,
                    pose.orientation.z,
                    pose.orientation.w
                );

                _lastSeq = header.seq;

                transform.localPosition = position;
                transform.localRotation = rotation;
            }
        }

        if (_twistStampedPublisher != null)
        {
            var twist = _twistStampedPublisher.Twist;
            twist.linear.x = _moveVec.x;
            twist.linear.y = _moveVec.y;
            twist.linear.z = 0.0f;

            twist.angular.x = 0.0f;
            twist.angular.y = 0.0f;
            twist.angular.z = _lookVec.x;

            _twistStampedPublisher.Twist = twist;
        }
    }

    public bool SpawnItems(List<string> items)
    {
        Debug.Log($"{items}");

        return true;
    }

    public void OnInputMove(InputAction.CallbackContext context)
    {
        var vec = context.ReadValue<Vector2>();
        _moveVec = vec;
    }

    public void OnInputLook(InputAction.CallbackContext context)
    {
        var vec = context.ReadValue<Vector2>();
        _lookVec = vec;
    }


    public void OnInputLeftAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {

        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {

        }
    }

    public void OnInputRightAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {

        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {

        }
    }
}
