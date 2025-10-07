using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace ZeroMQ
{
    public class Publisher : MonoBehaviour
    {
        private Manager _manager = null;

        [SerializeField] private string _topic = "";
        [SerializeField, ReadOnly] private string _message = "";
        [SerializeField] float intervalTime = 0.1f;
        [SerializeField, ReadOnly] private float lastTime = 0.0f;

        protected string _lastMessage = "";

        protected virtual void Start()
        {
            lastTime = Time.time;
            if (Manager.Instance != null)
            {
                _manager = Manager.Instance;
            }
            else
            {
                Debug.LogError("ZeroMQManager is not initialized.");
                return;
            }
        }

        protected virtual void Update()
        {
            if(Time.time - lastTime > intervalTime)
            {
                lastTime = Time.time;
                if (_manager != null)
                {
                    SerializeData();

                    _manager.PubMessageForTopic(_topic, _lastMessage);
                }
            }
        }

        protected virtual void SerializeData()
        {
        }
    }
}