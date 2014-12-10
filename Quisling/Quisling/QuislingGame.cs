#region File Description
//-----------------------------------------------------------------------------
// QuislingGame.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Input.Touch;


namespace Quisling {
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class QuislingGame : Microsoft.Xna.Framework.Game {
        // Resources for drawing.
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        // Global content.
        private SpriteFont hudFont;

        private Texture2D winOverlay;
        private Texture2D loseOverlay;
        private Texture2D diedOverlay;

        // Meta-level game state.
        private int levelIndex = -1;
        private Level level;
        private bool wasContinuePressed;

        // When the time remaining is less than the warning time, it blinks on the hud
        private static readonly TimeSpan WarningTime = TimeSpan.FromSeconds(30);

        // We store our input states so that we only poll once per frame, 
        // then we use the same input state wherever needed
        private GamePadState gamePadState;
        private KeyboardState keyboardState;
        private KeyboardState pkeyboardState;
        private TouchCollection touchState;
        private AccelerometerState accelerometerState;

        // The number of levels in the Levels directory of our content. We assume that
        // levels in our content are 0-based and that all numbers under this constant
        // have a level file present. This allows us to not need to check for the file
        // or handle exceptions, both of which can add unnecessary time to level loading.
        private const int numberOfLevels = 10;

        public QuislingGame() {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

#if WINDOWS_PHONE
            graphics.IsFullScreen = true;
            TargetElapsedTime = TimeSpan.FromTicks(333333);
#endif

            Accelerometer.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent() {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load fonts
            hudFont = Content.Load<SpriteFont>("Fonts/Hud");

            // Load overlay textures
            winOverlay = Content.Load<Texture2D>("Overlays/Win");
            loseOverlay = Content.Load<Texture2D>("Overlays/Infected");
            diedOverlay = Content.Load<Texture2D>("Overlays/Dead");

            //Known issue that you get exceptions if you use Media PLayer while connected to your PC
            //See http://social.msdn.microsoft.com/Forums/en/windowsphone7series/thread/c8a243d2-d360-46b1-96bd-62b1ef268c66
            //Which means its impossible to test this from VS.
            //So we have to catch the exception and throw it away
            try {
                MediaPlayer.IsRepeating = true;
                MediaPlayer.Play(Content.Load<Song>("Sounds/Background"));
            } catch { }

            LoadNextLevel();
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime) {
            // Handle polling for our input and handling high-level input
            HandleInput();

            // update our level, passing down the GameTime along with all of our input states
            level.Update(gameTime, keyboardState, gamePadState, touchState,
                         accelerometerState, Window.CurrentOrientation);

            base.Update(gameTime);
        }

        private void HandleInput() {
            // get all of our input states
            keyboardState = Keyboard.GetState();
            gamePadState = GamePad.GetState(PlayerIndex.One);
            touchState = TouchPanel.GetState();
            accelerometerState = Accelerometer.GetState();

            // Exit the game when back is pressed.
            if (gamePadState.Buttons.Back == ButtonState.Pressed)
                Exit();

            bool continuePressed =
                keyboardState.IsKeyDown(Keys.Space) ||
                gamePadState.IsButtonDown(Buttons.A) ||
                touchState.AnyTouch();

            // level skip for testing
            //if (keyboardState.IsKeyDown(Keys.Q) && keyboardState.IsKeyDown(Keys.P) && !pkeyboardState.IsKeyDown(Keys.P)) {
            //    LoadNextLevel();
            //}

            // Perform the appropriate action to advance the game and
            // to get the player back to playing.
            if (!wasContinuePressed && continuePressed) {
                if (!level.Player.IsAlive) {
                    level.StartNewLife();
                } else if (level.TimeRemaining == TimeSpan.Zero) {
                    if (level.ReachedExit)
                        LoadNextLevel();
                    else
                        ReloadCurrentLevel();
                }
            }

            wasContinuePressed = continuePressed;
            pkeyboardState = Keyboard.GetState();
        }

        private void LoadNextLevel() {
            // move to the next level
            levelIndex = (levelIndex + 1) % numberOfLevels;
            //levelIndex = 9;

            // Unloads the content for the current level before loading the next one.
            if (level != null)
                level.Dispose();

            // Load the level.
            string levelPath = string.Format("Content/Levels/{0}.txt", levelIndex);
            using (Stream fileStream = TitleContainer.OpenStream(levelPath))
                level = new Level(Services, fileStream, levelIndex);
        }

        private void ReloadCurrentLevel() {
            --levelIndex;
            LoadNextLevel();
        }

        /// <summary>
        /// Draws the game from background to foreground.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime) {
            graphics.GraphicsDevice.Clear(Color.CornflowerBlue);


            //spriteBatch.Begin();

            level.Draw(gameTime, spriteBatch);

            DrawHud();

            //spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawHud() {
            spriteBatch.Begin();

            Rectangle titleSafeArea = GraphicsDevice.Viewport.TitleSafeArea;
            Vector2 hudLocation = new Vector2(titleSafeArea.X, titleSafeArea.Y);
            Vector2 center = new Vector2(titleSafeArea.X + titleSafeArea.Width / 2.0f,
                                         titleSafeArea.Y + titleSafeArea.Height / 2.0f);

            // Draw time remaining. Uses modulo division to cause blinking when the
            // player is running out of time.
            string timeString = "INFECTION: " + level.TimeRemaining.Minutes.ToString("00") + ":" + level.TimeRemaining.Seconds.ToString("00");
            Color timeColor;
            if (level.TimeRemaining > WarningTime ||
                level.ReachedExit ||
                (int)level.TimeRemaining.TotalSeconds % 2 == 0) {
                timeColor = Color.Cornsilk;
            } else {
                timeColor = Color.Maroon;
            }
            DrawShadowedString(hudFont, timeString, hudLocation, timeColor);

            // Draw Vials
            float timeHeight = hudFont.MeasureString(timeString).Y;
            DrawShadowedString(hudFont, "VIALS: " + level.CollectedItems.ToString() + "/" + level.TotalItems.ToString(), hudLocation + new Vector2(0.0f, timeHeight * 1.2f), Color.DarkSalmon);

            // draw level
            DrawShadowedString(hudFont, "LEVEL: " + ((int)levelIndex + 1), hudLocation + new Vector2(0.0f, timeHeight * 2.4f), timeColor);

            // Determine the status overlay message to show.
            Texture2D status = null;
            if (level.TimeRemaining == TimeSpan.Zero) {
                if (level.ReachedExit) {
                    status = winOverlay;
                } else {
                    status = loseOverlay;
                }
            } else if (!level.Player.IsAlive) {
                status = diedOverlay;
            }

            if (status != null) {
                // Draw status message.
                Vector2 statusSize = new Vector2(status.Width, status.Height);
                spriteBatch.Draw(status, center - statusSize / 2, Color.White);
            }
            spriteBatch.End();
        }

        private void DrawShadowedString(SpriteFont font, string value, Vector2 position, Color color) {
            spriteBatch.DrawString(font, value, position + new Vector2(1.0f, 1.0f), Color.Black);
            spriteBatch.DrawString(font, value, position, color);
        }
    }
}
