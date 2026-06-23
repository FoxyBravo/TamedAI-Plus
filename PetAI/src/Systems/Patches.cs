using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;

namespace PetAI
{
    public class MultiplyPatch
    {

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(MethodInfo()
                , prefix: new HarmonyMethod(typeof(MultiplyPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(MethodInfo()
                , HarmonyPatchType.Prefix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            return typeof(EntityBehaviorMultiply).GetMethod("TryGetPregnant", BindingFlags.Instance | BindingFlags.NonPublic);
        }
        public static bool Prefix(ref bool __result, EntityBehaviorMultiply __instance)
        {
            bool? multiplyAllowed = __instance.entity.GetBehavior<EntityBehaviorTameable>()?.MultiplyAllowed;
            if (multiplyAllowed == null || multiplyAllowed == true)
            {
                return true;
            }
            else
            {
                __result = false;
                return false;
            }
        }
    }

    public class MortallyWoundableOnEntityReceiveDamagePatch
    {

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(MethodInfo()
                , prefix: new HarmonyMethod(typeof(MortallyWoundableOnEntityReceiveDamagePatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(MethodInfo()
                , HarmonyPatchType.Prefix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            return typeof(EntityBehaviorMortallyWoundable).GetMethod("OnEntityReceiveDamage", BindingFlags.Instance | BindingFlags.Public);
        }
        public static bool Prefix(EntityBehaviorMortallyWoundable __instance, DamageSource damageSource, ref float damage)
        {
            var tameable = __instance.entity.GetBehavior<EntityBehaviorTameable>();
            if (tameable != null && string.IsNullOrEmpty(tameable.OwnerId))
            {
                return false;
            }
            if (tameable == null
                || __instance.HealthState == EnumEntityHealthState.Normal
                || damageSource.Type == EnumDamageType.Heal)
            {
                return true;
            }
            damage = 0;
            return false;
        }
    }

    public class EntityBehaviorNameTagGetNamePatch
    {

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(MethodInfo()
                , postfix: new HarmonyMethod(typeof(EntityBehaviorNameTagGetNamePatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(MethodInfo()
                , HarmonyPatchType.Postfix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            return typeof(EntityBehaviorNameTag).GetMethod("GetName", BindingFlags.Instance | BindingFlags.Public);
        }
        public static void Postfix(ref string __result)
        {
            if (string.IsNullOrWhiteSpace(__result))
            {
                __result = null;
            }
        }
    }

    public class EntityBehaviorGrowBecomeAdultPatch
    {

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(MethodInfo()
                , postfix: new HarmonyMethod(typeof(EntityBehaviorGrowBecomeAdultPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(MethodInfo()
                , HarmonyPatchType.Postfix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            return typeof(EntityBehaviorGrow).GetMethod("BecomeAdult", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void Postfix(EntityBehaviorGrow __instance, Entity adult) {
            Entity entity = __instance.entity;

            if (adult.HasBehavior<EntityBehaviorTameable>())
            {
                adult.GetBehavior<EntityBehaviorTameable>().DomesticationStatus = entity.GetBehavior<EntityBehaviorTameable>().DomesticationStatus;
            }
            adult.GetBehavior<EntityBehaviorNameTag>()?.SetName(entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName);
        }
    }

    public class EntityBehaviorHealthGetInfoTextPatch
    {
        public static void Patch(Harmony harmony)
        {
            harmony.Patch(MethodInfo()
                , prefix: new HarmonyMethod(typeof(EntityBehaviorHealthGetInfoTextPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(MethodInfo()
                , HarmonyPatchType.Prefix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            return typeof(EntityBehaviorHealth).GetMethod("GetInfoText", BindingFlags.Instance | BindingFlags.Public);
        }
        public static bool Prefix(EntityBehaviorHealth __instance)
        {
            return !__instance.entity.HasBehavior<EntityBehaviorTameable>();
        }
    }

    public class AiTaskStayCloseToEntityOnNoPathPatch
    {

        public static void Patch(Harmony harmony)
        {
            harmony.Patch(MethodInfo()
                , postfix: new HarmonyMethod(typeof(AiTaskStayCloseToEntityOnNoPathPatch).GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public)));
        }

        public static void Unpatch(Harmony harmony)
        {
            harmony.Unpatch(MethodInfo()
                , HarmonyPatchType.Postfix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            return typeof(AiTaskStayCloseToEntity).GetMethod("OnNoPath", BindingFlags.Instance | BindingFlags.Public, []);
        }
        public static void Postfix(AiTaskStayCloseToEntity __instance)
        {
            __instance.OnNoPath(null);
        }
    }

    public class ItemDogToyDurabilityPatch
    {
        private static long lastDecrementMs = 0;

        public static void Patch(Harmony harmony, ICoreAPI api)
        {
            MethodInfo method;
            try
            {
                method = MethodInfo();
            }
            catch (Exception ex)
            {
                api.Logger.Warning("petai: could not resolve ItemDogToy.OnHeldInteractStart ({0}); chewing bone will not lose durability on throw", ex.Message);
                return;
            }
            if (method == null)
            {
                api.Logger.Warning("petai: could not resolve ItemDogToy.OnHeldInteractStart; chewing bone will not lose durability on throw");
                return;
            }
            api.Logger.Notification("petai: chewing bone durability patch applied to {0}.{1}", method.DeclaringType.FullName, method.Name);
            try
            {
                harmony.Patch(method,
                    prefix: new HarmonyMethod(typeof(ItemDogToyDurabilityPatch).GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public)));
            }
            catch (Exception ex)
            {
                api.Logger.Error("petai: failed to apply chewing bone durability patch: {0}", ex.Message);
            }
        }

        public static void Unpatch(Harmony harmony)
        {
            var method = MethodInfo();
            if (method == null) return;
            harmony.Unpatch(method, HarmonyPatchType.Prefix, "gerste.petai");
        }

        public static MethodInfo MethodInfo()
        {
            var type = AccessTools.TypeByName("WolfTaming.ItemDogToy")
                    ?? AccessTools.TypeByName("Wolftaming.ItemDogToy")
                    ?? AccessTools.TypeByName("wolftaming.ItemDogToy");
            if (type == null) return null;
            return type.GetMethod("OnHeldInteractStart", BindingFlags.Instance | BindingFlags.Public);
        }

        public static void Prefix(ItemSlot itemslot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (itemslot?.Itemstack == null) return;
            if (byEntity?.World == null) return;

            long now = byEntity.World.ElapsedMilliseconds;
            if (now - lastDecrementMs < 100)
            {
                return;
            }
            lastDecrementMs = now;

            int max = itemslot.Itemstack.Collectible.Durability;
            int dur = itemslot.Itemstack.Attributes.GetInt("durability");
            int newDur = dur + 1;

            if (newDur >= max)
            {
                itemslot.Itemstack = null;
            }
            else
            {
                itemslot.Itemstack.Attributes.SetInt("durability", newDur);
            }
            itemslot.MarkDirty();
        }
    }
}
