using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SmartphoneFlippyBird.Data;
using StardewValley;
using StardewValley.Menus;

namespace SmartphoneFlippyBird
{
    public class FlippyBirdGameScreen : IClickableMenu
    {
        private readonly ISmartPhoneApi smartphoneApi;
        private readonly Action onBack;

        // Custom assets
        private readonly Texture2D? bgTexture;
        private readonly Texture2D? birdTexture;
        private readonly Texture2D? pipeTexture;

        // Layout bounds
        private int phoneFrameWidth;
        private int phoneFrameHeight;
        private int phoneContentOffsetX;
        private int phoneContentOffsetY;
        private float phoneUiScale;

        private Texture2D? phoneFrameTexture;
        private Texture2D? phoneBackgroundTexture;

        private int contentWidth;  // portrait content width (maps to landscape height)
        private int contentHeight; // portrait content height (maps to landscape width)

        // Game State
        private enum GameState
        {
            Start,
            Playing,
            GameOver
        }

        private GameState state = GameState.Start;

        // Virtual coordinate system for physics and tiling consistency
        private const float VirtualWidth = 480f;
        private const float VirtualHeight = 270f;
        private const float GroundY = 230f; // ground boundary starts at Y = 230

        // Bird State
        private float birdX;
        private float birdY;
        private float birdVelocity;
        private float birdRotation;
        private const float Gravity = 0.32f;
        private const float FlapStrength = -5.25f;
        private const float BirdRadius = 12f;

        // Pipes State
        private struct Pipe
        {
            public float X;
            public float GapY;
            public float GapHeight;
            public float Width;
            public bool Passed;
        }

        private readonly List<Pipe> pipes = new();
        private int pipeSpawnTimer = 0;
        private const int PipeSpawnInterval = 100; // spawn pipe every 100 frames
        private const float PipeSpeed = 2.0f;
        private const float PipeWidth = 52f;
        private const float PipeGapHeight = 80f;

        // Background scrolling offset
        private float bgOffset = 0f;

        // Score State
        private int score = 0;
        private int highScore = 0;
        private int restartCooldown = 0;

        // Drag State
        private bool isDragging;
        private int dragOffsetX;
        private int dragOffsetY;

        // Animation Timer
        private float bounceTimer = 0f;

