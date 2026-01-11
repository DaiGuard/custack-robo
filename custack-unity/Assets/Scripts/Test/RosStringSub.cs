using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Std;

public class RosStringSub : MonoBehaviour
{
    [SerializeField]
    string topicName = "chatter";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<StringMsg>(topicName, StringCallback);
    }

    void StringCallback(StringMsg msg)
    {
        Debug.Log($"Received: {msg.data}");
    }
}
