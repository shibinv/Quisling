﻿#region File Description
//-----------------------------------------------------------------------------
// Level.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using System.IO;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Input;

namespace Quisling
{
    /// <summary>
    /// A uniform grid of tiles with collections of items and enemies.
    /// The level owns the player and controls the game's win and lose
    /// conditions as well as scoring.
    /// </summary>
    class Level : IDisposable
    {
        // Physical structure of the level.
        private Tile[,] tiles;
        private Layer[] layers;
        // The layer which entities are drawn on top of.
        private const int EntityLayer = 2;

        // Entities in the level.
        public Player Player
        {
            get { return player; }
        }
        Player player;

        private List<Item> items = new List<Item>();
        private List<Enemy> enemies = new List<Enemy>();

        // Key locations in the level.        
        private Vector2 start;
        private Point exit = InvalidPosition;
        private static readonly Point InvalidPosition = new Point(-1, -1);

        // Level game state.
        private Random random = new Random(354668); // Arbitrary, but constant seed
        private float cameraPositionXAxis;
        public float cameraPositionYAxis;

        // count items
        public int TotalItems = 0;
        public int CollectedItems = 0;

        public int Score
        {
            get { return score; }
        }
        int score;

        public bool ReachedExit
        {
            get { return reachedExit; }
        }
        bool reachedExit;

        public TimeSpan TimeRemaining
        {
            get { return timeRemaining; }
        }
        TimeSpan timeRemaining;

        private const int PointsPerSecond = 5;

        // Level content.        
        public ContentManager Content
        {
            get { return content; }
        }
        ContentManager content;

        private SoundEffect exitReachedSound;

        #region Loading

        /// <summary>
        /// Constructs a new level.
        /// </summary>
        /// <param name="serviceProvider">
        /// The service provider that will be used to construct a ContentManager.
        /// </param>
        /// <param name="fileStream">
        /// A stream containing the tile data.
        /// </param>
        public Level(IServiceProvider serviceProvider, Stream fileStream, int levelIndex)
        {
            // Create a new content manager to load content used just by this level.
            content = new ContentManager(serviceProvider, "Content");

            timeRemaining = TimeSpan.FromMinutes(2.0);

            LoadTiles(fileStream);

            // Load background layer textures. For now, all levels must
            // use the same backgrounds and only use the left-most part of them.
            layers = new Layer[1];
            layers[0] = new Layer(Content, "Backgrounds/Layer0", 0.2f);
            //layers[1] = new Layer(Content, "Backgrounds/Layer1", 0.5f);
            //layers[2] = new Layer(Content, "Backgrounds/Layer2", 0.8f);

            // Load sounds.
            exitReachedSound = Content.Load<SoundEffect>("Sounds/Victory");
        }

        /// <summary>
        /// Iterates over every tile in the structure file and loads its
        /// appearance and behavior. This method also validates that the
        /// file is well-formed with a player start point, exit, etc.
        /// </summary>
        /// <param name="fileStream">
        /// A stream containing the tile data.
        /// </param>
        private void LoadTiles(Stream fileStream)
        {
            // Load the level and ensure all of the lines are the same length.
            int width;
            List<string> lines = new List<string>();
            using (StreamReader reader = new StreamReader(fileStream))
            {
                string line = reader.ReadLine();
                width = line.Length;
                while (line != null)
                {
                    lines.Add(line);
                    if (line.Length != width)
                        throw new Exception(String.Format("The length of line {0} is different from all preceeding lines.", lines.Count));
                    line = reader.ReadLine();
                }
            }

            // Allocate the tile grid.
            tiles = new Tile[width, lines.Count];

            // Loop over every tile position,
            for (int y = 0; y < Height; ++y)
            {
                for (int x = 0; x < Width; ++x)
                {
                    // to load each tile.
                    char tileType = lines[y][x];
                    tiles[x, y] = LoadTile(tileType, x, y);
                }
            }

            // Verify that the level has a beginning and an end.
            if (Player == null)
                throw new NotSupportedException("A level must have a starting point.");
            if (exit == InvalidPosition)
                throw new NotSupportedException("A level must have an exit.");

        }

