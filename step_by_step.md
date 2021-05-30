# k8-rebuild-folder-to-folder

## Make sure that all [AWS prerequisites](AWS_prerequisites.md) are in place before proceeding.

## Process Overview

<p align="center">
<img src="https://user-images.githubusercontent.com/70108899/106617806-3a5ed200-656f-11eb-851a-530136d3a68c.PNG" width="500">
</p>

### Creating custom AMI

* In this documentation, we will be creating two instances:
  - `k8-f2f-service` - where all processing is happening (service OVA contains File Handling Service and Rebuild API)
  - `k8-f2f-user` - which acts as an instance for users to login and use service
* To create a custom AMI, OVA file stored in S3 bucket need to be imported. 

```
  Service_OVA_Path   :   https://glasswall-sow-ova.s3-eu-west-1.amazonaws.com/vms/k8-rebuild-folder-to-folder/k8-rebuild-folder-to-folder-1f57e688e7eca0801047a1d45a8dc1b383d08585.ova
  User_OVA_Path      :   https://glasswall-sow-ova.s3-eu-west-1.amazonaws.com/vms/k8-rebuild-folder-to-folder/user-vm/Ubuntu18.04.5.ova
```

* Run below commands from *your local machine* to import ova file from s3 bucket and create custom AMI

``` 
  $ git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git
  $ cd k8-rebuild-folder-to-folder
```

`k8-f2f-service`:

```shell 
  $ chmod +x ./packer/import-ova.sh
  $ ./packer/import-ova.sh <Service_OVA_Path>

  Example: $ ./packer/import-ova.sh s3://glasswall-sow-ova/vms/k8-rebuild-folder-to-folder/k8-rebuild-folder-to-folder-f782a8ab15b1067ab31b43a7c451a8c759b76f58.ova
 ```
* If you run into issue `./packer/import-ova.sh: line 28: jq: command not found` install jq (ex. on linux run `sudo apt-get install jq`)
* Once import task is completed, above command produces output similar to `Imported AMI ID is: <AMI ID>`. Note the value of AMI ID which is used in launching instance.
  
 `k8-f2f-user`:
 
```shell 
  $ ./packer/import-ova.sh <User_OVA_Path>

  Example: $ ./packer/import-ova.sh s3://glasswall-sow-ova/vms/k8-rebuild-folder-to-folder/user-vm/Ubuntu18.04.5.ova
```
* Note the value of AMI ID from output
  
### Launching Instance

* Login to aws console `https://aws.amazon.com/console/`

* Go to EC2 service

* Go to "AMI" under "Images"

* Change the region in which AMI is created (Ireland, eu-west-1)

* Search the `<AMI ID>` of `k8-f2f-service` present in output of above step.

* Select the `<AMI>` and click `Launch` button.

* Select below configuration for next steps (available on bottom right corner):

      - Choose Instance Type          :     t2.medium 
      - COnfiguration Instance details:     Select specific VPC and Subnet (you can use default but make sure its the same for both k8-f2f-user and k8-f2f-user instances)
      - Add Storage (disk space)      :     At least 20G
      - Add Tags                      :     Can be skipped
      - Configure Security Group      :     22, 80, 443, 2049 (on Glasswall AWS preexisting security group launch-wizard-8 can be used)
      - Public ip                     :     Assign a public ip/ NAT gateway in case of private subnet 
      
* Example of Security Group (as IP use the ones specific to you)

