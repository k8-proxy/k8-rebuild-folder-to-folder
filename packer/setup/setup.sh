#!/bin/bash

# get source code
cd ~
git clone https://github.com/k8-proxy/k8-rebuild-folder-to-folder.git && cd k8-rebuild-folder-to-folder
git clone https://github.com/k8-proxy/k8-rebuild.git --recursive && cd k8-rebuild && git submodule foreach git pull origin main && cd ../

# install docker and docker-compose
END=$((SECONDS+300))
# retry till apt is available
while [ $SECONDS -lt $END ]; do
    sleep 10s
    sudo apt update
        sudo apt-get install \
        apt-transport-https \
        ca-certificates \
        curl \
        gnupg-agent \
        software-properties-common -y
    if [[ $? -eq 0 ]];then
    break
    fi
done
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo apt-key add -
sudo add-apt-repository \
   "deb [arch=amd64] https://download.docker.com/linux/ubuntu \
   $(lsb_release -cs) \
   stable"
sudo apt-get update
sudo DEBIAN_FRONTEND=noninteractive apt-get install docker-ce docker-ce-cli containerd.io -y
sudo curl -L "https://github.com/docker/compose/releases/download/1.27.4/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# start applications
sudo docker-compose up -d
sleep 10s
