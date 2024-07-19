﻿using System.Linq;
using NebulaAPI.Packets;
using NebulaModel.Logger;
using NebulaModel.Networking;
using NebulaModel.Packets;
using NebulaModel.Packets.Combat;

namespace NebulaNetwork.PacketProcessors.Combat
{
    [RegisterPacketProcessor]
    public class DFSClientHatredSyncProcessor : PacketProcessor<DFSClientHatredSyncPacket>
    {
        protected override void ProcessPacket(DFSClientHatredSyncPacket packet, NebulaConnection conn)
        {
            Log.Debug($"DFSClientHatredSyncPacket Adding {packet.Hatred} hatred to EnemyPool {packet.EnemyPoolId} via unit {packet.UnitComponentId}");

            var unitBuffer = GameMain.spaceSector.combatSpaceSystem.units.buffer;
            if (GameMain.spaceSector.combatSpaceSystem.units.buffer[packet.UnitComponentId].id != packet.UnitComponentId)
            {
                Log.Debug($"DFSClientHatredSyncPacket error: Unable to find UnitBuffer with ID {packet.UnitComponentId}");
                return;
            }

            if (GameMain.spaceSector.enemyPool[packet.EnemyPoolId].id != packet.EnemyPoolId)
            {
                Log.Debug($"DFSClientHatredSyncPacket error: Unable to find EnemyPool with ID {packet.EnemyPoolId}");
                return;
            }
            
            unitBuffer[packet.UnitComponentId].hatred.HateTarget(EObjectType.Enemy, packet.EnemyPoolId, packet.Hatred, 3000, EHatredOperation.Add);
        }
    }
}
