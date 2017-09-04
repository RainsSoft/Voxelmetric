using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Core;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.VM
{
    public class VmClient : VmSocketState.IMessageHandler
    {
        protected World m_World;
        private IPAddress m_ServerIP;
        private Socket m_ClientSocket;

        public bool m_Connected;

        private bool m_DebugClient = false;

        public IPAddress ServerIP { get { return m_ServerIP; } }

        public VmClient(World world, IPAddress serverIP = null)
        {
            this.m_World = world;
            this.m_ServerIP = serverIP;
            ConnectToServer();
        }

        private void ConnectToServer()
        {
            m_ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            if (m_ServerIP == null)
            {
                string serverName = Dns.GetHostName();
                Debug.Log("serverName='" + serverName + "'");
                IPAddress serverAddress = Dns.GetHostAddresses(serverName)[0];
                Debug.Log("serverAddress='" + serverAddress + "'");
                m_ServerIP = serverAddress;
            }
            IPEndPoint serverEndPoint = new IPEndPoint(m_ServerIP, 8000);

            m_ClientSocket.BeginConnect(serverEndPoint, OnConnect, null);
        }

        private void OnConnect(IAsyncResult ar)
        {
            try
            {

                if (m_ClientSocket == null || !m_ClientSocket.Connected)
                {
                    Debug.Log("VmClient.OnConnect (" + Thread.CurrentThread.ManagedThreadId + "): "
                              + "server connection rejected because connection was shutdown or not started");
                    return;
                }

                m_ClientSocket.EndConnect(ar);
                m_Connected = true;

                VmSocketState socketState = new VmSocketState(this);
                m_ClientSocket.BeginReceive(socketState.buffer, 0, VmNetworking.bufferLength, SocketFlags.None, OnReceiveFromServer, socketState);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void OnReceiveFromServer(IAsyncResult ar)
        {
            try
            {
                if (m_ClientSocket == null || !m_ClientSocket.Connected)
                {
                    Debug.Log("VmClient.OnReceiveFromServer (" + Thread.CurrentThread.ManagedThreadId + "): "
                              + "server message rejected because connection was shutdown or not started");
                    return;
                }

                int received = m_ClientSocket.EndReceive(ar);
                if (received == 0)
                {
                    Debug.Log("disconnected from server");
                    Disconnect();
                    return;
                }

                if (m_DebugClient)
                    Debug.Log("VmClient.OnReceiveFromServer (" + Thread.CurrentThread.ManagedThreadId + "): ");

                VmSocketState socketState = ar.AsyncState as VmSocketState;
                socketState.Receive(received, 0);
                if (m_ClientSocket != null && m_ClientSocket.Connected)
                {
                    // Should be able to use a mutex but unity doesn't seem to like it
                    m_ClientSocket.BeginReceive(socketState.buffer, 0, VmNetworking.bufferLength, SocketFlags.None, OnReceiveFromServer, socketState);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void Send(byte[] buffer)
        {
            if (!m_Connected)
                return;

            try
            {
                m_ClientSocket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, OnSend, null);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        private void OnSend(IAsyncResult ar)
        {
            try
            {
                m_ClientSocket.EndSend(ar);
                if (m_DebugClient)
                    Debug.Log("VmClient.OnSend (" + Thread.CurrentThread.ManagedThreadId + "): send ended");
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                Disconnect();
            }
        }

        public int GetExpectedSize(byte messageType)
        {
            switch (messageType)
            {
                case VmNetworking.SendBlockChange:
                    return 15;
                case VmNetworking.transmitChunkData:
                    //TODO TCD So that small chunks don't need 1025 bytes to be sent...
                    //return -VmServer.leaderSize;
                    return VmNetworking.bufferLength;
                default:
                    return 0;
            }
        }

        public void HandleMessage(byte[] receivedData)
        {
            switch (receivedData[0])
            {
                case VmNetworking.SendBlockChange:
                    Vector3Int pos = new Vector3Int().FromBytes(receivedData, 1);
                    ushort data = BitConverter.ToUInt16(receivedData, 13);
                    ReceiveChange(ref pos, new BlockData(data));
                    break;
                case VmNetworking.transmitChunkData:
                    ReceiveChunk(receivedData);
                    break;
            }
        }

        public void RequestChunk(Vector3Int pos)
        {
            if (m_DebugClient)
                Debug.Log("VmClient.RequestChunk (" + Thread.CurrentThread.ManagedThreadId + "): " + pos);

            byte[] message = new byte[13];
            message[0] = VmNetworking.RequestChunkData;
            pos.ToBytes().CopyTo(message, 1);
            Send(message);
        }

        private void ReceiveChunk(byte[] data)
        {
            Vector3Int pos = new Vector3Int().FromBytes(data, 1);
            Chunk chunk = m_World.Chunks.Get(ref pos);
            // for now just issue an error if it isn't yet loaded
            if (chunk == null)
            {
                Debug.LogError("VmClient.ReceiveChunk (" + Thread.CurrentThread.ManagedThreadId + "): "
                               + "Could not find chunk for " + pos);
            }
            else
                chunk.Blocks.ReceiveChunkData(data);
        }

        public void BroadcastChange(Vector3Int pos, BlockData blockData)
        {
            byte[] data = new byte[GetExpectedSize(VmNetworking.SendBlockChange)];

            data[0] = VmNetworking.SendBlockChange; // 1 B
            pos.ToBytes().CopyTo(data, 1); // 3*4B = 12 B
            BlockData.ToByteArray(blockData).CopyTo(data, 13); // 2 B

            Send(data);
        }

        private void ReceiveChange(ref Vector3Int pos, BlockData block)
        {
            m_World.Blocks.Modify(ref pos, block, false);
        }

        public void Disconnect()
        {
            if (m_ClientSocket != null)// && clientSocket.Connected)
            {
                //clientSocket.Shutdown(SocketShutdown.Both);
                m_ClientSocket.Close();
                m_Connected = false;
                m_ClientSocket = null;
            }
        }

    }
}
