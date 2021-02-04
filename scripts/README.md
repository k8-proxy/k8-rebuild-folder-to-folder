## Setup k8-rebuild-folder-to-folder on AWS

### Authenticate to AWS

- Use AWS access keys or AWS profile to authenticate to aws cli

    ```
    export AWS_ACCESS_KEY_ID=<replace access key>
    export AWS_SECRET_ACCESS_KEY=<replace secret access key>
    export AWS_DEFAULT_REGION=eu-west-1
    ```
    Or
    ```
    export AWS_PROFILE_NAME=<some profile name>
    ```

### Fill config.env file with details:
- One of SERVICE_OVA_PATH and SERVICE_AMI_ID is required.
- SERVICE_OVA_PATH: S3 path of service OVA
- SERVICE_AMI_ID: AMI ID of imported service ova
- One of USER_OVA_PATH and USER_AMI_ID is required. Pass none of them to skip creating user instance.
- USER_OVA_PATH: S3 path of user OVA
- USER_AMI_ID: AMI ID of imported user ova
- VPC_ID: VPC ID, must be passed
- SUBNET_ID: Subnet ID, must be passed
- SECURITY_GROUP_ID: security group ID to be attached to ec2 and efs. If not passed, a new security group will be created.
- SERVICE_INSTANCE_PASSWORD: password for service instance
- USER_INSTANCE_PASSWORD: password for user instance
- If EFS_DOMAIN is not passed, new EFS will be created
- EFS_DOMAIN: EFS domain in format "<FILE_SYSTEM_ID>.efs.<AWS_REGION>.amazonaws.com"

### Run main.sh
    ./main.sh

