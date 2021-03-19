﻿using NebulaModel.DataStructures;
using NebulaModel.Networking;
using NebulaModel.Networking.Serialization;
using NebulaModel.Packets.GameStates;
using NebulaModel.Utils;
using NebulaWorld;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace NebulaHost
{
    public class MultiplayerHostSession : MonoBehaviour, INetworkProvider
    {
        public static MultiplayerHostSession Instance { get; protected set; }

        private WebSocketServer socketServer;

        public PlayerManager PlayerManager { get; protected set; }
        public NetPacketProcessor PacketProcessor { get; protected set; }

        private readonly Queue<PendingPacket> pendingPackets = new Queue<PendingPacket>();

        float gameStateUpdateTimer = 0;

        private void Awake()
        {
            Instance = this;
        }

        public void StartServer(int port)
        {
            PlayerManager = new PlayerManager();
            PacketProcessor = new NetPacketProcessor();
            PacketUtils.RegisterAllPacketNestedTypes(PacketProcessor);
            PacketUtils.RegisterAllPacketProcessorsInCallingAssembly(PacketProcessor);

            socketServer = new WebSocketServer(port);
            socketServer.AddWebSocketService("/socket", () => new WebSocketService(PlayerManager, PacketProcessor, pendingPackets));

            socketServer.Start();

            SimulatedWorld.Initialize();

            LocalPlayer.SetNetworkProvider(this);
            LocalPlayer.IsMasterClient = true;

            // TODO: Load saved player info here
            LocalPlayer.SetPlayerData(new PlayerData(PlayerManager.GetNextAvailablePlayerId(), new Float3(1.0f, 0.6846404f, 0.243137181f)));
        }

        private void StopServer()
        {
            socketServer?.Stop();
        }

        public void DestroySession()
        {
            StopServer();
            Destroy(gameObject);
        }

        public void SendPacket<T>(T packet) where T : class, new()
        {
            PlayerManager.SendPacketToAllPlayers(packet);
        }

        private void Update()
        {
            gameStateUpdateTimer += Time.deltaTime;
            if (gameStateUpdateTimer > 1)
            {
                SendPacket(new GameStateUpdate() { State = new GameState(TimeUtils.CurrentUnixTimestampMilliseconds(), GameMain.gameTick) });
            }

            lock (pendingPackets)
            {
                while (pendingPackets.Count > 0)
                {
                    PendingPacket packet = pendingPackets.Dequeue();
                    PacketProcessor.ReadPacket(new NetDataReader(packet.Data), packet.Connection);
                }
            }
        }

        private class WebSocketService : WebSocketBehavior
        {
            private readonly PlayerManager playerManager;
            private readonly NetPacketProcessor packetProcessor;
            private readonly Queue<PendingPacket> pendingPackets;

            public WebSocketService(PlayerManager playerManager, NetPacketProcessor packetProcessor, Queue<PendingPacket> pendingPackets)
            {
                this.playerManager = playerManager;
                this.packetProcessor = packetProcessor;
                this.pendingPackets = pendingPackets;
            }

            protected override void OnClose(CloseEventArgs e)
            {
                NebulaModel.Logger.Log.Info($"Client disconnected: {this.Context.UserEndPoint}, reason: {e.Reason}");
                playerManager.PlayerDisconnected(new NebulaConnection(this.Context.WebSocket, packetProcessor));
            }

            protected override void OnError(ErrorEventArgs e)
            {
                // TODO: Decide what to do here - does OnClose get called too?
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                lock (pendingPackets)
                {
                    pendingPackets.Enqueue(new PendingPacket(e.RawData, new NebulaConnection(this.Context.WebSocket, packetProcessor)));
                }
            }

            protected override void OnOpen()
            {
                NebulaModel.Logger.Log.Info($"Client connected ID: {this.ID}, {this.Context.UserEndPoint}");
                NebulaConnection conn = new NebulaConnection(this.Context.WebSocket, packetProcessor);
                playerManager.PlayerConnected(conn);
            }
        }
    }
}