![image](https://user-images.githubusercontent.com/70108899/105986618-768cc100-609d-11eb-86af-b16ea851b2a9.png)
      
* Click `Review and Launch` and then `Launch`

* Repeat the same steps for `k8-f2f-user` instance, making sure that instance has same VPC, Subnet and security group assigned as `k8-f2f-service`

* After process of instance creation is completed, you can login to created instance by using below command

```shell
  $ ssh glasswall@<instanceip>
```
  - Note: Since instance is created using custom AMI, SSH authentication is allowed only by username and password combination. SSH key supplied in AWS console cannot be used.
  - Default password is shared
  - Once you login you can change default password.
```shell
  $ passwd glasswall
```

### Proceed to creation and mounting of EFS volume which is used to store input and processed output files.

### Creating and mounting EFS Volume

`Using AWS Console:`
* To create EFS volume, login to AWS Console and navigate to [https://eu-west-1.console.aws.amazon.com/efs/]
* Click on `Create file system` and click on `Customize` 
* In creation wizard Step 1, select follow options and click Next
```shell
    - Name: Enter preferred name of EFS file system  Eg: k8-folder-folder
    - Performance Mode: General Purpose
    - Throughput Mode: Bursting
    - Tags - Key:Service, Value: k8-folder-folder
```
* In step 2, Select following options for Network access and Click Next
```shell
  - VPC               : Select VPC in which `k8-f2f-service` and `k8-f2f-user` instances are created
  - Mount targets     : Select Subnet in which `k8-f2f-service` and `k8-f2f-user` instances are created 
  - Security Groups   : Select a security group in which Inbound connections to `NFS service - Port 2049` is allowed from Security group assigned to `k8-f2f-service` and `k8-f2f-user` instances
```
* Click Next on optional File system policy and review the changes and click on Create
* Once EFS File system is created, make a note of `File System ID`


`Using AWS Cli:`
* EFS volume can be created by running below command by replacing creation-token (any string of your choice) and aws-region (same as instance created above)

```shell
    $ aws efs create-file-system \
    --creation-token creation-token \
    --performance-mode generalPurpose \
    --throughput-mode bursting \
    --region aws-region 
```

* Note down `file-system-id` from output of above command.
* Once volume is created, mount target can be created by running below command.

```shell
    $ aws efs create-mount-target \
    --file-system-id file-system-id \
    --subnet-id  subnet-id \
    --security-group ID-of-the-security-group-created-for-mount-target \
    --region aws-region
```

* Note :
  - EFS mount target should be created in same subnet in which two instances are created
  - Security group assigned to EFS volume should allow incoming connections on NFS port from EC2 instance security group
  - Once EFS mount target is created, take a note of `FileSystemId` which is required in next step for mounting
  
### Mounting EFS Volume

<p align="center">
<img src="https://user-images.githubusercontent.com/70108899/106618748-19e34780-6570-11eb-8b06-43336c593604.PNG" width="500">
</p>

* Note: Log folder contains logs from files that are not processed correctly. Other log details can be found in `GlasswallFileProcessingStatus.txt` under each processed zip file (in both error or output folder)

* SSH to `k8-f2f-service` instance
* Run below command which will mount EFS volume at `/data/folder-to-folder`

```shell
  $ chmod +x ./packer/mount-efs.sh 
  $ ./packer/mount-efs.sh <file system domain> /data/folder-to-folder 

  (<file system domain> = <file system id>.efs.<aws region>.amazonaws.com)
```

* SSH to `k8-f2f-user` instance
* Run below command. Note that EFS volume can be mounted at any required path by passing mount path as an argument.

```shell
  $ git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git
  $ cd k8-rebuild-folder-to-folder
  $ chmod +x packer/mount-efs.sh
  $ ./packer/mount-efs.sh <file system domain> <mount path>
```
  * In mount path, there are four folders that will be created: Input, Output, Error and logs. These folders can be accessed from any instance for which file system is mounted.
  * Mmount path should be different from mounted path used in `k8-f2f-service` instance (in this example different from /data/folder-to-folder)

### Running Service

* Once all componenets are created, final setup should have following components:
  ```shell
      - `k8-f2f-service`- ec2 instnace - Instance in which k8-folder-folder copy service containers are located and which monitors and process files
      - `k8-f2f-user` - ec2 instance - a demo instance which is used to demonstrate file processing from another instance which can be used by normal users without needing access to `k8-f2f-service` instance
      - `k8-f2f-efs` - EFS file system - A file system which can be mounted to any number of instances for supplying file to processing service
  ```
#### Demo from `k8-f2f-service`:

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

#### Demo from `k8-f2f-user`:

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
