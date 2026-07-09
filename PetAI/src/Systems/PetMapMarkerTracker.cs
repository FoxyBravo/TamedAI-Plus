using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace TamedAIPlus
{
    public class TamedAIPlusMapMarkerTracker
    {
        public readonly int IntervalMs;

        public IReadOnlySet<long> WatchedIds => watchers;
        public IReadOnlySet<long> TameCandidateIds => tameCandidates;

        private readonly ICoreServerAPI sapi;
        private readonly Dictionary<long, TrackedPet> tracked = new Dictionary<long, TrackedPet>();
        private readonly HashSet<string> dirtyOwners = new HashSet<string>();
        private readonly HashSet<long> tameCandidates = new HashSet<long>();
        private WaypointMapLayer layer = null;
        private readonly HashSet<long> watchers = new HashSet<long>();

        private const int OwnedCreaturesGroupId = 1;
        private const string OwnedCreaturesGroupName = "Owned creatures";

        private readonly Queue<PetMemento> pendingGrowups = new Queue<PetMemento>();
        private readonly Dictionary<long, PetMemento> growupByEntityId = new Dictionary<long, PetMemento>();
        private readonly Dictionary<long, PetMemento> caged = new Dictionary<long, PetMemento>();

        private readonly MapMarkerConfig cfg;

        private const string originalIdKey = "petmarker:originalId";

        public TamedAIPlusMapMarkerTracker(ICoreServerAPI sapi, MapMarkerConfig cfg)
        {
            this.sapi = sapi;
            this.cfg = cfg;
            IntervalMs = (int)(cfg.UpdateIntervalSeconds * 1000);

            sapi.Event.OnEntityLoaded += OnEntityLoaded;
            sapi.Event.OnEntitySpawn += OnEntitySpawn;
            sapi.Event.OnEntityDespawn += OnEntityDespawn;
            sapi.Event.PlayerJoin += OnPlayerJoin;

            sapi.Event.RegisterGameTickListener(OnTick, IntervalMs);
            LogNotification("TamedAIPlusMapMarker started; tracking " + IntervalMs + "ms ticks");

            if (cfg.FullScanMinutes > 0)
            {
                var fullMs = cfg.FullScanMinutes * 60 * 1000;
                sapi.Event.RegisterGameTickListener(OnFullScanTick, fullMs);
                LogNotification(
                    $"Full-scan registered every {cfg.FullScanMinutes} minutes ({fullMs}ms)"
                );
            }
        }

        private void OnTick(float dt)
        {
            try
            {
                layer = layer ?? WaypointUtil.GetWaypointLayer(sapi);

                foreach (var tamedId in tameCandidates.ToArray())
                {
                    if (sapi.World.LoadedEntities.TryGetValue(tamedId, out var entity) && entity != null)
                    {
                        if (TryAddPetFromEntity(entity))
                            tameCandidates.Remove(tamedId);
                    }
                }

                foreach (var pet in tracked.Values)
                {
                    if (sapi.World.LoadedEntities.TryGetValue(pet.EntityId, out var entity) && entity != null)
                    {
                        pet.IsLoaded = true;
                        bool isDowned = IsDowned(entity);

                        if (pet.WasDowned && !isDowned)
                            HandlePetHealed(pet);
                        else if (!pet.WasDowned && isDowned)
                            HandlePetDowned(pet);
                        pet.WasDowned = isDowned;

                        pet.LastKnownPosition = entity.Pos.XYZ;
                        pet.Name = entity.GetName();
                    }
                    else
                    {
                        if (pet.IsLoaded)
                            LogError(
                                $"Pet id={pet.EntityId} not found in loaded entities; did OnEntityDespawn not fire?!"
                            );
                        pet.IsLoaded = false;
                    }
                }

                if (layer != null)
                {
                    SyncWaypoints();
                    WaypointUtil.ResendWaypointsToAll(layer, sapi, dirtyOwners);
                    dirtyOwners.Clear();
                }
            }
            catch (Exception e)
            {
                LogError($"exception in TamedAIPlusMapMarkerTracker OnTick: {e}");
            }
        }

        private void OnFullScanTick(float dt)
        {
            try
            {
                foreach (var entity in sapi.World.LoadedEntities.Values)
                {
                    if (entity == null) continue;
                    TryAddPetFromEntity(entity);
                }
            }
            catch (Exception e)
            {
                LogError($"exception in OnFullScanTick: {e}");
            }
        }

        private void OnEntityLoaded(Entity entity)
        {
            try
            {
                TryAddPetFromEntity(entity);
                RegisterDomesticationWatcher(entity);
            }
            catch (Exception e)
            {
                LogError($"exception in OnEntityLoaded: {e}");
            }
        }

        private void OnEntitySpawn(Entity entity)
        {
            try
            {
                if (pendingGrowups.Count > 0 && entity.HasBehavior<EntityBehaviorTameable>())
                    growupByEntityId[entity.EntityId] = pendingGrowups.Dequeue();

                TryAddPetFromEntity(entity);
                RegisterDomesticationWatcher(entity);
            }
            catch (Exception e)
            {
                LogError($"exception in OnEntitySpawn: {e}");
            }
        }

        private void OnEntityDespawn(Entity entity, EntityDespawnData data)
        {
            try
            {
                if (!tracked.TryGetValue(entity.EntityId, out var pet))
                    return;

                LogNotification($"Despawned pet id={entity.EntityId} name={pet.Name} reason={data.Reason}");

                if (data.Reason is EnumDespawnReason.OutOfRange or EnumDespawnReason.Unload or EnumDespawnReason.Disconnect)
                {
                    pet.IsLoaded = false;
                }

                if (data.Reason == EnumDespawnReason.PickedUp)
                    HandlePetCaged(pet, entity);

                if (data.Reason == EnumDespawnReason.Expire)
                {
                    if (entity.GetBehavior<EntityBehaviorGrow>() != null)
                        PrepareGrowupMemento(pet);
                    tracked.Remove(entity.EntityId);
                    LogNotification($"Expired - forget pet");
                }
            }
            catch (Exception e)
            {
                LogError($"exception in OnEntityDespawn: {e}");
            }
        }

        private void OnPlayerJoin(IServerPlayer byPlayer)
        {
            EnsureOwnedCreaturesGroup(byPlayer);
            if (layer != null)
                WaypointUtil.ResendWaypoints(layer, byPlayer);
        }

        private void EnsureOwnedCreaturesGroup(IServerPlayer player)
        {
            if (player == null) return;
            var memberships = player.ServerData.PlayerGroupMemberships;
            if (!memberships.ContainsKey(OwnedCreaturesGroupId))
            {
                memberships[OwnedCreaturesGroupId] = new PlayerGroupMembership
                {
                    GroupName = OwnedCreaturesGroupName,
                    GroupUid = OwnedCreaturesGroupId,
                    Level = EnumPlayerGroupMemberShip.Member
                };
            }
        }

        private void RegisterDomesticationWatcher(Entity entity)
        {
            if (!entity.HasBehavior<EntityBehaviorTameable>())
                return;

            var title = $"{entity.EntityId} {entity.Code.Path} {entity.GetName()}";

            if (!watchers.Add(entity.EntityId))
                return;

            LogNotification($"Watching over tameable {title}");

            const string domesticationStatusKey = "domesticationstatus";
            var watched = entity.WatchedAttributes;
            watched.RegisterModifiedListener(
                domesticationStatusKey,
                () =>
                {
                    try
                    {
                        bool alreadyTracked = tracked.ContainsKey(entity.EntityId);
                        bool isCandidate = tameCandidates.Contains(entity.EntityId);
                        var tameable = entity.GetBehavior<EntityBehaviorTameable>();
                        if (alreadyTracked && string.IsNullOrEmpty(tameable?.OwnerId))
                        {
                            LogNotification($"Tracked pet {title} lost owner - forget pet");
                            if (tameable == null)
                                LogError("Domestication status was removed - but this should never happen with TamedAI-Plus?!");
                            var pet = tracked[entity.EntityId];
                            bool r = tracked.Remove(entity.EntityId);
                            if (!r)
                                LogError("Attempted to remove pet that was not tracked?!");
                            if (layer != null)
                            {
                                var wp = WaypointUtil.FindWaypointByGuid(layer, pet.WaypointUid);
                                if (wp != null)
                                {
                                    layer.Waypoints.Remove(wp);
                                    dirtyOwners.Add(pet.OwnerUid);
                                }
                                else
                                    LogError("Waypoint not found in domesticationstatus listener");
                            }
                            else
                                LogError("Waypoint layer is null in domesticationstatus listener");
                            return;
                        }
                        if (!alreadyTracked && !isCandidate && tameable != null)
                        {
                            LogNotification($"Pet {title} not tracked yet - mark to track");
                            tameCandidates.Add(entity.EntityId);
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"exception in domesticationstatus listener for entity {entity.EntityId}: {e}");
                    }
                }
            );
        }

        private bool TryAddPetFromEntity(Entity entity)
        {
            var tameable = entity.GetBehavior<EntityBehaviorTameable>();
            if (tameable == null) return false;
            if (tameable.DomesticationLevel == DomesticationLevel.WILD) return false;

            bool isTaming = tameable.DomesticationLevel == DomesticationLevel.TAMING;
            if (isTaming && !cfg.TrackTamingPets) return false;

            string owner = tameable.OwnerId;
            if (string.IsNullOrEmpty(owner)) return false;

            string petName = entity.GetName();
            bool isDowned = IsDowned(entity);

            if (!tracked.TryGetValue(entity.EntityId, out var pet))
            {
                pet = new TrackedPet()
                {
                    EntityId = entity.EntityId,
                    OwnerUid = owner,
                    WaypointUid = WaypointUidFor(entity),
                    Name = petName,
                    WasDowned = isDowned,
                    IsLoaded = true,
                    LastKnownPosition = entity.Pos.XYZ,
                };

                long origId = entity.WatchedAttributes.GetLong(originalIdKey, 0);
                if (growupByEntityId.TryGetValue(entity.EntityId, out var memento))
                {
                    growupByEntityId.Remove(entity.EntityId);
                    pet.SavedColor = memento.SavedColor;
                    pet.SavedPinned = memento.SavedPinned;
                    LogNotification($"Tracking grown/swapped pet id={pet.EntityId} creature='{entity.Code.Path}' - new guid={pet.WaypointUid}");
                }
                else if (origId != 0 && caged.TryGetValue(origId, out var cagingMemento))
                {
                    caged.Remove(origId);
                    pet.SavedColor = cagingMemento.SavedColor;
                    pet.SavedPinned = cagingMemento.SavedPinned;
                    if (layer != null)
                    {
                        var oldWp = WaypointUtil.FindWaypointByGuid(layer, cagingMemento.OldWaypointGuid);
                        if (oldWp != null)
                        {
                            layer.Waypoints.Remove(oldWp);
                            dirtyOwners.Add(owner);
                        }
                    }
                    LogNotification($"Released-from-cage pet id={pet.EntityId} name='{pet.Name}' - new guid={pet.WaypointUid}");
                }
                else
                {
                    LogNotification($"Tracking new pet id={pet.EntityId} creature='{entity.Code.Path}' owner='{pet.OwnerUid}' name='{pet.Name}' down={pet.WasDowned}");
                }

                entity.WatchedAttributes.SetLong(originalIdKey, entity.EntityId);
                tracked[entity.EntityId] = pet;

                var ownerPlayer = sapi.World.PlayerByUid(owner) as IServerPlayer;
                if (ownerPlayer != null)
                    EnsureOwnedCreaturesGroup(ownerPlayer);
            }
            else
            {
                pet.IsLoaded = true;
                if (pet.WasDowned && !isDowned)
                    HandlePetHealed(pet);
                else if (!pet.WasDowned && isDowned)
                    HandlePetDowned(pet);
                pet.WasDowned = isDowned;
            }
            return true;
        }

        private void PrepareGrowupMemento(TrackedPet pet)
        {
            int? savedColor = null;
            bool? savedPinned = null;
            if (layer != null)
            {
                var wp = WaypointUtil.FindWaypointByGuid(layer, pet.WaypointUid);
                if (wp != null)
                {
                    savedColor = pet.SavedColor ?? wp.Color;
                    savedPinned = pet.SavedPinned ?? wp.Pinned;
                    layer.Waypoints.Remove(wp);
                    dirtyOwners.Add(pet.OwnerUid);
                    LogNotification($"Removed old waypoint guid={pet.WaypointUid} for growing pet");
                }
            }
            var memento = new PetMemento
            {
                SavedColor = savedColor,
                SavedPinned = savedPinned,
                OldWaypointGuid = pet.WaypointUid,
            };
            long adultId = FindGrowupAdultInCandidates(pet.OwnerUid);
            if (adultId != 0)
            {
                growupByEntityId[adultId] = memento;
                LogNotification($"Associated grow-up memento with already-spawned adult id={adultId} for pet '{pet.Name}'");
            }
            else
            {
                pendingGrowups.Enqueue(memento);
                LogNotification($"Queued grow-up memento for pet id={pet.EntityId} name='{pet.Name}'");
            }
        }

        private long FindGrowupAdultInCandidates(string ownerUid)
        {
            foreach (var candidateId in tameCandidates)
            {
                if (sapi.World.LoadedEntities.TryGetValue(candidateId, out var candidate))
                {
                    var status = candidate.GetBehavior<EntityBehaviorTameable>();
                    if (status?.OwnerId == ownerUid)
                        return candidateId;
                }
            }
            return 0;
        }

        private void HandlePetCaged(TrackedPet pet, Entity entity)
        {
            int restorationColor = MapMarkerConfig.ColorStringToArgb(cfg.DefaultColor);
            bool restorationPinned = false;
            if (layer != null)
            {
                var wp = WaypointUtil.FindWaypointByGuid(layer, pet.WaypointUid);
                if (wp != null)
                {
                    restorationColor = pet.SavedColor ?? wp.Color;
                    restorationPinned = pet.SavedPinned ?? wp.Pinned;
                    wp.Color = 0;
                    wp.Pinned = false;
                    dirtyOwners.Add(pet.OwnerUid);
                }
                else
                    LogError($"Waypoint not found when caging pet id={pet.EntityId} name='{pet.Name}'");
            }
            else
                LogError("Waypoint layer is null in HandlePetCaged");

            var memento = new PetMemento
            {
                OldWaypointGuid = pet.WaypointUid,
                SavedColor = restorationColor,
                SavedPinned = restorationPinned,
            };
            caged[entity.EntityId] = memento;
            tracked.Remove(pet.EntityId);
            LogNotification($"Pet caged id={pet.EntityId} code='{entity.Code.Path}' name='{pet.Name}' waypoint dimmed");
        }

        private void HandlePetDowned(TrackedPet pet)
        {
            try
            {
                var lyr = WaypointUtil.GetWaypointLayer(sapi);
                var wp = WaypointUtil.FindWaypointByGuid(lyr, pet.WaypointUid);
                if (wp != null)
                {
                    pet.SavedColor = wp.Color;
                    pet.SavedPinned = wp.Pinned;
                    wp.Color = MapMarkerConfig.ColorStringToArgb(cfg.DownedColor);
                    wp.Pinned = true;
                    dirtyOwners.Add(pet.OwnerUid);
                }
                LogNotification($"Pet downed id={pet.EntityId} name='{pet.Name}' owner={pet.OwnerUid}");
            }
            catch (Exception e)
            {
                LogError($"exception in HandlePetDowned: {e}");
            }
        }

        private void HandlePetHealed(TrackedPet pet)
        {
            try
            {
                var lyr = WaypointUtil.GetWaypointLayer(sapi);
                var wp = WaypointUtil.FindWaypointByGuid(lyr, pet.WaypointUid);
                if (wp != null)
                {
                    wp.Color = pet.SavedColor ?? MapMarkerConfig.ColorStringToArgb(cfg.DefaultColor);
                    wp.Pinned = pet.SavedPinned ?? false;
                    pet.SavedColor = null;
                    pet.SavedPinned = null;
                    dirtyOwners.Add(pet.OwnerUid);
                }
                LogNotification($"Pet healed id={pet.EntityId} name='{pet.Name}' owner={pet.OwnerUid}");
            }
            catch (Exception e)
            {
                LogError($"exception in HandlePetHealed: {e}");
            }
        }

        private void SyncWaypoints()
        {
            if (layer == null) return;

            foreach (var pet in tracked.Values)
            {
                if (!pet.IsLoaded) continue;

                var wp = WaypointUtil.FindWaypointByGuid(layer, pet.WaypointUid);
                var pos = pet.LastKnownPosition ?? new Vec3d(0, 0, 0);
                var title = pet.Name;

                if (wp == null)
                {
                    var color = pet.SavedColor ?? MapMarkerConfig.ColorStringToArgb(cfg.DefaultColor);
                    var pinned = pet.SavedPinned ?? false;
                    if (pet.WasDowned)
                    {
                        color = MapMarkerConfig.ColorStringToArgb(cfg.DownedColor);
                        pinned = true;
                    }
                    else
                    {
                        pet.SavedColor = null;
                        pet.SavedPinned = null;
                    }

                    var newWp = new Waypoint()
                    {
                        Guid = pet.WaypointUid,
                        Title = title,
                        Position = pos,
                        Icon = cfg.DefaultIcon,
                        Pinned = pinned,
                        OwningPlayerUid = pet.OwnerUid,
                        OwningPlayerGroupId = OwnedCreaturesGroupId,
                        Color = color,
                    };

                    layer.Waypoints.Add(newWp);
                    LogNotification($"Created waypoint for {pet.Name} (guid={newWp.Guid})");
                }
                else
                {
                    if (wp.Position != pos)
                    {
                        wp.Position = pos;
                        dirtyOwners.Add(pet.OwnerUid);
                    }
                    if (wp.Title != title)
                    {
                        wp.Title = title;
                        dirtyOwners.Add(pet.OwnerUid);
                    }
                }
            }
        }

        private void LogNotification(string message)
        {
            sapi.World.Logger.Notification("[TamedAIPlusMapMarker] " + message);
        }

        private void LogError(string message)
        {
            sapi.World.Logger.Error("[TamedAIPlusMapMarker][ERROR] " + message);
        }

        public static bool IsDowned(Entity entity)
        {
            var b = entity.GetBehavior<EntityBehaviorMortallyWoundable>();
            if (b == null) return false;
            return b.HealthState != EnumEntityHealthState.Normal;
        }

        public static string WaypointUidFor(long id)
        {
            return $"petmarker-{id}";
        }

        public static string WaypointUidFor(Entity entity)
        {
            return WaypointUidFor(entity.EntityId);
        }

        public IEnumerable<Entity> GetTrackedEntities()
        {
            foreach (var pet in tracked.Values)
            {
                if (sapi.World.LoadedEntities.TryGetValue(pet.EntityId, out var entity) && entity != null)
                    yield return entity;
            }
        }
    }

    public class TrackedPet
    {
        public long EntityId;
        public string OwnerUid;
        public string Name;
        public string WaypointUid;
        public bool WasDowned;
        public bool IsLoaded;
        public int? SavedColor;
        public bool? SavedPinned;
        public Vec3d LastKnownPosition;
    }

    public class PetMemento
    {
        public int? SavedColor;
        public bool? SavedPinned;
        public string OldWaypointGuid;
    }
}
