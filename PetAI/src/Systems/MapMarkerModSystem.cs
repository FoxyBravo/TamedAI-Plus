using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PetAI
{
    public class MapMarkerModSystem : ModSystem
    {
        public PetMapMarkerTracker Tracker { get; private set; }

        public override void Start(ICoreAPI api) { }

        public override void StartServerSide(ICoreServerAPI api)
        {
            var cfg = api.LoadModConfig<MapMarkerConfig>("petaimapmarkers.json") ?? new MapMarkerConfig();
            try
            {
                Tracker = new PetMapMarkerTracker(api, cfg);
                api.Logger.Notification("[PetMapMarker] tracker initialised");
            }
            catch (System.Exception e)
            {
                api.Logger.Error("[PetMapMarker] failed to initialise tracker: " + e);
            }
        }
    }
}
