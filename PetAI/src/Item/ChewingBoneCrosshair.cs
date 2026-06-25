using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace TamedAIPlus
{
    /// <summary>
    /// Client-side crosshair renderer for the Chewing bone aim throw.
    /// Draws a white ring + center dot + cardinal tick marks at screen center
    /// while the local player is aiming. The texture is loaded from
    /// assets/tamedai-plus/textures/gui/chewingbone-crosshair.png.
    /// </summary>
    public class ChewingBoneCrosshair : IRenderer
    {
        private const string AimingAttrKey = "tamedaiplus:chewingbone-aiming";
        private static readonly AssetLocation TexturePath =
            new AssetLocation("tamedaiplus", "textures/gui/chewingbone-crosshair.png");

        private ICoreClientAPI capi;
        private LoadedTexture crosshairTexture;

        public double RenderOrder => 0.98;
        public int RenderRange => 9999;

        public ChewingBoneCrosshair(ICoreClientAPI capi)
        {
            this.capi = capi;
            crosshairTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(TexturePath, ref crosshairTexture);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi?.World?.Player?.Entity == null) return;
            if (crosshairTexture == null || crosshairTexture.TextureId == 0) return;
            if (capi.World.Player.Entity.Attributes.GetInt(AimingAttrKey) != 1) return;

            float scale = RuntimeEnv.GUIScale;
            float texW = crosshairTexture.Width;
            float texH = crosshairTexture.Height;
            float w = texW * scale;
            float h = texH * scale;

            capi.Render.Render2DTexture(
                crosshairTexture.TextureId,
                (capi.Render.FrameWidth / 2f) - (w / 2f),
                (capi.Render.FrameHeight / 2f) - (h / 2f),
                w, h,
                10000f
            );
        }

        public void Dispose()
        {
            crosshairTexture?.Dispose();
            crosshairTexture = null;
            capi = null;
        }
    }
}
