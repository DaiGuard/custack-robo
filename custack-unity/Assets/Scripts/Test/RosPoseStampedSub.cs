using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class RosPoseStampedSub : MonoBehaviour
{
    [SerializeField]
    string topicName = "marker_0/pose";

    Vector3 originPosition = Vector3.zero;
    Quaternion originRotation = Quaternion.identity;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<PoseStampedMsg>(topicName, StringCallback);

        originPosition = gameObject.transform.localPosition;
        originRotation = gameObject.transform.localRotation;
    }

    void StringCallback(PoseStampedMsg msg)
    {
        Debug.Log($"Received Position: x={msg.pose.position.x}, y={msg.pose.position.y}, z={msg.pose.position.z}");
        Debug.Log($"Received Orientation: x={msg.pose.orientation.x}, y={msg.pose.orientation.y}, z={msg.pose.orientation.z}, w={msg.pose.orientation.w}");

        gameObject.transform.localPosition = originPosition + new Vector3(
            (float)msg.pose.position.x,
            (float)msg.pose.position.y,
            (float)msg.pose.position.z
        );
        gameObject.transform.localRotation = originRotation * new Quaternion(
            (float)msg.pose.orientation.x,
            (float)msg.pose.orientation.y,
            (float)msg.pose.orientation.z,
            (float)msg.pose.orientation.w
        );
    }
}
