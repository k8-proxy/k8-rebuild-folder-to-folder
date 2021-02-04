#!/bin/bash

cd $( dirname $0 )
source ./config.env
if [[ -z $SERVICE_OVA_PATH ]]; then
    echo "Please give s3 path of service OVA in config.env file"
    exit -1
fi

import_ova(){
    OVA_PATH=$1 # example argument: s3://glasswall-sow-ova/vms/k8-rebuild-folder-to-folder/some.ova
    if [[ -z "$OVA_PATH" ]]; then
        echo "Please pass s3 path of OVA as argument. Example: s3://glasswall-sow-ova/some.ova"
        exit -1
    fi
    BUCKET_NAME=$( echo $OVA_PATH | sed 's|s3://||' | cut -d"/" -f1 )
    FILE_PATH=$( echo $OVA_PATH | sed 's|s3://||' | cut -d"/" -f 2- )
    cat > containers.json <<EOF
[
    {
        "Description": "k8-rebuild-folder-to-folder",
        "Format": "ova",
        "UserBucket": {
            "S3Bucket": "$BUCKET_NAME",
            "S3Key": "$FILE_PATH"
    }
    }
]
EOF
    IMPORT_TASK=$(aws ec2 import-image --description "k8-rebuild-folder-to-folder" --disk-containers "file://containers.json")
    IMPORT_ID=$(echo $IMPORT_TASK | jq -r .ImportTaskId)
    echo "Started importing with task id: $IMPORT_ID"
    until [ "$RESPONSE" = "completed" ]
    do
    RESPONSE=$(aws ec2 describe-import-image-tasks --import-task-ids $IMPORT_ID | jq -r '.ImportImageTasks[0].Status')
    StatusMessage=$(aws ec2 describe-import-image-tasks --import-task-ids $IMPORT_ID | jq -r '.ImportImageTasks[0].StatusMessage')
    if [[ "deleted" == "$RESPONSE" ]]; then
        echo "Failed to import OVA"
        echo "OVA Import status is $RESPONSE"
        echo "Status message is $StatusMessage"
        exit -1
    elif [[ "active" == "$RESPONSE" ]]; then
        echo "OVA Import status is $RESPONSE"
        echo "Status message is $StatusMessage"
        sleep 30
    fi
    done
    echo "OVA Import status is $RESPONSE"
    AMI_ID=$(aws ec2 describe-import-image-tasks --import-task-ids $IMPORT_ID | jq -r '.ImportImageTasks[0].ImageId')
    echo "Imported AMI ID is: ${AMI_ID}"

}

create_efs(){
    CREATION_TOKEN=$1
    SECURITY_GROUP=$2
    SUBNET_ID=$3
    REGION=$4
    file_system_result=$(aws efs create-file-system \
        --creation-token $CREATION_TOKEN \
        --performance-mode generalPurpose \
        --throughput-mode bursting \
        --region $REGION)
    FILE_SYSTEM_ID=$(echo $file_system_result | jq -r '.FileSystemId') 
    sleep 10s
    aws efs create-mount-target \
        --file-system-id $FILE_SYSTEM_ID \
        --subnet-id  $SUBNET_ID \
        --security-group $SECURITY_GROUP \
        --region $REGION

    EFS_DOMAIN="${FILE_SYSTEM_ID}.efs.${REGION}.amazonaws.com"

}