        public FlippyBirdGameScreen(ISmartPhoneApi api, Action onBack, Texture2D? bgTex, Texture2D? birdTex, Texture2D? pipeTex)
            : base()
        {
            this.smartphoneApi = api;
            this.onBack = onBack;
            this.bgTexture = bgTex;
            this.birdTexture = birdTex;
            this.pipeTexture = pipeTex;

            // Retrieve phone positioning and dimensions
            var (px, py) = api.GetPhonePosition();
            this.phoneFrameWidth = api.GetPhoneFrameWidth();
            this.phoneFrameHeight = api.GetPhoneFrameHeight();

            // Center-rotate the position from portrait to landscape
            this.xPositionOnScreen = px + (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
            this.yPositionOnScreen = py + (this.phoneFrameHeight - this.phoneFrameWidth) / 2;

            var (offX, offY) = api.GetPhoneContentOffset();
            this.phoneContentOffsetX = offX;
            this.phoneContentOffsetY = offY;
            this.phoneUiScale = api.GetPhoneUiScale();
            this.phoneFrameTexture = api.GetPhoneFrameTexture();
            this.phoneBackgroundTexture = api.GetPhoneBackgroundTexture();

            // In landscape, visual width is portrait height, and visual height is portrait width
            this.width = this.phoneFrameHeight;
            this.height = this.phoneFrameWidth;

            if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
            {
                this.contentWidth = (int)Math.Round(this.phoneBackgroundTexture.Width * this.phoneUiScale);
                this.contentHeight = (int)Math.Round(this.phoneBackgroundTexture.Height * this.phoneUiScale);
            }
            else
            {
                this.contentWidth = Math.Max(1, this.phoneFrameWidth - (this.phoneContentOffsetX * 2));
                this.contentHeight = Math.Max(1, this.phoneFrameHeight - this.phoneContentOffsetY - scaleVal(80));
            }

            // Load high score from player modData
            if (Game1.player != null && Game1.player.modData.TryGetValue("d5a1lamdtd.Smartphone-FlippyBird.HighScore", out string scoreStr) && int.TryParse(scoreStr, out int val))
            {
                this.highScore = val;
            }
            else
            {
                this.highScore = 0;
            }

            // Align bird initially at 25% of virtual width, vertically centered
            this.birdX = VirtualWidth * 0.25f;
            this.birdY = GroundY / 2f;
        }

        private int scaleVal(int val)
        {
            return (int)Math.Round(val * this.phoneUiScale);
        }

        private Rectangle PortraitToLandscape(Rectangle rect)
        {
            return new Rectangle(
                this.xPositionOnScreen + rect.Y,
                this.yPositionOnScreen + this.phoneFrameWidth - rect.X - rect.Width,
                rect.Height,
                rect.Width
            );
        }

        private void UpdateScaleAndDimensions()
        {
            float currentScale = this.smartphoneApi.GetPhoneUiScale();
            if (Math.Abs(currentScale - this.phoneUiScale) > 0.001f)
            {
                // Capture the current landscape center before updating dimensions
                int oldH = this.phoneFrameHeight;
                int oldW = this.phoneFrameWidth;
                int centerX = this.xPositionOnScreen + oldH / 2;
                int centerY = this.yPositionOnScreen + oldW / 2;

                this.phoneUiScale = currentScale;
                this.phoneFrameWidth = this.smartphoneApi.GetPhoneFrameWidth();
                this.phoneFrameHeight = this.smartphoneApi.GetPhoneFrameHeight();
                var (offX, offY) = this.smartphoneApi.GetPhoneContentOffset();
                this.phoneContentOffsetX = offX;
                this.phoneContentOffsetY = offY;
                this.phoneFrameTexture = this.smartphoneApi.GetPhoneFrameTexture();
                this.phoneBackgroundTexture = this.smartphoneApi.GetPhoneBackgroundTexture();

                this.width = this.phoneFrameHeight;
                this.height = this.phoneFrameWidth;

                // Position the new landscape frame around the same center point
                this.xPositionOnScreen = centerX - this.phoneFrameHeight / 2;
                this.yPositionOnScreen = centerY - this.phoneFrameWidth / 2;

                // Sync the center-rotated portrait position globally
                int px = this.xPositionOnScreen - (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
                int py = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth) / 2;
                this.smartphoneApi.SetPhonePosition(px, py);

                if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
                {
                    this.contentWidth = (int)Math.Round(this.phoneBackgroundTexture.Width * this.phoneUiScale);
                    this.contentHeight = (int)Math.Round(this.phoneBackgroundTexture.Height * this.phoneUiScale);
                }
                else
                {
                    this.contentWidth = Math.Max(1, this.phoneFrameWidth - (this.phoneContentOffsetX * 2));
                    this.contentHeight = Math.Max(1, this.phoneFrameHeight - this.phoneContentOffsetY - scaleVal(80));
                }
            }
        }

        public override void update(GameTime time)
        {
            UpdateScaleAndDimensions();
            base.update(time);

            if (this.isDragging)
            {
                this.xPositionOnScreen = Game1.getMouseX() - this.dragOffsetX;
                this.yPositionOnScreen = Game1.getMouseY() - this.dragOffsetY;
                
                int px = this.xPositionOnScreen - (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
                int py = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth) / 2;
                this.smartphoneApi.SetPhonePosition(px, py);
            }

            if (this.state == GameState.Start)
            {
                this.bounceTimer += (float)time.ElapsedGameTime.TotalSeconds;
                this.birdX = VirtualWidth * 0.25f;
                this.birdY = GroundY / 2f + (float)Math.Sin(this.bounceTimer * 4f) * 8f;
                this.birdRotation = 0f;
            }
            else if (this.state == GameState.Playing)
            {
                // Scroll background
                this.bgOffset = (this.bgOffset + 0.5f) % (this.bgTexture?.Width ?? 276);

                // Physics
                this.birdVelocity += Gravity;
                this.birdY += this.birdVelocity;

                // Bird rotation mapping matching main.js logic:
                // velocity <= 5.25: rotation = -15 degrees (pointing up)
                // velocity >= 7.25: rotation = 70 degrees (pointing down)
                // otherwise: 0 degrees
                float degree = (float)(Math.PI / 180.0);
                if (this.birdVelocity <= 5.25f)
                {
                    this.birdRotation = -15f * degree;
                }
                else if (this.birdVelocity >= 7.25f)
                {
                    this.birdRotation = 70f * degree;
                }
                else
                {
                    this.birdRotation = 0f;
                }

                // Move pipes
                for (int i = this.pipes.Count - 1; i >= 0; i--)
                {
                    var pipe = this.pipes[i];
                    pipe.X -= PipeSpeed;

                    // Score point on pass
                    if (!pipe.Passed && pipe.X + pipe.Width / 2f < this.birdX)
                    {
                        pipe.Passed = true;
                        this.score++;
                        Game1.playSound("coin");

                        if (this.score > this.highScore)
                        {
                            this.highScore = this.score;
                            if (Game1.player != null)
                            {
                                Game1.player.modData["d5a1lamdtd.Smartphone-FlippyBird.HighScore"] = this.highScore.ToString();
                            }
                        }
                    }

                    this.pipes[i] = pipe;

                    // Remove off-screen pipes
                    if (pipe.X + pipe.Width < 0)
                    {
                        this.pipes.RemoveAt(i);
                    }
                }

                // Spawn pipes
                this.pipeSpawnTimer--;
                if (this.pipeSpawnTimer <= 0)
                {
                    // For height 230 (groundY), gap 80, the top pipe bottom Y must reside comfortably within [30, 130]
                    float minTopBottomY = 30f;
                    float maxTopBottomY = 130f;
                    float topPipeBottom = minTopBottomY + (float)Game1.random.NextDouble() * (maxTopBottomY - minTopBottomY);

                    this.pipes.Add(new Pipe
                    {
                        X = VirtualWidth,
                        GapY = topPipeBottom + PipeGapHeight / 2f,
                        GapHeight = PipeGapHeight,
                        Width = PipeWidth,
                        Passed = false
                    });
                    this.pipeSpawnTimer = PipeSpawnInterval;
                }

                // Collisions
                bool gameOver = false;

                // Ground & Ceiling collision
                if (this.birdY + BirdRadius > GroundY)
                {
                    this.birdY = GroundY - BirdRadius;
                    gameOver = true;
                }
                else if (this.birdY - BirdRadius < 0)
                {
                    this.birdY = BirdRadius;
                }

                // Pipes collision
                foreach (var pipe in this.pipes)
                {
                    float topPipeBottom = pipe.GapY - pipe.GapHeight / 2f;
                    float bottomPipeTop = pipe.GapY + pipe.GapHeight / 2f;

                    if (this.birdX + BirdRadius > pipe.X && this.birdX - BirdRadius < pipe.X + pipe.Width)
                    {
                        if (this.birdY - BirdRadius < topPipeBottom || this.birdY + BirdRadius > bottomPipeTop)
                        {
                            gameOver = true;
                        }
                    }
                }

                if (gameOver)
                {
                    this.state = GameState.GameOver;
                    this.restartCooldown = 30; // 0.5s cooldown
                    Game1.playSound("fishEscape");
                }
            }
            else if (this.state == GameState.GameOver)
            {
                if (this.restartCooldown > 0)
                {
                    this.restartCooldown--;
                }
            }
        }

        private void TriggerFlap()
        {
            if (this.state == GameState.Start)
            {
                this.state = GameState.Playing;
                this.score = 0;
                this.pipes.Clear();
                this.pipeSpawnTimer = 0; // Spawn first pipe immediately
                this.birdVelocity = FlapStrength;
                this.bgOffset = 0f;
                Game1.playSound("grassyStep");
            }
            else if (this.state == GameState.Playing)
            {
                this.birdVelocity = FlapStrength;
                Game1.playSound("grassyStep");
            }
            else if (this.state == GameState.GameOver)
            {
                if (this.restartCooldown <= 0)
                {
                    this.state = GameState.Start;
                    this.bounceTimer = 0f;
                    this.birdX = VirtualWidth * 0.25f;
                    this.birdY = GroundY / 2f;
                    this.birdVelocity = 0f;
                    this.birdRotation = 0f;
                    this.pipes.Clear();
                    Game1.playSound("bigSelect");
                }
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                this.onBack?.Invoke();
                return;
            }

            string keyStr = key.ToString();
            if (keyStr == this.smartphoneApi.GetDecreaseSizeKey())
            {
                this.smartphoneApi.AdjustPhoneSize(-0.1f);
                return;
            }
            if (keyStr == this.smartphoneApi.GetIncreaseSizeKey())
            {
                this.smartphoneApi.AdjustPhoneSize(0.1f);
                return;
            }

            if (key == Keys.Space || key == Keys.Up || key == Keys.W)
            {
                TriggerFlap();
                return;
            }

            base.receiveKeyPress(key);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            int px = this.xPositionOnScreen - (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
            int py = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth) / 2;

            // Map click back to portrait coordinate offsets relative to the top-left for navigation buttons
            int px_click = px + (this.yPositionOnScreen + this.phoneFrameWidth - y);
            int py_click = py + (x - this.xPositionOnScreen);

            if (this.smartphoneApi.HandlePhoneAppBottomNavClick(px_click, py_click, px, py, onBack: this.onBack))
            {
                return;
            }

            if (this.smartphoneApi.HandlePhoneSizeButtonsClick(px_click, py_click, px, py))
            {
                return;
            }

            // Check if clicking in active landscape game region
            int landscapeContentX = this.xPositionOnScreen + this.phoneContentOffsetY;
            int landscapeContentY = this.yPositionOnScreen + this.phoneFrameWidth - this.phoneContentOffsetX - this.contentWidth;
            int LWidth = this.contentHeight;
            int LHeight = this.contentWidth;

            if (x >= landscapeContentX && x <= landscapeContentX + LWidth && y >= landscapeContentY && y <= landscapeContentY + LHeight)
            {
                TriggerFlap();
            }
            else
            {
                this.isDragging = false;
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            if (!this.isDragging)
            {
                Rectangle frameBounds = new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.phoneFrameHeight, this.phoneFrameWidth);
                int landscapeContentX = this.xPositionOnScreen + this.phoneContentOffsetY;
                int landscapeContentY = this.yPositionOnScreen + this.phoneFrameWidth - this.phoneContentOffsetX - this.contentWidth;
                Rectangle contentBounds = new Rectangle(landscapeContentX, landscapeContentY, this.contentHeight, this.contentWidth);

                // Start dragging if clicking inside phone bezel but outside screen content area
                if (frameBounds.Contains(x, y) && !contentBounds.Contains(x, y))
                {
                    this.isDragging = true;
                    this.dragOffsetX = x - this.xPositionOnScreen;
                    this.dragOffsetY = y - this.yPositionOnScreen;
                }
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            this.isDragging = false;
        }

        public override void draw(SpriteBatch b)
        {
            // Dim background behind phone
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.6f);

            int landscapeContentX = this.xPositionOnScreen + this.phoneContentOffsetY;
            int landscapeContentY = this.yPositionOnScreen + this.phoneFrameWidth - this.phoneContentOffsetX - this.contentWidth;
            int LWidth = this.contentHeight;
            int LHeight = this.contentWidth;

            // Draw phone background rotated -90 degrees
            if (this.phoneBackgroundTexture != null && !this.phoneBackgroundTexture.IsDisposed)
            {
                float bgScaleX = (float)this.contentWidth / this.phoneBackgroundTexture.Width;
                float bgScaleY = (float)this.contentHeight / this.phoneBackgroundTexture.Height;
                b.Draw(
                    this.phoneBackgroundTexture,
                    new Vector2(landscapeContentX, landscapeContentY + this.contentWidth),
                    null,
                    Color.White,
                    -MathHelper.PiOver2,
                    Vector2.Zero,
                    new Vector2(bgScaleX, bgScaleY),
                    SpriteEffects.None,
                    0f);
            }
            else
            {
                b.Draw(Game1.staminaRect, new Rectangle(landscapeContentX, landscapeContentY, LWidth, LHeight), new Color(30, 30, 30));
            }

            // Draw Scissor-tested game viewport
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });
            Rectangle previousScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(
                new Rectangle(landscapeContentX, landscapeContentY, LWidth, LHeight),
                Game1.graphics.GraphicsDevice.Viewport.Bounds);

