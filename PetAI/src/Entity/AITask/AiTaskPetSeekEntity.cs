using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TamedAIPlus
{
    public class AiTaskPetSeekEntity : AiTaskSeekEntity
    {
        readonly bool isCommandable = false;

        long lastCheck;

        long lastSearch;

        private Vec3d stuckCheckPos;
        private long stuckCheckMs;
        private bool usingDetour;
        private int detourAttempts;
        private const int STUCK_CHECK_INTERVAL_MS = 1500;
        private const double STUCK_MIN_MOVE_SQ = 0.16;
        private const int DETOUR_SEARCH_RADIUS = 8;
        private const int DETOUR_STEP = 2;
        private const int MAX_DETOUR_ATTEMPTS = 3;

        private EntityBehaviorGiveCommand _behaviorGiveCommand;
        private long lastOwnerLookup;
        private EntityBehaviorGiveCommand BehaviorGiveCommand
        {
            get
            {
                if (_behaviorGiveCommand == null && lastOwnerLookup + 5000 < entity.World.ElapsedMilliseconds)
                {
                    lastOwnerLookup = entity.World.ElapsedMilliseconds;
                    _behaviorGiveCommand = entity.GetBehavior<EntityBehaviorTameable>()?.CachedOwner?.Entity?.GetBehavior<EntityBehaviorGiveCommand>();
                }
                return _behaviorGiveCommand;
            }
        }

        public AiTaskPetSeekEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
        {
            isCommandable = taskConfig["isCommandable"].AsBool(false);
            moveSpeed *= TamedAIPlusConfig.Current.Difficulty.petSpeedMultiplier;
            animMeta.AnimationSpeed *= TamedAIPlusConfig.Current.Difficulty.petSpeedMultiplier;
        }

        public override bool ShouldExecute()
        {
            var aggressionLevel = entity.GetBehavior<EntityBehaviorReceiveCommand>()?.AggressionLevel ?? EnumAggressionLevel.AGGRESSIVE;
            var elapsedMs = entity.World.ElapsedMilliseconds;
            if (lastCheck + 500 < elapsedMs)
            {
                NowSeekRange = getSeekRange();
                lastCheck = elapsedMs;
                if (aggressionLevel == EnumAggressionLevel.PASSIVE) { return false; }
                if (targetEntity?.Alive != true || !CanSense(targetEntity, NowSeekRange)) { targetEntity = null; }
                if (targetEntity == null)
                {
                    if (aggressionLevel != EnumAggressionLevel.NEUTRAL && isCommandable)
                    {
                        var ownerAttackedBy = BehaviorGiveCommand?.Attacker;
                        if (ownerAttackedBy?.Alive == true && CanSense(ownerAttackedBy, NowSeekRange))
                        {
                            targetEntity = ownerAttackedBy;
                        }

                        var ownerAttacks = BehaviorGiveCommand?.Victim;
                        if (ownerAttacks?.Alive == true && CanSense(ownerAttacks, NowSeekRange))
                        {
                            targetEntity = ownerAttacks;
                        }
                    }
                    if (attackedByEntity?.Alive == true && CanSense(attackedByEntity, NowSeekRange))
                    {
                        targetEntity = attackedByEntity;
                    }
                }

                if (CanSense(targetEntity, NowSeekRange))
                {
                    targetPos = targetEntity.Pos.XYZ;
                    return true;
                }
            }
            if (aggressionLevel == EnumAggressionLevel.AGGRESSIVE && lastSearch + 5000 < elapsedMs)
            {
                lastSearch = elapsedMs;
                targetEntity = partitionUtil.GetNearestInteractableEntity(entity.Pos.XYZ, NowSeekRange, e => IsTargetableEntity(e, NowSeekRange));

                if (IsTargetableEntity(targetEntity, NowSeekRange))
                {
                    targetPos = targetEntity.Pos.XYZ;
                    return true;
                }
            }
            return false;
        }

        public override bool IsTargetableEntity(Entity e, float range)
        {
            if (e == null) { return false; }
            var tameable = entity.GetBehavior<EntityBehaviorTameable>();
            if (e is EntityPlayer player)
            {
                if (player.PlayerUID == tameable?.OwnerId && tameable != null && tameable.Obedience > 0.5f)
                {
                    return false;
                }
                if (!TamedAIPlusConfig.Current.PvpOn && tameable?.DomesticationLevel != DomesticationLevel.WILD && player.PlayerUID != tameable?.OwnerId)
                {
                    return false;
                }
            }
            if (e.Pos.SquareDistanceTo(entity.Pos) > range * range) { return false; }


            return base.IsTargetableEntity(e, range);
        }

        public override bool CanSense(Entity e, double range)
        {
            return e != null
                && base.CanSense(e, range)
                && e.Pos.SquareDistanceTo(entity.Pos) <= range * range;
        }

        public override void StartExecute()
        {
            base.StartExecute();
            stuckCheckPos = entity.Pos.XYZ;
            stuckCheckMs = entity.World.ElapsedMilliseconds;
            usingDetour = false;
            detourAttempts = 0;
        }

        public override bool ContinueExecute(float dt)
        {
            if (!base.ContinueExecute(dt)) return false;
            if (targetEntity == null) return true;

            long now = entity.World.ElapsedMilliseconds;
            if (now - stuckCheckMs >= STUCK_CHECK_INTERVAL_MS)
            {
                double movedSq = entity.Pos.SquareDistanceTo(stuckCheckPos);
                stuckCheckPos = entity.Pos.XYZ;
                stuckCheckMs = now;

                if (movedSq < STUCK_MIN_MOVE_SQ && !usingDetour && pathTraverser.Active && detourAttempts < MAX_DETOUR_ATTEMPTS)
                {
                    Vec3d detour = FindDetourWaypoint();
                    if (detour != null)
                    {
                        usingDetour = true;
                        detourAttempts++;
                        pathTraverser.Stop();
                        pathTraverser.WalkTowards(detour, moveSpeed, NowSeekRange + 4, OnDetourReached, OnDetourStuck);
                    }
                }
            }
            return true;
        }

        private void OnGoalReached()
        {
            usingDetour = false;
        }

        private void OnStuck()
        {
            usingDetour = false;
        }

        private void OnDetourReached()
        {
            usingDetour = false;
            if (targetEntity != null && targetEntity.Alive)
            {
                pathTraverser.WalkTowards(targetEntity.Pos.XYZ, moveSpeed, NowSeekRange + 4, OnGoalReached, OnStuck);
            }
        }

        private void OnDetourStuck()
        {
            usingDetour = false;
        }

        private Vec3d FindDetourWaypoint()
        {
            Vec3d here = entity.Pos.XYZ;
            Vec3d there = targetEntity.Pos.XYZ;
            double dx = there.X - here.X;
            double dz = there.Z - here.Z;
            double horizDist = Math.Sqrt(dx * dx + dz * dz);
            if (horizDist < 0.5) return null;

            double ux = dx / horizDist;
            double uz = dz / horizDist;
            double px = -uz;
            double pz = ux;

            IBlockAccessor ba = entity.World.BlockAccessor;
            for (int r = DETOUR_STEP; r <= DETOUR_SEARCH_RADIUS; r += DETOUR_STEP)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    double cx = here.X + px * r * side + ux * r;
                    double cy = here.Y;
                    double cz = here.Z + pz * r * side + uz * r;
                    Vec3d candidate = new Vec3d(cx, cy, cz);
                    if (IsPathClear(ba, here, candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        private bool IsPathClear(IBlockAccessor ba, Vec3d from, Vec3d to)
        {
            double dist = from.DistanceTo(to);
            int steps = Math.Max(1, (int)(dist * 2));
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                int x = (int)Math.Floor(from.X + (to.X - from.X) * t);
                int y = (int)Math.Floor(from.Y + (to.Y - from.Y) * t);
                int z = (int)Math.Floor(from.Z + (to.Z - from.Z) * t);
                Block block = ba.GetBlock(new BlockPos(x, y, z));
                if (IsDangerous(block)) return false;
                if (block.Id != 0 && !block.IsLiquid() && !IsStepUp(ba, x, y, z)) return false;
            }
            return true;
        }

        private bool IsStepUp(IBlockAccessor ba, int x, int y, int z)
        {
            Block above = ba.GetBlock(new BlockPos(x, y + 1, z));
            return above.Id == 0 || above.IsLiquid();
        }

        private bool IsDangerous(Block block)
        {
            string code = block.Code?.Path?.ToLower() ?? "";
            if (code.Contains("lava") || code.Contains("fire") || code.Contains("magma")) return true;
            return false;
        }
    }
}