        /// <summary>
        /// Loads an individual tile's appearance and behavior.
        /// </summary>
        /// <param name="tileType">
        /// The character loaded from the structure file which
        /// indicates what should be loaded.
        /// </param>
        /// <param name="x">
        /// The X location of this tile in tile space.
        /// </param>
        /// <param name="y">
        /// The Y location of this tile in tile space.
        /// </param>
        /// <returns>The loaded tile.</returns>
        private Tile LoadTile(char tileType, int x, int y)
        {
            switch (tileType)
            {
                // Blank space
                case '.':
                    return new Tile(null, TileCollision.Passable);

                // Exit
                case 'X':
                    return LoadExitTile(x, y);

                // Item
                case 'G':
                    return LoadItemTile(x, y);

                // Floating platform
                case '-':
                    return LoadTile("Glass", TileCollision.Platform);

                // Various enemies
                case 'A':
                    return LoadEnemyTile(x, y, "ZombieA");
                case 'B':
                    return LoadEnemyTile(x, y, "ZombieB");
                case 'C':
                    return LoadEnemyTile(x, y, "ZombieC");
                case 'D':
                    return LoadEnemyTile(x, y, "ZombieD");

                // Platform block
                case '~':
                    return LoadVarietyTile("PassA", 2, TileCollision.Platform);

                // Passable block
                case ':':
                    return LoadVarietyTile("PassA", 2, TileCollision.Passable);

                // Player 1 start point
                case '1':
                    return LoadStartTile(x, y);

                // Impassable block
                case '#':
                    return LoadVarietyTile("GroundA", 2, TileCollision.Impassable);

                // Ladder
                case 'H':
                    return LoadTile("Ladder", TileCollision.Ladder);

                // Unknown tile type character
                default:
                    throw new NotSupportedException(String.Format("Unsupported tile type character '{0}' at position {1}, {2}.", tileType, x, y));
            }
        }

        /// <summary>
        /// Creates a new tile. The other tile loading methods typically chain to this
        /// method after performing their special logic.
        /// </summary>
        /// <param name="name">
        /// Path to a tile texture relative to the Content/Tiles directory.
        /// </param>
        /// <param name="collision">
        /// The tile collision type for the new tile.
        /// </param>
        /// <returns>The new tile.</returns>
        private Tile LoadTile(string name, TileCollision collision)
        {
            return new Tile(Content.Load<Texture2D>("Tiles/" + name), collision);
        }


        /// <summary>
        /// Loads a tile with a random appearance.
        /// </summary>
        /// <param name="baseName">
        /// The content name prefix for this group of tile variations. Tile groups are
        /// name LikeThis0.png and LikeThis1.png and LikeThis2.png.
        /// </param>
        /// <param name="variationCount">
        /// The number of variations in this group.
        /// </param>
        private Tile LoadVarietyTile(string baseName, int variationCount, TileCollision collision)
        {
            int index = random.Next(variationCount);
            return LoadTile(baseName + index, collision);
        }


        /// <summary>
        /// Instantiates a player, puts him in the level, and remembers where to put him when he is resurrected.
        /// </summary>
        private Tile LoadStartTile(int x, int y)
        {
            if (Player != null)
                throw new NotSupportedException("A level may only have one starting point.");

            start = RectangleExtensions.GetBottomCenter(GetBounds(x, y));
            player = new Player(this, start);

            return new Tile(null, TileCollision.Passable);
        }

        /// <summary>
        /// Remembers the location of the level's exit.
        /// </summary>
        private Tile LoadExitTile(int x, int y)
        {
            if (exit != InvalidPosition)
                throw new NotSupportedException("A level may only have one exit.");

            exit = GetBounds(x, y).Center;

            return LoadTile("Exit", TileCollision.Passable);
        }

        /// <summary>
        /// Instantiates an enemy and puts him in the level.
        /// </summary>
        private Tile LoadEnemyTile(int x, int y, string spriteSet)
        {
            Vector2 position = RectangleExtensions.GetBottomCenter(GetBounds(x, y));
            enemies.Add(new Enemy(this, position, spriteSet));

            return new Tile(null, TileCollision.Passable);
        }

        /// <summary>
        /// Instantiates a item and puts it in the level.
        /// </summary>
        private Tile LoadItemTile(int x, int y)
        {
            Point position = GetBounds(x, y).Center;
            items.Add(new Item(this, new Vector2(position.X, position.Y)));
            TotalItems++;
            return new Tile(null, TileCollision.Passable);
        }

        /// <summary>
        /// Unloads the level content.
        /// </summary>
        public void Dispose()
        {
            Content.Unload();
        }

        #endregion

        #region Bounds and collision

        /// <summary>
        /// Gets the collision mode of the tile at a particular location.
        /// This method handles tiles outside of the levels boundries by making it
        /// impossible to escape past the left or right edges, but allowing things
        /// to jump beyond the top of the level and fall off the bottom.
        /// </summary>
        public TileCollision GetCollision(int x, int y)
        {
            // Prevent escaping past the level ends.
            if (x < 0 || x >= Width)
                return TileCollision.Impassable;
            // Allow jumping past the level top and falling through the bottom.
            if (y < 0 || y >= Height)
                return TileCollision.Passable;

            return tiles[x, y].Collision;
        }

