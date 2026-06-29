using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SmartphoneFlippyBird.Data
{
    public enum AppIconType
    {
        Notification,
        AppStore,
        Camera,
        Photo,
        Setting,
        Calendar
    }

    public enum AppSize
    {
        Size1x1,
        Size2x1,
        Size2x2,
        Size2x3,
        Size2x4,
        Size4x2,
        Size4x3,
        Size4x4,
    }

    public interface IContactActionCardButton
    {
        public string Text { get; set; }
        public Color BackgroundColor { get; set; }
        public Color TextColor { get; set; }
        public Action<string>? OnClick { get; set; }
    }

    public interface ISmartPhoneApi
    {
        bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Action onClick,
            bool closePhoneOnLaunch = true,
            Rectangle? sourceRect = null,
            Func<int>? getBadgeCount = null,
            AppSize[]? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null,
            Dictionary<string, Texture2D>? themedIconTextures = null
        );

        bool UnregisterPhoneApp(string ownerModId, string appId);

        Texture2D? GetAppIconTexture(string appId);

        Texture2D? GetAppTexture(AppIconType appIconType);

        bool OpenPhoneHomeScreen();

        (int x, int y) GetPhonePosition();

        void SetPhonePosition(int x, int y);

        bool HandlePhoneAppBottomNavClick(int x, int y, int phoneX, int phoneY, Action? onBack = null);

        float GetPhoneUiScale();

        int GetPhoneFrameWidth();

        int GetPhoneFrameHeight();

        (int offsetX, int offsetY) GetPhoneContentOffset();

        Texture2D? GetPhoneFrameTexture();

        Texture2D? GetPhoneBackgroundTexture();

        void DrawPhoneSizeButtons(SpriteBatch b, int phoneX, int phoneY);

        bool HandlePhoneSizeButtonsClick(int x, int y, int phoneX, int phoneY);

        string GetDecreaseSizeKey();

        string GetIncreaseSizeKey();

        void AdjustPhoneSize(float amount);

        bool RegisterContactActionCard(string modId, string cardTitle, IList<IContactActionCardButton> buttons, List<string> npcNames = null);

        void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "");
    }
}
