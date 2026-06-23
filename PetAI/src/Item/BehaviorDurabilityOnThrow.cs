using System;
using System.Reflection;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace PetAI
{
    class BehaviorDurabilityOnThrow : CollectibleBehavior
    {
        ICoreAPI Api;

        public BehaviorDurabilityOnThrow(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            Api = api;
        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
        {
            if (!firstEvent) return;
            if (slot?.Itemstack == null || byEntity?.World == null) return;

            slot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, slot, 1);

            double dirX = Math.Sin(byEntity.Pos.Yaw);
            double dirZ = Math.Cos(byEntity.Pos.Yaw);
            Vec3d spawnPos = byEntity.Pos.XYZ
                .Add(0, byEntity.LocalEyePos.Y, 0)
                .Add(dirX * 3, 0.2, dirZ * 3);

            ItemStack taken = slot.TakeOut(1);
            if (taken == null) return;

            Entity entity = byEntity.World.SpawnItemEntity(taken, spawnPos);
            if (entity != null)
            {
                TryNotifyDogs(byEntity, entity, spawnPos);
            }

            handHandling = EnumHandHandling.PreventDefault;
            handling = EnumHandling.PreventDefault;
        }

        private void TryNotifyDogs(EntityAgent byEntity, Entity boneEntity, Vec3d throwPos)
        {
            try
            {
                var type = AccessTools.TypeByName("WolfTaming.ItemDogToy")
                        ?? AccessTools.TypeByName("Wolftaming.ItemDogToy")
                        ?? AccessTools.TypeByName("wolftaming.ItemDogToy");
                if (type == null) return;

                var method = type.GetMethod("NotifyDogs", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (method == null) return;

                var parms = method.GetParameters();
                object[] args = new object[parms.Length];
                object instance = method.IsStatic ? null : byEntity.World.GetItem(new AssetLocation("wolftaming:dogtoy"));
                if (!method.IsStatic && instance == null) return;

                for (int i = 0; i < parms.Length; i++)
                {
                    Type pt = parms[i].ParameterType;
                    if (pt == typeof(EntityAgent)) args[i] = byEntity;
                    else if (pt.IsAssignableFrom(typeof(EntityPlayer)) && byEntity is EntityPlayer ep) args[i] = ep;
                    else                     if (pt == typeof(ItemStack) && boneEntity is EntityItem ei) args[i] = ei.Itemstack;
                    else if (pt == typeof(Entity)) args[i] = boneEntity;
                    else if (pt.IsAssignableFrom(boneEntity?.GetType() ?? typeof(Entity))) args[i] = boneEntity;
                    else if (pt == typeof(Vec3d)) args[i] = throwPos;
                    else if (pt == typeof(IWorldAccessor)) args[i] = byEntity.World;
                    else if (pt == typeof(AssetLocation) && boneEntity is EntityItem ei2) args[i] = ei2.Itemstack?.Item?.Code;
                    else args[i] = null;
                }

                method.Invoke(instance, args);
            }
            catch (Exception ex)
            {
                Api?.Logger.Warning("petai: NotifyDogs reflection failed: {0}", ex.Message);
            }
        }
    }
}
