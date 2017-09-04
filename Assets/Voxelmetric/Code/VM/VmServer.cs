using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.VM
{
    public class VmServer
    {
        protected World m_World;
        private IPAddress m_ServerIP;
        private Socket m_ServerSocket;

        private Dictionary<int, ClientConnection> m_Clients = new Dictionary<int, ClientConnection>();
        private int m_NextId = 0;

        private bool m_DebugServer = false;

        public IPAddress ServerIP { get { return m_ServerIP; } }

        public int ClientCount
        {
            get
            {
                lock (m_Clients)
                {
                    return m_Clients.Count;
                }
            }
        }

        public VmServer(World world)
        {
            this.m_World = world;

            try
            {
                AddressFamily addressFamily = AddressFamily.InterNetwork;
                m_ServerSocket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);

                string serverName = Dns.GetHostName();
                if (m_DebugServer) Debug.Log("serverName='" + serverName + "'");
                foreach (IPAddress serverAddress in Dns.GetHostAddresses(serverName))
                {
                    if (m_DebugServer) Debug.Log("serverAddress='" + serverAddress + "', AddressFamily=" + serverAddress.AddressFamily);
                    if (serverAddress.AddressFamily != addressFamily)
                        continue;
                    m_ServerIP = serverAddress;
                    break;
                }
                IPEndPoint serverEndPoint = new IPEndPoint(m_ServerIP, 8000);
                m_ServerSocket.Bind(serverEndPoint);
                m_ServerSocket.Listen(0);
                m_ServerSocket.BeginAccept(OnJoinServer, null);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void OnJoinServer(IAsyncResult ar)
        {
            try
            {
                if (m_ServerSocket == null)
                {
                    Debug.Log("VmServer.OnJoinServer (" + Thread.CurrentThread.ManagedThreadId + "): "
                              + "client connection rejected because server was not started");
                    return;
                }
                Socket newClientSocket = m_ServerSocket.EndAccept(ar);
                lock (m_Clients)
                {
                    ClientConnection connection = new ClientConnection(m_Clients.Count, newClientSocket, this);
                    m_Clients.Add(m_NextId, connection);
                    m_NextId++;
                }

                m_ServerSocket.BeginAccept(OnJoinServer, null);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        internal void RemoveClient(int id)
        {
            lock (m_Clients)
            {
                m_Clients[id] = null;
            }
        }

        public void Disconnect()
        {
            lock (m_Clients)
            {
                var clientConnections = m_Clients.Values.ToList();
                foreach (var client in clientConnections)
                    client.Disconnect();
            }

            if (m_ServerSocket != null)
            {// && serverSocket.Connected) {
                //serverSocket.Shutdown(SocketShutdown.Both);
                m_ServerSocket.Close();
                m_ServerSocket = null;
            }
        }

        public void SendToClient(byte[] data, int client)
        {
            lock (m_Clients)
            {
                ClientConnection clientConnection = m_Clients[client];
                if (clientConnection != null)
                    clientConnection.Send(data);
            }
        }

        public void RequestChunk(ref Vector3Int pos, int id)
        {
            Chunk chunk = null;
            if (m_World == null)
            {
                Debug.LogError("VmServer.RequestChunk (" + Thread.CurrentThread.ManagedThreadId + "): "
                               + " world not set (" + pos + ", " + id + ")");
            }
            else
                chunk = m_World.Chunks.Get(ref pos);

            byte[] data;

            //for now return an empty chunk if it isn't yet loaded
            // Todo: load the chunk then send it to the player
            if (chunk == null)
            {
                Debug.LogError("VmServer.RequestChunk (" + Thread.CurrentThread.ManagedThreadId + "): "
                               + "Could not find chunk for " + pos);
                data = ChunkBlocks.EmptyBytes;
            }
            else
                data = chunk.Blocks.ToBytes();

            if (m_DebugServer)
                Debug.Log("VmServer.RequestChunk (" + Thread.CurrentThread.ManagedThreadId + "): " + id
                          + " " + pos);

            SendChunk(pos, data, id);
        }

        public const int HEADER_SIZE = 13, LEADER_SIZE = HEADER_SIZE + 8;

        protected void SendChunk(Vector3Int pos, byte[] chunkData, int id)
        {
            int chunkDataIndex = 0;
            while (chunkDataIndex < chunkData.Length)
            {
                byte[] message = new byte[VmNetworking.bufferLength];
                message[0] = VmNetworking.transmitChunkData;
                pos.ToBytes().CopyTo(message, 1);
                BitConverter.GetBytes(chunkDataIndex).CopyTo(message, HEADER_SIZE);
                BitConverter.GetBytes(chunkData.Length).CopyTo(message, HEADER_SIZE + 4);

                if (m_DebugServer)
                    Debug.Log("VmServer.SendChunk (" + Thread.CurrentThread.ManagedThreadId + "): " + pos
                              + ", chunkDataIndex=" + chunkDataIndex
                              + ", chunkData.Length=" + chunkData.Length
                              + ", buffer=" + message.Length);

                for (int i = LEADER_SIZE; i < message.Length; i++)
                {
                    message[i] = chunkData[chunkDataIndex];
                    chunkDataIndex++;

                    if (chunkDataIndex >= chunkData.Length)
                        break;
                }

                SendToClient(message, id);
            }
        }

        public void BroadcastChange(Vector3Int pos, BlockData blockData, int excludedUser)
        {
            lock (m_Clients)
            {
                if (m_Clients.Count == 0)
                    return;

                byte[] data = new byte[15];

                data[0] = VmNetworking.SendBlockChange;
                pos.ToBytes().CopyTo(data, 1);
                BlockData.ToByteArray(blockData).CopyTo(data, 13);

                foreach (var client in m_Clients.Values)
                {
                    if (excludedUser == -1 || client.ID != excludedUser)
                        client.Send(data);
                }
            }
        }

        public void ReceiveChange(ref Vector3Int pos, ushort data, int id)
        {
            BlockData blockData = new BlockData(data);
            m_World.Blocks.Modify(ref pos, blockData, true);
            BroadcastChange(pos, blockData, id);
        }
    }
}