                public TileCollision GetTileCollisionBehindPlayer(Vector2 playerPosition)
                {
                    int x = (int)playerPosition.X / Tile.Width;
                    int y = (int)(playerPosition.Y - 1) / Tile.Height;
 
                    // Prevent escaping past the level ends.
                    if (x == Width)
                        return TileCollision.Impassable;
                    // Allow jumping past the level top and falling through the bottom.
                    if (y == Height)
                        return TileCollision.Passable;
 
                    return tiles[x, y].Collision;
                }
 
                public TileCollision GetTileCollisionBelowPlayer(Vector2 playerPosition)
                {
                    int x = (int)playerPosition.X / Tile.Width;
                    int y = (int)(playerPosition.Y) / Tile.Height;

                    if (x < 0 || x >= Width)
                        return TileCollision.Impassable;
                    // Allow jumping past the level top and falling through the bottom.
                    if (y < 0 || y >= Height)
                        return TileCollision.Passable;

                    return tiles[x, y].Collision;
                }

        /// <summary>
        /// Gets the bounding rectangle of a tile in world space.
        /// </summary>        
        public Rectangle GetBounds(int x, int y)
        {
            return new Rectangle(x * Tile.Width, y * Tile.Height, Tile.Width, Tile.Height);
        }

        /// <summary>
        /// Width of level measured in tiles.
        /// </summary>
        public int Width
        {
            get { return tiles.GetLength(0); }
        }

        /// <summary>
        /// Height of the level measured in tiles.
        /// </summary>
        public int Height
        {
            get { return tiles.GetLength(1); }
        }

        #endregion

        #region Update

        /// <summary>
        /// Updates all objects in the world, performs collision between them,
        /// and handles the time limit with scoring.
        /// </summary>
        public void Update(
            GameTime gameTime, 
            KeyboardState keyboardState, 
            GamePadState gamePadState, 
            TouchCollection touchState, 
            AccelerometerState accelState,
            DisplayOrientation orientation)
        {
            // Pause while the player is dead or time is expired.
            if (!Player.IsAlive || TimeRemaining == TimeSpan.Zero)
            {
                // Still want to perform physics on the player.
                Player.ApplyPhysics(gameTime);
            }
            else if (ReachedExit)
            {
                // Animate the time being converted into points.
                int seconds = (int)Math.Round(gameTime.ElapsedGameTime.TotalSeconds * 100.0f);
                seconds = Math.Min(seconds, (int)Math.Ceiling(TimeRemaining.TotalSeconds));
                timeRemaining -= TimeSpan.FromSeconds(seconds);
                score += seconds * PointsPerSecond;
            }
            else
            {
                timeRemaining -= gameTime.ElapsedGameTime;
                Player.Update(gameTime);
                UpdateItems(gameTime);

                // Falling off the bottom of the level kills the player.
                if (Player.BoundingRectangle.Top >= Height * Tile.Height)
                    OnPlayerKilled(null);

                UpdateEnemies(gameTime);

                // The player has reached the exit if they are standing on the ground and
                // his bounding rectangle contains the center of the exit tile. They can only
                // exit when they have collected all of the items.
                if (Player.IsAlive &&
                    Player.IsOnGround &&
                    Player.BoundingRectangle.Contains(exit))
                {
                    OnExitReached();
                }
            }

            // Clamp the time remaining at zero.
            if (timeRemaining < TimeSpan.Zero)
                timeRemaining = TimeSpan.Zero;
        }

        /// <summary>
        /// Animates each item and checks to allows the player to collect them.
        /// </summary>
        private void UpdateItems(GameTime gameTime)
        {
            for (int i = 0; i < items.Count; ++i)
            {
                Item item = items[i];

                item.Update(gameTime);

                if (item.BoundingCircle.Intersects(Player.BoundingRectangle))
                {
                    items.RemoveAt(i--);
                    OnItemCollected(item, Player);
                    CollectedItems++;
                }
            }
        }

        /// <summary>
        /// Animates each enemy and allow them to kill the player.
        /// </summary>
        private void UpdateEnemies(GameTime gameTime)
        {
            foreach (Enemy enemy in enemies)
            {
                enemy.Update(gameTime);

                // Touching an enemy instantly kills the player
                if (enemy.BoundingRectangle.Intersects(Player.BoundingRectangle))
                {
                    OnPlayerKilled(enemy);
                }
            }
        }

        /// <summary>
        /// Called when a item is collected.
        /// </summary>
        /// <param name="item">The item that was collected.</param>
        /// <param name="collectedBy">The player who collected this item.</param>
        private void OnItemCollected(Item item, Player collectedBy)
        {
            score += Item.PointValue;

            item.OnCollected(collectedBy);
        }

        /// <summary>
        /// Called when the player is killed.
        /// </summary>
        /// <param name="killedBy">
        /// The enemy who killed the player. This is null if the player was not killed by an
        /// enemy, such as when a player falls into a hole.
        /// </param>
        private void OnPlayerKilled(Enemy killedBy)
        {
            Player.OnKilled(killedBy);
        }

