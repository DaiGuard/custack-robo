using System;
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

    [SerializeField] private Transform _rightArmPos;
    [SerializeField] private Transform _leftArmPos;
    [SerializeField] private WeaponDatabase _weaponDatabase = null;
    [SerializeField] private GameObject _targetObject = null;  

    private bool _updatedItems = false;
    private List<int> _itemIds = new List<int>();

    [SerializeField] private GameObject _rightWeapon = null;
    [SerializeField] private GameObject _leftWeapon = null;

    private Vector2 _moveVec = Vector2.zero;
    private Vector2 _lookVec = Vector2.zero;
    private bool _rightWeaponAttack = false;
    private bool _leftWeaponAttack = false;


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
        // 武器情報のアップデート
        if (_updatedItems)
        {
            _updatedItems = false;

            lock (_itemIds)
            {
                var rightWeapon = _weaponDatabase.GetWeapon(_itemIds[0]);
                if (rightWeapon != null)
                {
                    if (_rightWeapon != null)
                    {
                        Destroy(_rightWeapon);
                    }

                    _rightWeapon = Instantiate(rightWeapon,
                        _rightArmPos.position, _rightArmPos.rotation,
                        _rightArmPos);

                }
                else
                {
                    Debug.LogWarning($"Weapon with ID {_itemIds[0]} not found in database.");
                }

                var leftWeapon = _weaponDatabase.GetWeapon(_itemIds[1]);
                if (leftWeapon != null)
                {
                    if (_leftWeapon != null)
                    {
                        Destroy(_leftWeapon);
                    }

                    _leftWeapon = Instantiate(leftWeapon,
                        _leftArmPos.position, _leftArmPos.rotation,
                        _leftArmPos);
                }
                else
                {
                    Debug.LogWarning($"Weapon with ID {_itemIds[1]} not found in database.");
                }
            }
        }

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
            _twistStampedPublisher.RightWeapon = _rightWeaponAttack;
            _twistStampedPublisher.LeftWeapon = _leftWeaponAttack;
        }
    }

    public bool SpawnItems(List<string> items)
    {
        var itemIds = new List<int>();
        foreach (var itemStr in items)
        {
            if(int.TryParse(itemStr, out int id))
            {
                itemIds.Add(id);
            }
            else
            {
                Debug.LogWarning($"Invalid item ID: {itemStr}");
            }
        }

        if (itemIds.Count >= 2)
        {
            lock (_itemIds)
            {
                _itemIds = itemIds;
            }
            
            _updatedItems = true;
        }

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
            _leftWeaponAttack = true;
            if (_leftWeapon != null)
            {
                var weaponSystem = _leftWeapon.GetComponent<WeaponSystem>();
                if (weaponSystem != null)
                {
                    weaponSystem.Play(_leftArmPos, _targetObject);
                }
            }
        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {
            _leftWeaponAttack = false;
        }
    }

    public void OnInputRightAttack(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            _rightWeaponAttack = true;
            if (_rightWeapon != null)
            {
                var weaponSystem = _rightWeapon.GetComponent<WeaponSystem>();
                if (weaponSystem != null)
                {
                    weaponSystem.Play(_rightArmPos, _targetObject);
                }
            }
        }
        else if (context.performed)
        {

        }
        else if (context.canceled)
        {
            _rightWeaponAttack = false;
        }
    }
}
