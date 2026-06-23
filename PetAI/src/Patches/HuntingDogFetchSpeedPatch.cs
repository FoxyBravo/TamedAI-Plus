using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace PetAI
{
    /// <summary>
    /// Slow down the huntingdog's fetch speed to match (or get closer to)
    /// the tamed wolf. The vanilla huntingdog has a higher base movement
    /// speed than the wolftaming tamed wolf, so even with the same
    /// AiTaskPlayFetch.moveSpeed it runs noticeably faster — which makes
    /// the fetch feel rushed and the pickup less reliable. We patch the
    /// task's constructor via reflection (the wolftaming assembly isn't
    /// directly referenced) and lower moveSpeed for huntingdog entities.
    /// </summary>
    public class HuntingDogFetchSpeedPatch
    {
        public static void Patch(Harmony harmony)
        {
            Type playFetchType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("WolfTaming.AiTaskPlayFetch");
                if (t != null) { playFetchType = t; break; }
            }
            if (playFetchType == null) return;

            // The constructor signature is (EntityAgent, JsonObject, JsonObject)
            var ctor = playFetchType.GetConstructor(new[] { typeof(EntityAgent), typeof(JsonObject), typeof(JsonObject) });
            if (ctor == null) return;

            harmony.Patch(ctor, postfix: new HarmonyMethod(typeof(HuntingDogFetchSpeedPatch), nameof(Postfix)));
        }

        public static void Postfix(object __instance, EntityAgent entity)
        {
            if (entity?.Code?.Path == null) return;
            if (!entity.Code.Path.Contains("huntingdog")) return;

            FieldInfo moveSpeedField = __instance.GetType().GetField("moveSpeed", BindingFlags.Public | BindingFlags.Instance);
            if (moveSpeedField == null) return;

            // Default is 0.03f. Drop to 0.02f for huntingdog so the fetch
            // pace is between the tamed wolf and the huntingdog's current pace.
            moveSpeedField.SetValue(__instance, 0.02f);
        }
    }
}
