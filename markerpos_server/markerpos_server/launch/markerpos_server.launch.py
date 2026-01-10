import os
from launch import LaunchDescription
from launch_ros.actions import Node
from launch.actions import DeclareLaunchArgument, SetEnvironmentVariable
from launch.substitutions import LaunchConfiguration


def generate_launch_description():

    domain_id_arg = DeclareLaunchArgument(
        'domain_id',
        default_value='10',
        description='ROS2 Domain ID'
    )

    client_ip_arg = DeclareLaunchArgument(
        'client_ip',
        default_value='127.0.0.1',
        description='Client IP address to bind TCP endpoint'
    )

    domain_id = LaunchConfiguration('domain_id')
    client_ip = LaunchConfiguration('client_ip')

    set_domain_id = SetEnvironmentVariable(
        'ROS_DOMAIN_ID', domain_id
        )
    
    markerpos_pub_node = Node(
        package='markerpos_server',
        executable='markerpos_pub',
        name='markerpos_pub',
        output='screen',
        parameters=[
            {'debug': False},
        ]
    )

    tcp_endpoint_node = Node(
        package='ros_tcp_endpoint',
        executable='default_server_endpoint',
        name='tcp_endpoint',
        output='screen',
        parameters=[
            {'ROS_IP': client_ip},
            {'ROS_TCP_PORT': 10000},
        ]
    )

    return LaunchDescription([
        set_domain_id,
        markerpos_pub_node,
        tcp_endpoint_node,
    ])
