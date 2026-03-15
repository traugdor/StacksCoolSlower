using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace StacksCoolSlower
{
    public class StacksCoolSlowerModSystem : ModSystem
    {
        Harmony harmony = new Harmony("StacksCoolSlower");

        public override void StartServerSide(ICoreServerAPI api)
        {
            MethodInfo GT3 = BeCoolMan.GetGetTemperatureMethod(3);
            MethodInfo GT2 = BeCoolMan.GetGetTemperatureMethod(2);
            MethodInfo GT3TP = AccessTools.Method(typeof(BeCoolMan), "GetTemperature3_transpiler");
            MethodInfo GT2TP = AccessTools.Method(typeof(BeCoolMan), "GetTemperature2_transpiler");
            harmony.Patch(GT3, transpiler: GT3TP);
            harmony.Patch(GT2, transpiler: GT2TP);
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony.UnpatchAll();
        }
    }

    internal static class BeCoolMan
    {
        public static IEnumerable<CodeInstruction> GetTemperature3_transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            int injectionpoint = 0;
            bool injectionpointfound = false;

            for(;injectionpoint < codes.Count; injectionpoint++)
            {
                if (codes[injectionpoint].opcode == OpCodes.Stloc_3)
                {
                    if(injectionpoint > 0 
                        && codes[injectionpoint-1].opcode == OpCodes.Call
                        && codes[injectionpoint-1].operand.ToString().Contains("Max"))
                    {
                        injectionpointfound = true;
                        break;
                    }
                }
            }

            if (!injectionpointfound)
            {
                throw new System.Exception("Injection point not found in CollectibleObject.GetTemperature(IWorldAccessor, ItemStack, float64)");
            }

            injectionpoint++;

            var toInject = new List<CodeInstruction>
            {
                //build call to ModifyCooling
                new CodeInstruction(OpCodes.Ldloc_3),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Call, typeof(BeCoolMan).GetMethod("ModifyCooling")),
                new CodeInstruction(OpCodes.Stloc_3)
            };

            codes.InsertRange(injectionpoint, toInject);

            return codes.AsEnumerable();
        }

        public static IEnumerable<CodeInstruction> GetTemperature2_transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            int injectionpoint = 0;
            bool injectionpointfound = false;

            for (; injectionpoint < codes.Count; injectionpoint++)
            {
                if (codes[injectionpoint].opcode == OpCodes.Stloc_S
                    && codes[injectionpoint].operand is LocalBuilder lb1
                    && lb1.LocalIndex == 4)
                {
                    if (injectionpoint > 0
                        && codes[injectionpoint - 1].opcode == OpCodes.Mul)
                    {
                        injectionpointfound = true;
                        break;
                    }
                }
            }

            if (!injectionpointfound)
            {
                throw new System.Exception("Injection point not found in <>c__DisplayClass154_0.<GetTemperatur>b__1");
            }

            injectionpoint++;

            var toInject = new List<CodeInstruction>
            {
                //build call to ModifyCooling
                new CodeInstruction(OpCodes.Ldloc_S, 4),
                new CodeInstruction(OpCodes.Ldnull),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, typeof(BeCoolMan).GetMethod("ModifyCooling")),
                new CodeInstruction(OpCodes.Stloc_S, 4)
            };

            codes.InsertRange(injectionpoint, toInject);

            return codes.AsEnumerable();
        }

        public static MethodInfo GetGetTemperatureMethod(int index)
        {
            var nested2 = typeof(CollectibleObject).GetNestedType("<>c__DisplayClass154_0", BindingFlags.NonPublic);
            MethodInfo method = null;
            switch (index)
            {
                case 3:
                    method = typeof(CollectibleObject).GetMethod(
                        "GetTemperature",
                        BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new Type[] { typeof(IWorldAccessor), typeof(ItemStack), typeof(double) },
                        null
                    );
                    break;
                case 2:
                    method = nested2.GetMethod("<GetTemperature>b__1", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
                default:
                    throw new System.Exception("Invalid index for GetGetTemperatureMethod. Valid values are 2 and 3.");
            }
            if(method == null)
                throw new Exception("Failed to find GetTemperature method for patching.");
            return method;
        }

        public static double ModifyCooling(float number, ItemStack itemstack, object closure = null)
        {
            if(closure != null)
            {
                var field = closure.GetType().GetField("itemstack");
                ItemStack itemStack = (ItemStack)field.GetValue(closure);

                if(itemStack != null && itemStack.StackSize > 1)
                {
                    number /= itemStack.StackSize;
                }
            }
            else
            {
                if (itemstack != null && itemstack.StackSize > 1)
                {
                    number /= itemstack.StackSize;
                }
            }

            return number;
        }
    }
}
