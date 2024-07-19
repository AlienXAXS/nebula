using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NebulaModel.Logger;
using NebulaModel.Packets.Combat;
using NebulaWorld;
using UnityEngine;

namespace NebulaPatcher.Patches.Transpilers
{
    [HarmonyPatch(typeof(UnitComponent))]
    internal class UnitComponent_Transplier
    {
        /*
         * Calls SpaceVesselAttackAddHatred after HateTarget is called within SensorLogic.
         * This allows the clients threat to be added to the host due to unsynced space vessels
         * attacking relays and other things not updating the hosts threat values.
         */
        [HarmonyTranspiler]
        [HarmonyPatch(nameof(UnitComponent.SensorLogic_Space))]
        public static IEnumerable<CodeInstruction> SensorLogic_Space_Transplier(IEnumerable<CodeInstruction> instructions)
        {
            try
            {
                var codeMatcher = new CodeMatcher(instructions);

                codeMatcher.MatchForward(true,
                    new CodeMatch(x => x.opcode == OpCodes.Call && ((MethodInfo)x.operand).Name == "HateTarget"));

                if (codeMatcher.IsInvalid)
                {
                    Log.Error(
                        "Transpiler SensorLogic_Space_Transplier matcher is not valid. Mod version not compatible with game version.");
                    return instructions;
                }

                var enemyPoolIndex = codeMatcher.InstructionAt(-12);
                var craftUnitSensorRange = codeMatcher.InstructionAt(-11);
                var num3 = codeMatcher.InstructionAt(-10);

                var unitComponentId = new CodeInstruction[2];
                unitComponentId[0] = new CodeInstruction(OpCodes.Ldarg_0);
                unitComponentId[1] = new CodeInstruction(OpCodes.Ldfld,
                    AccessTools.Field(typeof(UnitComponent), nameof(UnitComponent.id)));

                codeMatcher = codeMatcher.Advance(1)
                    .InsertAndAdvance(unitComponentId)
                    .InsertAndAdvance(enemyPoolIndex)
                    .InsertAndAdvance(craftUnitSensorRange)
                    .InsertAndAdvance(num3)
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(UnitComponent_Transplier), nameof(SpaceVesselAttackAddHatred))));

                return codeMatcher.InstructionEnumeration();
            }
            catch (Exception e)
            {
                Log.Error("Transpiler DFRelayComponent.RelaySailLogic failed. Mod version not compatible with game version.");
                Log.Error(e);
                return instructions;
            }
        }

        public static void SpaceVesselAttackAddHatred(int unitComponentId, int enemyPoolIndex, float craftUnitSensorRange, float num3)
        {
            if (!Multiplayer.IsActive) return;
            if (Multiplayer.Session.IsDedicated || Multiplayer.Session.IsServer) return; // Only run on a client

            var hatredAmount = (int)((craftUnitSensorRange - Mathf.Sqrt(num3)) * 0.1f + 0.5f);
            Multiplayer.Session.Network.SendPacket(new DFSClientHatredSyncPacket(unitComponentId, enemyPoolIndex, hatredAmount));
        }
    }
}
