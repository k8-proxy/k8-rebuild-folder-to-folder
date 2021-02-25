# k8-rebuild-folder-to-folder

## OVAs to be used 

- **user OVA**: https://glasswall-sow-ova.s3-eu-west-1.amazonaws.com/vms/k8-rebuild-folder-to-folder/user-vm/Ubuntu18.04.5.ova
- **service OVA**: https://glasswall-sow-ova.s3-eu-west-1.amazonaws.com/vms/k8-rebuild-folder-to-folder/k8-rebuild-fol[…]r-651161dccadd8d3c69bc2f2e0a69fa808e7fc458.ova
- **Important note**: Above OVAs need to be uploaded to your AWS S3 bucket. Once uploaded, use corresponding S3 URL when editing `config.env` during the setup phase.


## Process Overview

<p align="left">
<img src="https://user-images.githubusercontent.com/70108899/106617806-3a5ed200-656f-11eb-851a-530136d3a68c.PNG" width="500">
</p>

## Setup Overview

<p align="left">
<img src="https://user-images.githubusercontent.com/70108899/108490947-9b8ee100-72a3-11eb-8af8-3582db3210ba.png" width="500">
</p>

## Make sure that all [AWS prerequisites](AWS_prerequisites.md) are in place before proceeding.

- You are [authenticated to AWS CLI](AWS_prerequisites.md)
- Security group is created and securityGroupID is noted. In case security group details are not provided, new SG will be created by `main.sh` script
- VPC and subnet details are noted
- [EFS is created](step_by_step.md) and ID is noted. In case EFS details are not provided, new EFS will be created by `main.sh` script


## Setting up Service and User OVA via shell script

### Setup video 

[![Installation Video (OVA)](https://img.youtube.com/vi/yaBPLn9ISSg/hqdefault.jpg)](https://www.youtube.com/watch?v=EA10yatzaVw&ab_channel=GlasswallEngineering)


### Setup steps

**Note: Following instructions are made for Ubuntu based systems**

```
git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git
cd k8-rebuild-folder-to-folder/scripts
#install jq
sudo apt install jq
```

### In scripts subfolder edit `config.env` file:
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
- USER_INSTANCE_PASSWORD: password for user instance, if user instance is not being created, this can be skipped
- If EFS_DOMAIN is not passed, new EFS will be created
- EFS_DOMAIN: EFS domain in format "<FILE_SYSTEM_ID>.efs.<AWS_REGION>.amazonaws.com"

### Run main.sh
```
bash main.sh
```
- You can get service/user instance IPs 
    - seen next to "Warning: Permanently added..." 
    - or by logging to AWS Console and based on instance ID that is created, search for IP in EC2 Dashboard 
- Use above IPs to SSH to service/user instance and use them as described in following sections

### Running Service

- Once all componenets are created, final setup should have following components:

  - `k8-f2f-service`- EC2 instnace in which k8-folder-folder service containers are located and which monitors and process files
  - `k8-f2f-user` - EC2 (demo) instance which is used to demonstrate file processing from another instance which can be used by normal users without needing access to `k8-f2f-service` instance
  - `k8-f2f-efs` - EFS file system which can be mounted to any number of instances for supplying file to processing service
  - Bellow diagram shows how these 3 elements are interconnected 


<p align="left">
<img src="https://user-images.githubusercontent.com/70108899/106618748-19e34780-6570-11eb-8b06-43336c593604.PNG" width="500">
</p>

### How to use F2F video

[![Installation Video (OVA)](https://img.youtube.com/vi/yaBPLn9ISSg/hqdefault.jpg)](https://www.youtube.com/watch?v=xSsD2zi0_Ho&ab_channel=GlasswallEngineering)


### Steps for using `k8-f2f-service`:

* To run folder to folder service, SSH to `k8-f2f-service`
* Copy zip files from your local machine to `/data/folder-to-folder/`
* Transfer the copied files from above folder to `/data/folder-to-folder/input`
* **Note: Above two steps can be done directly by transferring file straight to `/data/folder-to-folder/input` but in case of any network interruption or file corruption during the transfer, file will immediately end up in error folder. This step-by-step process avoids that scenario.**
* You can find zip test files examples at [test_files folder](test_files)
```script
  $ scp /local/directory/files.zip glasswall@<k8-f2f-service IP>:/data/folder-to-folder/input

```
* Once zip file is copied, File handling service will automatically pick up the zip file and will process it 

* After processing is completed, data is automatically moved to `/data/folder-to-folder/output`

* Incase of any errors during processing, data will be moved to `/data/folder-to-folder/error`

* Logs of processing can be found in `/data/folder-to-folder/logs`

* You can navigate to all of these folders and check for the output files. 

* To move any of the output files back to your local run `scp glasswall@<k8-f2f-service IP>:/data/folder-to-folder/<ANY FOLDER>/files.zip /local/directory`

* In case of corrupted process try restarting docker-compose
```
cd ~/k8-rebuild-folder-to-folder
sudo docker-compose restart
```

### Steps for using `k8-f2f-user`:

* To run folder to folder service, SSH to `k8-f2f-user`
* Zip the files that needs to be processed. Copy the zip file to `<mount path>`
* Transfer the copied files from above folder to `<mount path>/input`
* **Note: Above two steps can be done directly by transferring file straight to `/data/folder-to-folder/input` but in case of any network interruption or file corruption during the transfer, file will immediately end up in error folder. This step-by-step process avoids that scenario.**
* You can find zip test files examples at [test_files folder](test_files)
```script
  $ scp /local/directory/files.zip glasswall@<k8-f2f-user IP>:<mount path>/input
```
* Once zip file is copied, File handling service will automatically pick up the zip file and will process it 

* After processing is completed, data is automatically moved to `<mount path>/output`

* Incase of any errors during processing, data will be moved to `<mount path>/error`

* Logs of processing can be found in `<mount path>/logs`

* You can navigate to all of these folders and check for the output files. 

* To move any of the output files back to your local run `scp glasswall@<k8-f2f-user IP>:<mount path>/<ANY FOLDER>/files.zip /local/directory`


* Having a commong EFS file system accessible to all user instances, makes it convinent to copy input files from any instance and access processed output files from any other user instnace, not necessarily from instance from which files are copied
* Similarly, EFS file system can be mounted to any number of `k8-f2f-user` instances and files can be copied to `input` folder and processed files can be accessed from `output` folder from any instance
