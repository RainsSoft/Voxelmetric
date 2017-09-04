using System;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Voxelmetric.Code.Common.Extensions;
using Voxelmetric.Code.Data_types;

namespace Voxelmetric.Code.VM
{
    internal class ClientConnection : VmSocketState.IMessageHandler
    {
        private Socket m_Socket;
        private VmServer m_Server;

        private bool m_DebugClientConnection = false;

        public int ID { get; private set; }

        public ClientConnection(int ID, Socket socket, VmServer server)
        {
            this.ID = ID;
            this.m_Socket = socket;
            this.m_Server = server;
            if (m_DebugClientConnection)
                Debug.Log("ClientConnection.ClientConnection (" + Thread.CurrentThread.ManagedThreadId + "): "
                          + "Client " + ID + " has connected");

            VmSocketState socketState = new VmSocketState(this);
            socket.BeginReceive(socketState.buffer, 0, VmNetworking.bufferLength, SocketFlags.None, OnReceiveFromClient, socketState);
        }

        private void OnReceiveFromClient(IAsyncResult ar)
        {
            try
            {
                if (m_Socket == null || !m_Socket.Connected)
                {
                    Debug.Log("ClientConnection.OnReceiveFromClient (" + Thread.CurrentThread.ManagedThreadId + "): "
                              + "client message rejected because connection was shutdown or not started");
                    return;
                }

                int received = m_Socket.EndReceive(ar);
                if (received == 0)
                {
                    Disconnect();
                    return;
                }

                if (m_DebugClientConnection)
                    Debug.Log("ClientConnection.OnReceiveFromClient (" + Thread.CurrentThread.ManagedThreadId + "): " + ID);

                VmSocketState socketState = ar.AsyncState as VmSocketState;
                socketState.Receive(received, 0);

                if (m_Socket != null && m_Socket.Connected)
                {
                    // Should be able to use a mutex but unity doesn't seem to like it
                    m_Socket.BeginReceive(socketState.buffer, 0, VmNetworking.bufferLength, SocketFlags.None, OnReceiveFromClient, socketState);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        public int GetExpectedSize(byte type)
        {
            switch (type)
            {
                case VmNetworking.SendBlockChange:
                    return 17;
                case VmNetworking.RequestChunkData:
                    return 13;
                default:
                    return 0;
            }
        }

        public void HandleMessage(byte[] receivedData)
        {
            Vector3Int pos = Vector3Int.zero;

            switch (receivedData[0])
            {
                case VmNetworking.SendBlockChange:
                    pos = pos.FromBytes(receivedData, 1);
                    ushort data = BlockData.RestoreBlockData(receivedData, 13);
                    m_Server.ReceiveChange(ref pos, data, ID);
                    break;
                case VmNetworking.RequestChunkData:
                    pos = pos.FromBytes(receivedData, 1);

                    if (m_DebugClientConnection)
                        Debug.Log("ClientConnection.HandleMessage (" + Thread.CurrentThread.ManagedThreadId + "): " + ID
                                  + " " + pos);

                    m_Server.RequestChunk(ref pos, ID);
                    break;
            }
        }

        public void Send(byte[] buffer)
        {
            try
            {
                m_Socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, OnSend, m_Socket);
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
                m_Socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        public void Disconnect()
        {
            if (m_DebugClientConnection)
                Debug.Log("ClientConnection.Disconnect (" + Thread.CurrentThread.ManagedThreadId + "): "
                          + "Client " + ID + " has disconnected");
            try
            {
                if (m_Socket != null)
                {// && socket.Connected) {
                    //socket.Shutdown(SocketShutdown.Both);
                    m_Socket.Close();
                    m_Socket = null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
            m_Server.RemoveClient(ID);
        }
    }
}