            DrawGame(b, landscapeContentX, landscapeContentY, LWidth, LHeight);

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissor;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            // Draw phone frame rotated -90 degrees on top
            if (this.phoneFrameTexture != null && !this.phoneFrameTexture.IsDisposed)
            {
                float scaleX = (float)this.phoneFrameWidth / this.phoneFrameTexture.Width;
                float scaleY = (float)this.phoneFrameHeight / this.phoneFrameTexture.Height;
                b.Draw(
                    this.phoneFrameTexture,
                    new Vector2(this.xPositionOnScreen, this.yPositionOnScreen + this.phoneFrameWidth),
                    null,
                    Color.White,
                    -MathHelper.PiOver2,
                    Vector2.Zero,
                    new Vector2(scaleX, scaleY),
                    SpriteEffects.None,
                    0f);
            }

            // Draw custom size control buttons on the bezel rotated
            this.smartphoneApi.DrawPhoneSizeButtons(b, this.xPositionOnScreen, this.yPositionOnScreen, landscape: true);

            // Draw standard mouse cursor
            drawMouse(b);
        }

        private void DrawGame(SpriteBatch b, int lx, int ly, int lw, int lh)
        {
            float scaleX = (float)lw / VirtualWidth;
            float scaleY = (float)lh / VirtualHeight;

            // 1. Clear background to sky blue
            b.Draw(Game1.staminaRect, new Rectangle(lx, ly, lw, lh), new Color(0, 187, 196));

            // 2. Draw tiled background (img/background.png)
            if (this.bgTexture != null && !this.bgTexture.IsDisposed)
            {
                // Scale background to match full virtual screen height
                float bgScale = VirtualHeight / this.bgTexture.Height;
                int bgScaledWidth = (int)(this.bgTexture.Width * bgScale);

                int numTiles = (int)Math.Ceiling(VirtualWidth / bgScaledWidth) + 1;
                float currentOffset = this.bgOffset * bgScale;
                
                for (int i = 0; i < numTiles; i++)
                {
                    float vx = i * bgScaledWidth - currentOffset;
                    b.Draw(
                        this.bgTexture,
                        new Rectangle(
                            (int)(lx + vx * scaleX),
                            ly,
                            (int)(bgScaledWidth * scaleX),
                            (int)(VirtualHeight * scaleY)),
                        Color.White);
                }
            }

            // 3. Draw pipes (img/pipe.png)
            foreach (var pipe in this.pipes)
            {
                float topPipeBottom = pipe.GapY - pipe.GapHeight / 2f;
                float bottomPipeTop = pipe.GapY + pipe.GapHeight / 2f;

                if (this.pipeTexture != null && !this.pipeTexture.IsDisposed)
                {
                    // Draw Top Pipe (rotated 180 degrees)
                    // Position is center-bottom of top pipe in virtual space
                    Vector2 topPipePos = new Vector2(
                        lx + (pipe.X + pipe.Width / 2f) * scaleX,
                        ly + topPipeBottom * scaleY);

                    // Origin is at top-center of the pipe texture
                    Vector2 topOrigin = new Vector2(this.pipeTexture.Width / 2f, 0f);
                    
                    // We scale both locally to fit the pipe.Width and topPipeBottom height
                    Vector2 topScale = new Vector2(
                        (pipe.Width / this.pipeTexture.Width) * scaleX,
                        (topPipeBottom / this.pipeTexture.Height) * scaleY);

                    b.Draw(
                        this.pipeTexture,
                        topPipePos,
                        null,
                        Color.White,
                        (float)Math.PI, // rotated 180 degrees
                        topOrigin,
                        topScale,
                        SpriteEffects.None,
                        0f);

                    // Draw Bottom Pipe (normal direction)
                    Rectangle botDestRect = new Rectangle(
                        (int)(lx + pipe.X * scaleX),
                        (int)(ly + bottomPipeTop * scaleY),
                        (int)(pipe.Width * scaleX),
                        (int)((GroundY - bottomPipeTop) * scaleY));

                    b.Draw(this.pipeTexture, botDestRect, Color.White);
                }
                else
                {
                    // Fallback to green cards
                    Rectangle topRect = new Rectangle(
                        (int)(lx + pipe.X * scaleX),
                        ly,
                        (int)(pipe.Width * scaleX),
                        (int)(topPipeBottom * scaleY));

                    Rectangle botRect = new Rectangle(
                        (int)(lx + pipe.X * scaleX),
                        (int)(ly + bottomPipeTop * scaleY),
                        (int)(pipe.Width * scaleX),
                        (int)((GroundY - bottomPipeTop) * scaleY));

                    DrawPipeFallback(b, topRect, Color.Green, Color.Black);
                    DrawPipeFallback(b, botRect, Color.Green, Color.Black);
                }
            }

            // 4. Draw bird (img/bird.png)
            if (this.birdTexture != null && !this.birdTexture.IsDisposed)
            {
                Vector2 birdPos = new Vector2(
                    lx + this.birdX * scaleX,
                    ly + this.birdY * scaleY);

                Vector2 birdOrigin = new Vector2(this.birdTexture.Width / 2f, this.birdTexture.Height / 2f);
                // Use a uniform scale factor of 2.0f to preserve the bird texture's 17x12 aspect ratio without distortion
                float birdScaleFactor = 2.0f;
                Vector2 birdScale = new Vector2(
                    birdScaleFactor * scaleX,
                    birdScaleFactor * scaleY);

                b.Draw(
                    this.birdTexture,
                    birdPos,
                    null,
                    Color.White,
                    this.birdRotation,
                    birdOrigin,
                    birdScale,
                    SpriteEffects.None,
                    0f);
            }
            else
            {
                // Fallback to yellow circle
                DrawCircleFallback(b, new Vector2(lx + this.birdX * scaleX, ly + this.birdY * scaleY), BirdRadius * Math.Min(scaleX, scaleY), Color.Yellow, Color.Black);
            }

            // 5. Draw Ground (Dirt brown + green top grass border)
            Rectangle grassRect = new Rectangle(lx, (int)(ly + GroundY * scaleY), lw, (int)(6 * scaleY));
            Rectangle dirtRect = new Rectangle(lx, (int)(ly + (GroundY + 6) * scaleY), lw, (int)((VirtualHeight - GroundY - 6) * scaleY));

            b.Draw(Game1.staminaRect, dirtRect, new Color(139, 90, 43)); // dirt brown
            b.Draw(Game1.staminaRect, grassRect, new Color(74, 182, 60)); // grass green

            // 6. Draw UI overlay based on state
            if (this.state == GameState.Start)
            {
                string title = "FLIPPY BIRD";
                Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
                float titleScale = 0.8f * this.phoneUiScale;
                Vector2 titlePos = new Vector2(lx + lw / 2f - (titleSize.X * titleScale) / 2f, ly + lh * 0.25f - (titleSize.Y * titleScale) / 2f);

                b.DrawString(Game1.dialogueFont, title, titlePos + new Vector2(2, 2), Color.Black * 0.4f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);
                b.DrawString(Game1.dialogueFont, title, titlePos, Color.Gold, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);

                string hint = "Click or Space to Jump";
                Vector2 hintSize = Game1.smallFont.MeasureString(hint);
                float hintScale = 0.8f * this.phoneUiScale;
                float pulse = 0.6f + 0.4f * (float)Math.Abs(Math.Sin(this.bounceTimer * 3f));
                Vector2 hintPos = new Vector2(lx + lw / 2f - (hintSize.X * hintScale) / 2f, ly + lh * 0.75f - (hintSize.Y * hintScale) / 2f);

                b.DrawString(Game1.smallFont, hint, hintPos, Color.Black * pulse, 0f, Vector2.Zero, hintScale, SpriteEffects.None, 1f);
            }
            else if (this.state == GameState.Playing)
            {
                string scoreStr = this.score.ToString();
                Vector2 scoreSize = Game1.dialogueFont.MeasureString(scoreStr);
                float scoreScale = 1.2f * this.phoneUiScale;
                Vector2 scorePos = new Vector2(lx + lw / 2f - (scoreSize.X * scoreScale) / 2f, ly + lh * 0.15f - (scoreSize.Y * scoreScale) / 2f);

                b.DrawString(Game1.dialogueFont, scoreStr, scorePos + new Vector2(2, 2), Color.Black * 0.2f, 0f, Vector2.Zero, scoreScale, SpriteEffects.None, 1f);
                b.DrawString(Game1.dialogueFont, scoreStr, scorePos, Color.White * 0.8f, 0f, Vector2.Zero, scoreScale, SpriteEffects.None, 1f);
            }
            else if (this.state == GameState.GameOver)
            {
                int cardW = (int)(220 * this.phoneUiScale);
                int cardH = (int)(140 * this.phoneUiScale);
                Rectangle cardRect = new Rectangle(lx + (lw - cardW) / 2, ly + (lh - cardH) / 2, cardW, cardH);

                b.Draw(Game1.staminaRect, cardRect, Color.Black * 0.5f);
                b.Draw(Game1.staminaRect, new Rectangle(cardRect.X + 2, cardRect.Y + 2, cardRect.Width - 4, cardRect.Height - 4), Color.White * 0.85f);

                string goText = "GAME OVER";
                Vector2 goSize = Game1.dialogueFont.MeasureString(goText);
                float goScale = 0.7f * this.phoneUiScale;
                Vector2 goPos = new Vector2(cardRect.Center.X - (goSize.X * goScale) / 2f, cardRect.Y + (int)(15 * this.phoneUiScale));
                b.DrawString(Game1.dialogueFont, goText, goPos, Color.Red, 0f, Vector2.Zero, goScale, SpriteEffects.None, 1f);

                string scoreText = $"Score: {this.score}";
                string bestText = $"Best: {this.highScore}";
                float textScale = 0.8f * this.phoneUiScale;

                Vector2 scoreTextSize = Game1.smallFont.MeasureString(scoreText) * textScale;
                Vector2 scoreTextPos = new Vector2(cardRect.Center.X - scoreTextSize.X / 2f, cardRect.Y + (int)(55 * this.phoneUiScale));
                b.DrawString(Game1.smallFont, scoreText, scoreTextPos, Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);

                Vector2 bestTextSize = Game1.smallFont.MeasureString(bestText) * textScale;
                Vector2 bestTextPos = new Vector2(cardRect.Center.X - bestTextSize.X / 2f, cardRect.Y + (int)(75 * this.phoneUiScale));
                b.DrawString(Game1.smallFont, bestText, bestTextPos, Color.DarkGoldenrod, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);

                string restartText = this.restartCooldown > 0 ? "Waiting..." : "Click to Try Again";
                Vector2 restartSize = Game1.smallFont.MeasureString(restartText) * (textScale * 0.9f);
                Vector2 restartPos = new Vector2(cardRect.Center.X - restartSize.X / 2f, cardRect.Y + (int)(105 * this.phoneUiScale));
                b.DrawString(Game1.smallFont, restartText, restartPos, Color.DimGray, 0f, Vector2.Zero, textScale * 0.9f, SpriteEffects.None, 1f);
            }
        }

        private void DrawPipeFallback(SpriteBatch b, Rectangle rect, Color fillColor, Color outlineColor)
        {
            b.Draw(Game1.staminaRect, rect, outlineColor);
            b.Draw(Game1.staminaRect, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), fillColor);
        }

        private void DrawCircleFallback(SpriteBatch b, Vector2 center, float radius, Color color, Color outlineColor)
        {
            DrawCircleFill(b, center, radius + 1.5f, outlineColor);
            DrawCircleFill(b, center, radius, color);
        }

        private void DrawCircleFill(SpriteBatch b, Vector2 center, float radius, Color color)
        {
            int r = (int)radius;
            for (int y = -r; y <= r; y++)
            {
                int x = (int)Math.Sqrt(r * r - y * y);
                b.Draw(Game1.staminaRect, new Rectangle((int)(center.X - x), (int)(center.Y + y), x * 2, 1), color);
            }
        }
    }
}
