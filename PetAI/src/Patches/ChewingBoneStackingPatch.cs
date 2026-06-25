using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;

namespace TamedAIPlus
{
    /// <summary>
    /// Stacking rule for the chewing bone (wolftaming:dogtoy).
    ///
    /// Only undamaged bones (durability still at the max value) can stack.
    /// Any bone that has been used — even once — becomes a singleton and
    /// never merges with other bones in the player's inventory, chests,
    /// vessels, or auto-pickup. Implemented as a Harmony prefix on
    /// ItemSlot.TryPutInto: when both source and sink are chewing bones
    /// and either side has been used, we set quantity to 0 and skip the
    /// original method, which prevents the stack merge.
    ///
    /// Note: VS's GetStackableItems on Collectible is not virtual in this
    /// API version, so a class override isn't possible. The patch on
    /// ItemSlot.TryPutInto is the cleanest working alternative.
    /// </summary>
    public class ChewingBoneStackingPatch
    {
        public static void Patch(Harmony harmony)
        {
            MethodInfo target = typeof(ItemSlot).GetMethod("TryPutInto",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(ItemSlot), typeof(int).MakeByRefType() },
                null);

            if (target == null) return;

            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(ChewingBoneStackingPatch), nameof(Prefix)));
        }

        public static bool Prefix(ItemSlot __instance, ItemSlot sinkSlot, ref int quantity)
        {
            // Only intervene when both slots actually have a chewing bone.
            // An empty sink or a non-bone sink falls through to vanilla
            // behavior (item moves normally, no stacking attempted).
            if (__instance?.Itemstack == null || sinkSlot?.Itemstack == null) return true;
            if (quantity <= 0) return true;

            string sourceCode = __instance.Itemstack.Collectible?.Code?.Path;
            string sinkCode = sinkSlot.Itemstack.Collectible?.Code?.Path;
            if (sourceCode != "dogtoy" || sinkCode != "dogtoy") return true;

            int sourceMax = __instance.Itemstack.Collectible.Durability;
            int sourceCur = __instance.Itemstack.Attributes.GetInt("durability", sourceMax);
            int sinkMax = sinkSlot.Itemstack.Collectible.Durability;
            int sinkCur = sinkSlot.Itemstack.Attributes.GetInt("durability", sinkMax);

            if (sourceCur < sourceMax || sinkCur < sinkMax)
            {
                quantity = 0;
                return false;
            }

            return true;
        }
    }
}
