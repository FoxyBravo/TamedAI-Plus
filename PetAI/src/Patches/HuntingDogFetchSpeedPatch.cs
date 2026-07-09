using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace TamedAIPlus
{
    /// <summary>
    /// Slow down the huntingdog's fetch speed to match (or get closer to)
    /// the tamed wolf. The vanilla huntingdog has a higher base movement
    /// speed than the wolftaming tamed wolf, so the same
    /// AiTaskPlayFetch.moveSpeed makes it run noticeably faster.
    ///
    /// Implementation: defer the patch by 1 tick (RegisterCallback) so the
    /// wolftaming assembly is loaded by the time we look it up via
    /// reflection (TamedAI-Plus's Start runs before wolftaming is loaded). Then
    /// Harmony-prefix StartExecute on the wolftaming AiTaskPlayFetch and,
    /// when the entity is a huntingdog, drop moveSpeed from the default
    /// 0.03f to 0.02f. Patching StartExecute is more reliable than
    /// patching the constructor because moveSpeed is actually read inside
    /// StartExecute, so we know the value will be live when the dog starts
    /// walking.
    /// </summary>
    public class HuntingDogFetchSpeedPatch
    {
        public static void Patch(Harmony harmony, ICoreAPI api)
        {
            api.Event.RegisterCallback((_) => ApplyPatch(harmony), 1);
        }

        private static void ApplyPatch(Harmony harmony)
        {
            Type playFetchType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("WolfTaming.AiTaskPlayFetch");
                if (t != null) { playFetchType = t; break; }
            }
            if (playFetchType == null) return;

            MethodInfo startExecute = playFetchType.GetMethod("StartExecute",
                BindingFlags.Public | BindingFlags.Instance);
            if (startExecute == null) return;

            harmony.Patch(startExecute, prefix: new HarmonyMethod(typeof(HuntingDogFetchSpeedPatch), nameof(StartExecutePrefix)));
        }

        public static void StartExecutePrefix(object __instance)
        {
            // entity is a protected field on the AiTaskBase base class
            FieldInfo entityField = __instance.GetType().BaseType
                .GetField("entity", BindingFlags.NonPublic | BindingFlags.Instance);
            if (entityField == null) return;

            var entity = entityField.GetValue(__instance) as EntityAgent;
            if (entity?.Code?.Path == null) return;
            if (!entity.Code.Path.Contains("huntingdog")) return;

            FieldInfo moveSpeedField = __instance.GetType()
                .GetField("moveSpeed", BindingFlags.Public | BindingFlags.Instance);
            if (moveSpeedField == null) return;

            // Default is 0.03f. Drop to 0.02f for huntingdog so the fetch
            // pace is between the tamed wolf and the huntingdog's current pace.
            moveSpeedField.SetValue(__instance, 0.02f);
        }
    }
}
