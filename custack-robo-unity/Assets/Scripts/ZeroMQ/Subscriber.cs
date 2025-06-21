using UnityEngine;

namespace ZeroMQ
{
    public class Subscriber : MonoBehaviour
    {
        private Manager _manager = null;

        [SerializeField]
        private string _topic = "";
        protected string lastMessage { get; private set; } = "";


        protected virtual void Start()
        {
            if (Manager.Instance != null)
            {
                _manager = Manager.Instance;
            }
            else
            {
                Debug.LogError("ZeroMQManager is not initialized.");
            }
        }

        protected virtual void Update()
        {
            if (_manager != null)
            {
                string message = _manager.SubMessageForTopic(_topic);
                if (message != null)
                {
                    lastMessage = message;

                    DeserializeData();
                }
            }
        }

        protected virtual void DeserializeData()
        {
        }
    }
}