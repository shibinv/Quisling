#region File Description
//-----------------------------------------------------------------------------
// Item.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;

namespace Quisling {
    /// <summary>
    /// A valuable Item the player can collect.
    /// </summary>
    class Item {
        private Texture2D texture;
        private Vector2 origin;
        private SoundEffect collectedSound;

        public const int PointValue = 30;
        public readonly Color Color = Color.Yellow;

        // The Item is animated from a base position along the Y axis.
        private Vector2 basePosition;
        private float bounce;

        public Level Level {
            get { return level; }
        }
        Level level;

        /// <summary>
        /// Gets the current position of this Item in world space.
        /// </summary>
        public Vector2 Position {
            get {
                return basePosition + new Vector2(0.0f, bounce);
            }
        }

        /// <summary>
        /// Gets a circle which bounds this Item in world space.
        /// </summary>
        public Circle BoundingCircle {
            get {
                return new Circle(Position, Tile.Width / 3.0f);
            }
        }

        /// <summary>
        /// Constructs a new Item.
        /// </summary>
        public Item(Level level, Vector2 position) {
            this.level = level;
            this.basePosition = position;

            LoadContent();
        }

        /// <summary>
        /// Loads the Item texture and collected sound.
        /// </summary>
        public void LoadContent() {
            texture = Level.Content.Load<Texture2D>("Sprites/Capsule");
            origin = new Vector2(texture.Width / 1.0f, texture.Height / 1.0f);
            collectedSound = Level.Content.Load<SoundEffect>("Sounds/ItemCollected");
        }

        /// <summary>
        /// Bounces up and down in the air to entice players to collect them.
        /// </summary>
        public void Update(GameTime gameTime) {
            // Bounce control constants
            const float BounceHeight = 0.18f;
            const float BounceRate = 3.0f;
            const float BounceSync = -0.75f;

            // Bounce along a sine curve over time.
            // Include the X coordinate so that neighboring Items bounce in a nice wave pattern.            
            double t = gameTime.TotalGameTime.TotalSeconds * BounceRate + Position.X * BounceSync;
            bounce = (float)Math.Sin(t) * BounceHeight * texture.Height;
        }

        /// <summary>
        /// Called when this Item has been collected by a player and removed from the level.
        /// </summary>
        /// <param name="collectedBy">
        /// The player who collected this Item. Although currently not used, this parameter would be
        /// useful for creating special powerup Items. For example, a Item could make the player invincible.
        /// </param>
        public void OnCollected(Player collectedBy) {
            collectedSound.Play();
        }

        /// <summary>
        /// Draws a Item in the appropriate color.
        /// </summary>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch) {
            spriteBatch.Draw(texture, Position, null, Color, 0.0f, origin, 1.0f, SpriteEffects.None, 0.0f);
        }
    }
}
