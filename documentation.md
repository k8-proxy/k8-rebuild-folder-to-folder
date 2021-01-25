#  K8 rebuild folder to folder

## Process Overview

<p align="center">
<img src="imgs/k8-rebuild-folder-to-folder.png" width="500">
</p>

### Prerequisites

- Install [AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2.html) on your local machine

    - AWS CLI installation [Linux](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2-linux.html):
        ```
        curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
        unzip awscliv2.zip
        sudo ./aws/install
        ```
    
    - AWS CLI installation [WIN](https://docs.aws.amazon.com/cli/latest/userguide/install-cliv2-windows.html):
        ```
        - Download and install msi: https://awscli.amazonaws.com/AWSCLIV2.msi
        - To verify AWS CLI was installed succesufully open CMD and run `aws --version`, result should be like `aws-cli/2.1.1 Python/3.7.4 Windows/10 botocore/2.0.0`
        ```
- To [setup access](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-quickstart.html) to AWS account run: `aws configure`. 
    - Enter the data for values as they pop up.
    ```
        example:
  
            AWS Access Key ID [None]: AKIAIOSFODNN7EXAMPLE
            AWS Secret Access Key [None]: wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
            Default region name [None]: eu-west-1
            Default output format [None]: json
    ```
    
- If your have MFA token enabled for authentication to AWS account you should [set up MFA token authenticate through AWS CLI](https://aws.amazon.com/premiumsupport/knowledge-center/authenticate-mfa-cli/)
    - Run: `aws sts get-session-token --serial-number arn-of-the-mfa-device --token-code code-from-token`
    - Replace arn-of-the-mfa-device with your username and AWS number and enter MFA code as code-from-token 
    - As a response you will get
      ```
        "SecretAccessKey": "secret-access-key",
        "SessionToken": "temporary-session-token",
        "Expiration": "expiration-date-time",
        "AccessKeyId": "access-key-id"
      ```
    - Use session token :
        - Linux: 
            ```
            export AWS_ACCESS_KEY_ID=example-access-key-as-in-previous-output
            export AWS_SECRET_ACCESS_KEY=example-secret-access-key-as-in-previous-output
            export AWS_SESSION_TOKEN=example-session-Token-as-in-previous-output
            ```
        - WIN: 
            ```
            set AWS_ACCESS_KEY_ID=example-access-key-as-in-previous-output
            set AWS_SECRET_ACCESS_KEY=example-secret-access-key-as-in-previous-output
            set AWS_SESSION_TOKEN=example-session-Token-as-in-previous-output
            ```

- [If you are logging in as IAM user](https://docs.aws.amazon.com/vm-import/latest/userguide/vmie_prereqs.html) the following policy should be assinged to user. Replace `<S3Bucket_Name>` with name of S3 bucket in ova file is stored
    ```
    {
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "s3:ListAllMyBuckets"
      ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:CreateBucket",
        "s3:DeleteBucket",
        "s3:DeleteObject",
        "s3:GetBucketLocation",
        "s3:GetObject",
        "s3:ListBucket",
        "s3:PutObject"
      ],
      "Resource": ["arn:aws:s3:::<S3Bucket_Name>","arn:aws:s3:::<S3Bucket_Name>/*"]
    }, 
    {
      "Effect": "Allow",
      "Action": [
        "ec2:CancelConversionTask",
        "ec2:CancelExportTask",
        "ec2:CreateImage",
        "ec2:CreateInstanceExportTask",
        "ec2:CreateTags",
        "ec2:DeleteTags",
        "ec2:DescribeConversionTasks",
        "ec2:DescribeExportTasks",
        "ec2:DescribeExportImageTasks",
        "ec2:DescribeImages",
        "ec2:DescribeInstanceAttribute",
        "ec2:DescribeInstanceStatus",
        "ec2:DescribeInstances",
        "ec2:DescribeSnapshots",
        "ec2:DescribeTags",
        "ec2:ExportImage",
        "ec2:ImportInstance",
        "ec2:ImportVolume",
        "ec2:StartInstances",
        "ec2:StopInstances",
        "ec2:TerminateInstances",
        "ec2:ImportImage",
        "ec2:ImportSnapshot",
        "ec2:DescribeImportImageTasks",
        "ec2:DescribeImportSnapshotTasks",
        "ec2:CancelImportTask"
      ],
      "Resource": "*"
    },
    {
      "Action": "elasticfilesystem:*",
      "Effect": "Allow",
      "Resource": "*"
    }
    ]
    }
    ```
- Create an IAM role named `vmimport`. This should be done only once.
    - Make sure AWS STS is enabled for the region you're working on
    - Create a file called `trust-policy.json` and add the following:
    ```
    {
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": { "Service": "vmie.amazonaws.com" },
            "Action": "sts:AssumeRole",
            "Condition": {
                "StringEquals":{
                "sts:Externalid": "vmimport"
                }
            }
        }
    ]
    }
    ```
    - From folder where `trust-policy.json` is created, run the `create-role` command
    ```
    aws iam create-role --role-name vmimport --assume-role-policy-document "file://trust-policy.json"
    ```
    - Create a file called `role-policy.json`, replacing `disk-image-file-bucket` with the bucket for disk images and `export-bucket` with the bucket for exported images:
    ```
    {
    "Version":"2012-10-17",
    "Statement":[
        {
            "Effect": "Allow",
            "Action": [
                "s3:GetBucketLocation",
                "s3:GetObject",
                "s3:ListBucket" 
            ],
            "Resource": [
                "arn:aws:s3:::<S3Bucket_Name>",
            ]
        },
        {
            "Effect": "Allow",
            "Action": [
                "ec2:ModifySnapshotAttribute",
                "ec2:CopySnapshot",
                "ec2:RegisterImage",
                "ec2:Describe*"
            ],
            "Resource": "*"
        }
    ]
    }
    ```
    - If you are adding multiple buckets for the same trust-policy add extra section with your bucket data in between bruckets {}
    - From folder where `role-policy.json` is created, run the `put-role-policy` command to attach the policy to the role created:
    ```
    aws iam put-role-policy --role-name vmimport --policy-name vmimport --policy-document "file://role-policy.json"
    ```
  
### Creating custom AMI

* In this documentation, we will be creating two instances. `k8-f2f-service` in which all processing happens and `k8-f2f-user` which acts as an instance for users to login and use services
* Path to ova file for `k8-f2f-service` : `s3://glasswall-sow-ova/vms/k8-rebuild-folder-to-folder/k8-rebuild-folder-to-folder-311282e30f06def19ec5f2f4127afbe7a89bb6a9.ova`
* Path to ova file for `k8-f2f-user` : `s3://glasswall-sow-ova/ova/Ubuntu18.04.5.ova`
* To create a custom AMI, OVA file stored in S3 bucket need to be imported. Run below command from *your local machine* to import ova file from s3 bucket and create custom AMI

```
  Service_OVA_Path   :   s3://glasswall-sow-ova/vms/k8-rebuild-folder-to-folder/k8-rebuild-folder-to-folder-f81add2d6180c586bb9a1d9e9cee023d89d50f3e.ova
  User_OVA_Path      :   s3://glasswall-sow-ova/vms/k8-rebuild-folder-to-folder/user-vm/k8-rebuild-folder-to-folder-export-i-081f0825e17222660.ova  
```
``` 
  $ git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git
  $ cd k8-rebuild-folder-to-folder
```

`k8-f2f-service`:

```shell 
  $ chmod +x ./packer/import-ova.sh
  $ ./packer/import-ova.sh <Service_OVA_Path>

  Example: ./packer/import-ova.sh s3://glasswall-sow-ova/some.ova
 ```

* Once import task is completed, above command produces output similar to `Imported AMI ID is: <AMI ID>`. Note the value of AMI ID which can be used in launching instance.
  
 `k8-f2f-user`:
 
```shell 
  $ git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git 
  $ cd k8-rebuild-folder-to-folder
  $ chmod +x packer/import-ova.sh
  $ ./packer/import-ova.sh <User_OVA_Path>

  Example: ./packer/import-ova.sh s3://glasswall-sow-ova/some.ova
```
* Note the value of AMI ID from output
  
### Launching Instance

* Login to aws console `https://aws.amazon.com/console/`

* Go to EC2 service

* Go to "AMI" under "Images"

* Change the region in which AMI is created (Ireland, eu-wst-1)

* Search the `<AMI ID>` of `k8-f2f-service` present in output of above step.

* Select the `<AMI>` and click `Launch` button.

* Select below configarature for next steps (available on bottom right corner):

      - Choose Instance Type         :     t2.micro ( For load testing we generally use c4.8xlarge but ask to requester which flavour he wants to use )  
      - Add Storage (disk space)     :     At least 20G
      - Add Tags                     :     Can be skipped
      - Configure Security Group     :     22, 80, 443, 2049
      
* Click `Review and Launch` and then `Launch`

* Repeat the same steps for `k8-f2f-user` instance.

* After instance is created, Login to created instance by using below command

```shell
  $ ssh glasswall@<instanceip>
```
  - Note: Since instance is created using custom AMI, SSH authentication is allowed only by username and password combination. SSH key supplied in AWS console cannot be used
  
* Once login is successfull, change default password using below command and enter new choosen password

```shell
  $ passwd glasswall
```
* After process of instance creation is completed, proceed to creation and mounting of EFS volume which is used to store input and processed output files

### Creating and mounting EFS Volume

* EFS volume can be created by running below command by replacing creation-token, aws-region and tags with proper values.

```shell
    $ aws efs create-file-system \
    --creation-token creation-token \
    --performance-mode generalPurpose \
    --throughput-mode bursting \
    --region aws-region \
    --tags Key=key,Value=value Key=key1,Value=value1 \
```

* Note down `file-system-id` from output of above command.
* Once volume is created, mount target can be created by running below command.

```shell
    $ aws efs create-mount-target \
    --file-system-id file-system-id \
    --subnet-id  subnet-id \
    --security-group ID-of-the-security-group-created-for-mount-target \
    --region aws-region \
```

* Please note :
  - EFS mount target should be created in same subnet in which two instances are created
  - Security group assigned to EFS volume should allow incoming connections on NFS port from EC2 instance security group
  - Once EFS mount target is created, take a note of `FileSystemId` which is required in next step for mounting
  
### Mounting EFS Volume

* For `k8-f2f-service` instance, run below command which will mount EFS volume at `/data/folder-to-folder`

```shell
  $ chmod +x ./packer/mount-efs.sh 
  $ ./packer/mount-efs.sh <file system domain> /data/folder-to-folder 

  (<file system domain> = <file system id>.efs.<aws region>.amazonaws.com)
```

* For `k8-f2f-user` instance, EFS volume can be mounted at any required path by passing mount path as an argument.

```shell
  $ git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git 
  $ cd k8-rebuild-folder-to-folder
  $ chmod +x packer/mount-efs.sh
  $ ./packer/mount-efs.sh <file system domain> <mount path>
```
  * In mount path, there are four folders: Input, Output, Error and logs which are used for file handling service
  
### Running Service

* To run folder to folder service, login to `k8-f2f-service` using SSH and Zip the files that needs to be processed. Copy the zip file to `<mount path>/input`
```script
  $ sudo apt install zip unzip
  $ zip -r files.zip <folder_name>/*
  $ cp files.zip <mount path>/input
```
* Once zip file is copied, File handling service will automatically pick up the folder and will process it 

* After processing is completed, data is automatically moved to `<mount path>/output`

* Incase of any errors during processing, data will be moved to `<mount path>/error`

* Logs of processing can be found in `<mount path>/logs`

* Similarly, files can be supplied for processing by logging into `k8-f2f-user` instance using SSH and copying zip files to `<mount path>/input`