        /// <summary>
        /// Called when the player reaches the level's exit.
        /// </summary>
        private void OnExitReached()
        {
            if (TotalItems == CollectedItems) {
                Player.OnReachedExit();
                exitReachedSound.Play();
                reachedExit = true;
            } else {
                OnPlayerKilled(null);
            }
        }

        /// <summary>
        /// Restores the player to the starting point to try the level again.
        /// </summary>
        public void StartNewLife()
        {
            Player.Reset(start);
        }

        #endregion

        #region Draw

        /// <summary>
        /// Draw everything in the level from background to foreground.
        /// </summary>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch) {
            spriteBatch.Begin();
            //for (int i = 0; i <= EntityLayer; ++i)
                layers[0].Draw(spriteBatch, cameraPositionXAxis);
            spriteBatch.End();

            ScrollCamera(spriteBatch.GraphicsDevice.Viewport);
            //Matrix cameraTransform = Matrix.CreateTranslation(-cameraPositionXAxis, 0.0f, 0.0f);
            Matrix cameraTransform = Matrix.CreateTranslation(-cameraPositionXAxis, -cameraPositionYAxis, 0.0f);
            //spriteBatch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Immediate, SaveStateMode.None, cameraTransform);
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null,null,null,null,cameraTransform);

            DrawTiles(spriteBatch);

            foreach (Item item in items)
                item.Draw(gameTime, spriteBatch);

            Player.Draw(gameTime, spriteBatch);

            foreach (Enemy enemy in enemies)
                enemy.Draw(gameTime, spriteBatch);

            spriteBatch.End();

            spriteBatch.Begin();
            for (int i = EntityLayer + 1; i < layers.Length; ++i)
                layers[i].Draw(spriteBatch, cameraPositionXAxis);
            spriteBatch.End();
        }

        private void ScrollCamera(Viewport viewport)
    {
#if ZUNE
const float ViewMargin = 0.45f;
#else
const float ViewMargin = 0.35f;
#endif

      // Calculate the edges of the screen.
      float marginWidth = viewport.Width * ViewMargin;
      float marginLeft = cameraPositionXAxis + marginWidth;
      float marginRight = cameraPositionXAxis + viewport.Width - marginWidth;
      const float TopMargin = 0.3f;
      const float BottomMargin = 0.1f;
      float marginTop = cameraPositionYAxis + viewport.Height * TopMargin;
      float marginBottom = cameraPositionYAxis + viewport.Height - viewport.Height * BottomMargin;

      float maxCameraPositionYOffset = Tile.Height * Height - viewport.Height;

      // Calculate how far to scroll when the player is near the edges of the screen.
      float cameraMovement = 0.0f;
      if (Player.Position.X < marginLeft)
        cameraMovement = Player.Position.X - marginLeft;
      else if (Player.Position.X > marginRight)
        cameraMovement = Player.Position.X - marginRight;

      // Calculate how far to vertically scroll when the player is near the top or bottom of the screen.  
      float cameraMovementY = 0.0f;
      if (Player.Position.Y < marginTop) //above the top margin  
          cameraMovementY = Player.Position.Y - marginTop;
      else if (Player.Position.Y > marginBottom) //below the bottom margin  
          cameraMovementY = Player.Position.Y - marginBottom;  


      // Update the camera position, but prevent scrolling off the ends of the level.
      float maxCameraPosition = Tile.Width * Width - viewport.Width;
      cameraPositionXAxis = MathHelper.Clamp(cameraPositionXAxis + cameraMovement, 0.0f, maxCameraPosition);

      cameraPositionYAxis = MathHelper.Clamp(cameraPositionYAxis + cameraMovementY, 0.0f, maxCameraPositionYOffset); 

        }

        /// <summary>
        /// Draws each tile in the level.
        /// </summary>
        private void DrawTiles(SpriteBatch spriteBatch)
        {
            // Calculate the visible range of tiles.
            int left = (int)Math.Floor(cameraPositionXAxis / Tile.Width);
            int right = left + spriteBatch.GraphicsDevice.Viewport.Width / Tile.Width;
            right = Math.Min(right, Width - 1);

            // For each tile position
            for (int y = 0; y < Height; ++y)
            {
                for (int x = left; x <= right; ++x)
                {
                    // If there is a visible tile in that position
                    Texture2D texture = tiles[x, y].Texture;
                    if (texture != null)
                    {
                        // Draw it in screen space.
                        Vector2 position = new Vector2(x, y) * Tile.Size;
                        spriteBatch.Draw(texture, position, Color.White);
                    }
                }
            }
        }

        #endregion
    }
}
