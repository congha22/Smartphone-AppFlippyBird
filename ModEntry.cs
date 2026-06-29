using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SmartphoneFlippyBird.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace SmartphoneFlippyBird
{
    internal sealed class ModEntry : Mod
    {
        private const string SmartphoneModId = "d5a1lamdtd.Smartphone";
        private ISmartPhoneApi? smartphoneApi;
        private Texture2D? bgTexture;
        private Texture2D? birdTexture;
        private Texture2D? pipeTexture;

        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.smartphoneApi = this.Helper.ModRegistry.GetApi<ISmartPhoneApi>(SmartphoneModId);
            if (this.smartphoneApi == null)
            {
                this.Monitor.Log("Smartphone API is unavailable; Flippy Bird app was not registered.", LogLevel.Warn);
                return;
            }

            try
            {
                this.bgTexture = this.Helper.ModContent.Load<Texture2D>("assets/background.png");
                this.birdTexture = this.Helper.ModContent.Load<Texture2D>("assets/bird.png");
                this.pipeTexture = this.Helper.ModContent.Load<Texture2D>("assets/pipe.png");
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Failed loading Flippy Bird assets: {ex.Message}", LogLevel.Error);
            }

            this.RegisterFlippyBirdApp();
        }

        private void RegisterFlippyBirdApp()
        {
            if (this.smartphoneApi == null)
                return;

            // Use the bird texture as the app icon
            Texture2D? appIcon = this.birdTexture;
            if (appIcon == null)
            {
                appIcon = this.smartphoneApi.GetAppTexture(AppIconType.Calendar);
            }
            if (appIcon == null)
            {
                // Fallback: draw a basic 84x84 color texture
                appIcon = new Texture2D(Game1.graphics.GraphicsDevice, 84, 84);
                Color[] data = new Color[84 * 84];
                for (int i = 0; i < data.Length; i++) data[i] = Color.Orange;
                appIcon.SetData(data);
            }

            bool appRegistered = this.smartphoneApi.RegisterPhoneApp(
                ownerModId: this.ModManifest.UniqueID,
                appId: "flippy_bird",
                displayName: "Flippy Bird",
                onClick: this.OpenFlippyBirdGame,
                closePhoneOnLaunch: true,
                sourceRect: null,
                getBadgeCount: null,
                supportedSizes: Array.Empty<AppSize>(),
                onDrawWidget: null,
                themedIconTextures: new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
                {
                    { "default", appIcon }
                });

            if (!appRegistered)
            {
                this.Monitor.Log("Failed to register Flippy Bird app.", LogLevel.Warn);
            }
        }

        private void OpenFlippyBirdGame()
        {
            if (!Context.IsWorldReady || this.smartphoneApi == null)
                return;

            Game1.activeClickableMenu = new FlippyBirdGameScreen(
                this.smartphoneApi,
                () => this.smartphoneApi.OpenPhoneHomeScreen(),
                this.bgTexture,
                this.birdTexture,
                this.pipeTexture);
        }
    }
}
