﻿#region

using HarmonyLib;
using NebulaModel.Packets.Trash;
using NebulaWorld;

#endregion

namespace NebulaPatcher.Patches.Dynamic;

[HarmonyPatch(typeof(TrashSystem))]
internal class TrashSystem_Patch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(TrashSystem.ClearAllTrash))]
    public static void ClearAllTrash_Postfix()
    {
        //Send notification, that somebody clicked on "ClearAllTrash"
        if (Multiplayer.IsActive && !Multiplayer.Session.Trashes.ClearAllTrashFromOtherPlayers)
        {
            Multiplayer.Session.Network.SendPacket(new TrashSystemClearAllTrashPacket());
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(TrashSystem.AddTrashFromGroundEnemy))]
    public static bool AddTrashFromGroundEnemy_Prefix()
    {
        // Tempoaray ban enemy drop
        return !Multiplayer.IsActive;
    }
}