mount_efs() {
    EFS_DOMAIN=${1}
    MOUNT_PATH=${2:-/data/folder-to-folder}
    INSTANCE_TYPE=${3:-service}
    sudo mkdir -p $MOUNT_PATH
    sudo apt update
    sudo apt install nfs-common -y
    sudo umount /data/folder-to-folder || sudo rm -rf /data/folder-to-folder/*
    sudo mount -t nfs4 -o nfsvers=4.1,rsize=1048576,wsize=1048576,hard,timeo=600,retrans=2,noresvport $EFS_DOMAIN:/ $MOUNT_PATH
    sudo chown -R $USER:$USER $MOUNT_PATH
    cd $MOUNT_PATH && mkdir -p input && mkdir -p output && mkdir -p error && mkdir -p log
    if [[ "$INSTANCE_TYPE" = "service" ]];then
        cd ~/k8-rebuild-folder-to-folder && sudo docker-compose restart
    fi
}

# Import OVA or use existing AMI for service instance
if [[ ! -z $SERVICE_AMI_ID ]]; then 
    echo "Using $SERVICE_AMI_ID for service instance"
else
    import_ova $SERVICE_OVA_PATH
    SERVICE_AMI_ID=$AMI_ID
    echo "Using $SERVICE_AMI_ID for service instance"
    sed -i "s/SERVICE_AMI_ID=.*/SERVICE_AMI_ID=$SERVICE_AMI_ID/g" config.env
fi

# Import OVA or use existing AMI for user instance
if [[ ! -z $USER_AMI_ID ]]; then 
    echo "Using $USER_AMI_ID for user instance"
elif [[ ! -z $USER_OVA_PATH ]]; then
    import_ova $USER_OVA_PATH
    USER_AMI_ID=$AMI_ID
    echo "Using $USER_AMI_ID for user instance"
    sed -i "s/USER_AMI_ID=.*/USER_AMI_ID=$USER_AMI_ID/g" config.env
else
    SKIP_USER_INSTANCE=1
fi

if [[ -z $SECURITY_GROUP_ID ]]; then
    echo "No security group id is passed, creating one."
    RANDOM_STR=$(cat /dev/urandom | tr -dc 'a-zA-Z0-9' | fold -w 6 | head -n 1 )
    sg_result=$(aws ec2 create-security-group --description "k8-rebuild-folder-to-folder" --group-name "k8-rebuild-f2f-${RANDOM_STR}" --vpc-id $VPC_ID)
    SECURITY_GROUP_ID=$( echo $sg_result | jq -r ".GroupId" )
    aws ec2 authorize-security-group-ingress --group-id $SECURITY_GROUP_ID --protocol tcp --port 22 --cidr 0.0.0.0/0
    aws ec2 authorize-security-group-ingress --group-id $SECURITY_GROUP_ID --protocol tcp --port 80 --cidr 0.0.0.0/0
    aws ec2 authorize-security-group-ingress --group-id $SECURITY_GROUP_ID --protocol tcp --port 2049 --cidr 0.0.0.0/0
    sed -i "s/SECURITY_GROUP_ID=.*/SECURITY_GROUP_ID=$SECURITY_GROUP_ID/g" config.env
    echo "Created security group $SECURITY_GROUP_ID"
else
    echo "Using $SECURITY_GROUP_ID security group"
fi

# create EFS if not exists
if [[ ! -z $EFS_DOMAIN ]]; then 
    echo "Using $EFS_DOMAIN file system"
else
    CREATION_TOKEN=$(cat /dev/urandom | tr -dc 'a-zA-Z0-9' | fold -w 6 | head -n 1 )
    create_efs $CREATION_TOKEN $SECURITY_GROUP_ID $SUBNET_ID $REGION
    echo "Using $EFS_DOMAIN for file system"
    sed -i "s/EFS_DOMAIN=.*/EFS_DOMAIN=$EFS_DOMAIN/g" config.env
fi

# create service instance
result=$(aws ec2 run-instances --image-id $SERVICE_AMI_ID --count 1 --instance-type t2.large  --subnet-id $SUBNET_ID --security-group-ids $SECURITY_GROUP_ID --tag-specifications "ResourceType=instance, Tags=[{Key=Name,Value=service-k8-rebuild-folder-to-folder}]" --block-device-mappings "DeviceName=/dev/sda1,Ebs={DeleteOnTermination=true,VolumeSize=20,VolumeType=gp2}")
sleep 4m
instance_id=$(echo $result | jq -r ".Instances[0].InstanceId")
echo "$instance_id is created."
instance_description=$(aws ec2 describe-instances --instance-ids $instance_id)
instance_state=$(echo $instance_description | jq -r ".Reservations[0].Instances[0].State.Name")
echo "Instance state is $instance_state"
if [[ "$instance_state" != "running" ]];then
    echo "EC2 instance $instance_id created from AMI has failed to start in time, terminating the instance."
    echo "Please try again"
    aws ec2 terminate-instances --instance-ids $instance_id
    exit -1
fi
instance_ip=$(echo $instance_description | jq -r ".Reservations[0].Instances[0].PublicIpAddress")

# mount efs on service instance
sudo apt install sshpass -y
sshpass -p "${SERVICE_INSTANCE_PASSWORD}" ssh -o StrictHostKeyChecking=no glasswall@${instance_ip} "$( typeset -f mount_efs); mount_efs $EFS_DOMAIN /data/folder-to-folder service"

# create user instance
if [[ $SKIP_USER_INSTANCE -eq 1 ]]; then
    exit 0
else
    result=$(aws ec2 run-instances --image-id $USER_AMI_ID --count 1 --instance-type t2.micro --subnet-id $SUBNET_ID --security-group-ids $SECURITY_GROUP_ID --tag-specifications "ResourceType=instance, Tags=[{Key=Name,Value=user-k8-rebuild-folder-to-folder}]" --block-device-mappings "DeviceName=/dev/sda1,Ebs={DeleteOnTermination=true,VolumeSize=8,VolumeType=gp2}")
    sleep 4m
    instance_id=$(echo $result | jq -r ".Instances[0].InstanceId")
    echo "$instance_id is created."
    instance_description=$(aws ec2 describe-instances --instance-ids $instance_id)
    instance_state=$(echo $instance_description | jq -r ".Reservations[0].Instances[0].State.Name")
    echo "Instance state is $instance_state"
    if [[ "$instance_state" != "running" ]];then
        echo "EC2 instance $instance_id created from AMI has failed to start in time, terminating the instance."
        echo "Please try again" 
        aws ec2 terminate-instances --instance-ids $instance_id
        exit -1
    fi
    instance_ip=$(echo $instance_description | jq -r ".Reservations[0].Instances[0].PublicIpAddress")

    # mount efs on user instance
    sshpass -p "${USER_INSTANCE_PASSWORD}" ssh -o StrictHostKeyChecking=no glasswall@${instance_ip} "$( typeset -f mount_efs); mount_efs $EFS_DOMAIN /data/folder-to-folder user"

fi
