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
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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
            MethodInfo HeatInput = ItemHeater.getMethod();
            MethodInfo HeatInputTP = AccessTools.Method(typeof(ItemHeater), "HeatInput_Transpiler");
            harmony.Patch(GT3, transpiler: GT3TP);
            harmony.Patch(GT2, transpiler: GT2TP);
            harmony.Patch(HeatInput, transpiler: HeatInputTP);
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony.UnpatchAll();
        }
    }

    internal static class ItemHeater
    {
        public static MethodInfo getMethod()
        {
            return AccessTools.Method(typeof(BlockEntityFirepit), "heatInput");
        }
        public static IEnumerable<CodeInstruction> HeatInput_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            int injectionstartpoint = -1;
            int injectionendpoint = 0;
            bool injectionpointfound = false;

            //find the injection point start and end
            //start matches lcloc.s 5 followed by ldloc.3
            //end matches ldloc.3 then div and finally stloc.5
            for (int i = 0; i < codes.Count-2; i++)
            {
                if (codes[i].opcode == OpCodes.Ldloc_S
                    && codes[i].operand is LocalBuilder lb1
                    && lb1.LocalIndex == 5
                    && injectionstartpoint < 0)
                {
                    if (i + 1 < codes.Count
                        && codes[i + 1].opcode == OpCodes.Ldloc_3)
                    {
                        injectionstartpoint = i;
                    }
                }
                if (injectionstartpoint >= 0) //find end point
                {
                    if(codes[i].opcode == OpCodes.Ldloc_3
                        && codes[i + 1].opcode == OpCodes.Div
                        && codes[i + 2].opcode == OpCodes.Stloc_S && codes[i + 2].operand is LocalBuilder lb2 && lb2.LocalIndex == 5)
                    {
                        injectionendpoint = i + 2;
                        injectionpointfound = true;
                        break;
                    }
                }
            }

            if (injectionpointfound)
            {
                //remove existing code
                for (int i = injectionstartpoint; i < injectionendpoint; i++)
                {
                    codes.RemoveAt(injectionstartpoint);
                }
                //inject call to ModifyHeating
                codes.Insert(injectionstartpoint, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ItemHeater), "ModifyHeating")));
                codes.Insert(injectionstartpoint, new CodeInstruction(OpCodes.Ldloc_0));
                codes.Insert(injectionstartpoint, new CodeInstruction(OpCodes.Ldloc_3));
                codes.Insert(injectionstartpoint, new CodeInstruction(OpCodes.Ldloc_S, 5));
            }
            else
            {
                throw new System.Exception("Injection range not found in HeatInput_Transpiler");
            }

            return codes.AsEnumerable();
        }

        public static float ModifyHeating(float newTemp, float stacksize, float oldTemp)
        {
            //newtemp is number
            float k = 1.05f;
            float effectiveStackSize = (float)Math.Ceiling(stacksize * k * 2f / 3f);
            newTemp = (newTemp + (effectiveStackSize - 1f) * oldTemp) / effectiveStackSize;
            return newTemp;
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
