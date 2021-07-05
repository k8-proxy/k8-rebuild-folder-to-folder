#  K8 rebuild folder to folder

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
        - Directly write the values in .aws/configure or in case of permission errors verify that they are correctly added in `.aws/conifigure` file
            ```
            aws_access_key_id = example-access-key-as-in-previous-output
            aws_secret_access_key = example-secret-access-key-as-in-previous-output
            aws_session_token = example-session-Token-as-in-previous-output
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
      "Action": [
        "elasticfilesystem:CreateFileSystem",
        "elasticfilesystem:CreateMountTarget",
        "elasticfilesystem:CreateTags",
        "elasticfilesystem:DescribeFileSystems",
        "elasticfilesystem:DescribeMountTargetSecurityGroups",
        "elasticfilesystem:DescribeMountTargets",
        "elasticfilesystem:DescribeTags"
      ],
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