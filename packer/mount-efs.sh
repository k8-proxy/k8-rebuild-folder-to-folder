#!/bin/bash

# mount EFS to /data/folder-to-folder
EFS_DOMAIN=${1:-fs-04997e31.efs.eu-west-1.amazonaws.com}
MOUNT_PATH=${2:-/data/folder-to-folder}
sudo mkdir -p $MOUNT_PATH
sudo apt update
sudo apt install nfs-common -y
sudo mount -t nfs4 -o nfsvers=4.1,rsize=1048576,wsize=1048576,hard,timeo=600,retrans=2,noresvport $EFS_DOMAIN:/ $MOUNT_PATH
sudo chown -R $USER:$USER $MOUNT_PATH
cd $MOUNT_PATH && mkdir -p input && mkdir -p output && mkdir -p error && mkdir -p log
