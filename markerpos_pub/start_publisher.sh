#!/bin/sh

USERNAME=$1
REMOTE_HOST=$2

WORKSPACE="/home/${USERNAME}/Workspaces/custack_ws"
ROS_DISTRO="humble"

if [ -z "$USERNAME" ] || [ -z "$REMOTE_HOST" ]; then
    echo "Usage: $0 <username> <remote_host>"
    exit 1
fi

echo "Starting publisher on remote host..."
ssh ${USERNAME}@${REMOTE_HOST} \
    "cd ${WORKSPACE} && \
    export ROS_DOMAIN_ID=10 && \
    source install/setup.bash && \
    ros2 run markerpos_pub markerpos_pub"
# ssh ${USERNAME}@${REMOTE_HOST} \
#     "cd ${WORKSPACE} && \
#     export ROS_DOMAIN_ID=10 && \
#     export ROS_IMPLEMENTATION=rmw_cyclonedds_cpp && \
#     source install/setup.bash && \
#     ros2 run markerpos_pub markerpos_pub"