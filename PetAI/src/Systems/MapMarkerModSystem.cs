using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace TamedAIPlus
{
    public class MapMarkerModSystem : ModSystem
    {
        public TamedAIPlusMapMarkerTracker Tracker { get; private set; }

        public override void Start(ICoreAPI api) { }

        public override void StartServerSide(ICoreServerAPI api)
        {
            var cfg = api.LoadModConfig<MapMarkerConfig>("tamedaiplus-mapmarkers.json") ?? new MapMarkerConfig();
            try
            {
                Tracker = new TamedAIPlusMapMarkerTracker(api, cfg);
                api.Logger.Notification("[TamedAIPlusMapMarker] tracker initialised");
            }
            catch (System.Exception e)
            {
                api.Logger.Error("[TamedAIPlusMapMarker] failed to initialise tracker: " + e);
            }
        }
    }
}
