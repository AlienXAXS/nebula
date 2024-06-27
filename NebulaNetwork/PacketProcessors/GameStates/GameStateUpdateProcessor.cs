﻿#region

using System;
using NebulaAPI.Packets;
using NebulaModel;
using NebulaModel.Logger;
using NebulaModel.Networking;
using NebulaModel.Packets;
using NebulaModel.Packets.GameStates;
using NebulaWorld;
using NebulaWorld.GameStates;
using UnityEngine;

#endregion

namespace NebulaNetwork.PacketProcessors.GameStates;

[RegisterPacketProcessor]
// ReSharper disable once UnusedType.Global
public class GameStateUpdateProcessor : PacketProcessor<GameStateUpdate>
{
    private readonly float BUFFERING_TICK = 60f;
    private readonly float BUFFERING_TIME = 30f;
    private float averageUPS = 60f;

    private int averageRTT;
    private bool hasChanged;

    protected override void ProcessPacket(GameStateUpdate packet, NebulaConnection conn)
    {
        var rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - packet.SentTime;
        averageRTT = (int)(averageRTT * 0.8 + rtt * 0.2);
        averageUPS = averageUPS * 0.8f + packet.UnitsPerSecond * 0.2f;
        Multiplayer.Session.World.UpdatePingIndicator($"Ping: {averageRTT}ms");

        // We offset the tick received to account for the time it took to receive the packet
        var tickOffsetSinceSent = (long)Math.Round(packet.UnitsPerSecond * rtt / 2 / 1000);
        var currentGameTick = packet.GameTick + tickOffsetSinceSent;
        var diff = currentGameTick - GameMain.gameTick;

        // Discard abnormal packet (usually after host saving the file)
        if (rtt > 2 * averageRTT || averageUPS - packet.UnitsPerSecond > 15)
        {
            // Initial connection
            if (GameMain.gameTick < 1200L)
            {
                averageRTT = (int)rtt;
                GameMain.gameTick = currentGameTick;
            }
            Log.Debug(
                $"GameStateUpdate unstable. RTT:{rtt}(avg{averageRTT}) UPS:{packet.UnitsPerSecond:F2}(avg{averageUPS:F2})");
            return;
        }

        if (!Config.Options.SyncUps)
        {
            // We allow for a small drift of 5 ticks since the tick offset using the ping is only an approximation
            if (GameMain.gameTick > 0 && Mathf.Abs(diff) > 5)
            {
                Log.Debug($"Game Tick desync. {GameMain.gameTick} skip={diff} UPS:{packet.UnitsPerSecond:F2}(avg{averageUPS:F2})");
                GameMain.gameTick = currentGameTick;
            }
            // Reset FixUPS when user turns off the option
            if (!hasChanged)
            {
                return;
            }
            FPSController.SetFixUPS(0);
            hasChanged = false;
            return;
        }

        // Adjust client's UPS to match game tick with server, range 30~120 UPS
        var ups = diff / 1f + averageUPS;
        long skipTick = 0;
        switch (ups)
        {
            case > GameStatesManager.MaxUPS:
                {
                    // Try to distribute game tick difference into BUFFERING_TIME (seconds)
                    if (diff / BUFFERING_TIME + averageUPS > GameStatesManager.MaxUPS)
                    {
                        // The difference is too large, need to skip ticks to catch up
                        skipTick = (long)(ups - GameStatesManager.MaxUPS);
                    }
                    ups = GameStatesManager.MaxUPS;
                    break;
                }
            case < GameStatesManager.MinUPS:
                {
                    if (diff + averageUPS - GameStatesManager.MinUPS < -BUFFERING_TICK)
                    {
                        skipTick = (long)(ups - GameStatesManager.MinUPS);
                    }
                    ups = GameStatesManager.MinUPS;
                    break;
                }
        }
        if (skipTick != 0)
        {
            Log.Debug($"Game Tick desync. skip={skipTick} diff={diff,2}, RTT={rtt}ms, UPS={packet.UnitsPerSecond:F2}(avg{averageUPS:F2})");
            GameMain.gameTick += skipTick;
        }
        FPSController.SetFixUPS(ups);
        hasChanged = true;
        // Tick difference in the next second. Expose for other mods
        GameStatesManager.NotifyTickDifference(diff / 1f + averageUPS - ups);
    }
}
