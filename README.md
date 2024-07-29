# Cryptopia Node Deployment
Reload
This guide provides instructions to deploy the Cryptopia Node on an Ubuntu server.

## Prerequisites

- Ubuntu 24.04 (LTS) x64 minimal server with 1 GB Memory / 25 GB Disk
- Open the following ports in your firewall:
  - 8000 (WebSocket server)
  - 3478 (STUN/TURN UDP traffic)
  - 3478 (STUN/TURN TCP traffic)
  - 5349 (TURN over TLS)
  - 3033 (ICE server)
  - 59000-65000 (TURN/STUN server for WebRTC)

## Steps to Deploy

1. **Connect to Your Ubuntu Server**:
   ```sh
   ssh root@your_server_ip
   ```

2. **Install Docker**:
   ```sh
   sudo apt-get update
   sudo apt-get install -y docker.io
   ```

3. **Install Docker Compose**:
   ```sh
   sudo curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
   sudo chmod +x /usr/local/bin/docker-compose
   ```

4. **Clone the Repository**:
   ```sh
   git clone https://github.com/cryptopia-com/cryptopia-node.git
   cd cryptopia-node
   ```

5. **Build and Start Containers**:
   ```sh
   docker-compose up -d --build
   ```

6. **View Logs**:
   - To view the logs of the running container:
     ```sh
     docker ps
     docker logs <container_id_or_name>
     ```
   - To follow logs in real-time:
     ```sh
     docker logs -f <container_id_or_name>
     ```

## Notes

- Ensure all the necessary ports are open on your server to allow proper communication.
- This setup is intended for testing and experimental purposes only.
