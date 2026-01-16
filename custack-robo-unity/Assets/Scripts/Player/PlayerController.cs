using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    string topicName = "marker_0/pose";

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ROSConnection.GetOrCreateInstance().Subscribe<PoseStampedMsg>(topicName, msg => {
            Vector3 position = new Vector3(
                (float)msg.pose.position.x,
                (float)msg.pose.position.y,
                (float)msg.pose.position.z
            );

            Quaternion rotation = new Quaternion(
                (float)msg.pose.orientation.x,
                (float)msg.pose.orientation.y,
                (float)msg.pose.orientation.z,
                (float)msg.pose.orientation.w
            );

            transform.SetLocalPositionAndRotation(position, rotation);
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
