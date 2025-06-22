using UnityEngine;

namespace ZeroMQ
{
    public class MessageService : MonoBehaviour
    {
        private Manager _manager = null;

        [SerializeField]
        private string _topic = "";


        // Start is called once before the first execution of Update after the MonoBehaviour is created
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

            _manager.RegisterResponseForTopic(_topic, ProcessResponse);
        }

        // Update is called once per frame
        protected virtual void Update()
        {

        }

        protected virtual string ProcessResponse(string message)
        {
            return "";
        }
    }
}