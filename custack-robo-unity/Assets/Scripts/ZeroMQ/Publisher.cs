using TMPro;
using Unity.VisualScripting;
using UnityEngine;

namespace ZeroMQ
{
    public class Publisher : MonoBehaviour
    {
        private Manager _manager = null;

        [SerializeField]
        private string _topic = "";

        protected string _lastMessage = "";

        protected virtual void Start()
        {
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
            if (_manager != null)
            {
                SerializeData();

                _manager.PubMessageForTopic(_topic, _lastMessage);
            }
        }

        protected virtual void SerializeData()
        {
        }
    }
}