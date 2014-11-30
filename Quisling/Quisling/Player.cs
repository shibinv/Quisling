#region File Description
//-----------------------------------------------------------------------------
// Player.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using System.Collections.Generic;

namespace Quisling {
    /// <summary>
    /// Our fearless adventurer!
    /// </summary>
    class Player {
        // Animations
        private Animation idleAnimation;
        private Animation runAnimation;
        private Animation jumpAnimation;
        private Animation celebrateAnimation;
        private Animation dieAnimation;
        private Animation shootAnimation;
        private Animation movingShootAnimation;
        private Animation jumpingShootAnimation;
        private Animation climbAnimation;
        private SpriteEffects flip = SpriteEffects.None;
        private AnimationPlayer sprite;

        // Sounds
        private SoundEffect killedSound;
        private SoundEffect jumpSound;
        private SoundEffect fallSound;

        public Level Level {
            get { return level; }
        }
        Level level;

        public bool IsAlive {
            get { return isAlive; }
        }
        bool isAlive;

        // Physics state
        public Vector2 Position {
            get { return position; }
            set { position = value; }
        }
        Vector2 position;

        private float previousBottom;

        public Vector2 Velocity {
            get { return velocity; }
            set { velocity = value; }
        }
        Vector2 velocity;

        // Constants for controling horizontal movement
        private const float MoveAcceleration = 10000.0f;
        private const float MaxMoveSpeed = 1750.0f;
        private const float GroundDragFactor = 0.48f;
        private const float AirDragFactor = 0.58f;

        // Constants for controlling vertical movement
        private const float MaxJumpTime = 0.35f;
        private const float JumpLaunchVelocity = -3500.0f;
        private const float GravityAcceleration = 3400.0f;
        private const float MaxFallSpeed = 550.0f;
        private const float JumpControlPower = 0.14f;

        // Input configuration
        private const float MoveStickScale = 1.0f;
        private const float AccelerometerScale = 1.5f;
        private const Buttons JumpButton = Buttons.A;

        /// <summary>
        /// Gets whether or not the player's feet are on the ground.
        /// </summary>
        public bool IsOnGround {
            get { return isOnGround; }
        }
        bool isOnGround;

        /// <summary>
        /// Current user movement input.
        /// </summary>
        private Vector2 movement;
        private const int LadderAlignment = 12;

        // Jumping state
        private bool isJumping;
        private bool wasJumping;
        private float jumpTime;

        private bool isClimbing;
        public bool IsClimbing {
            get { return isClimbing; }
        }
        private bool wasClimbing;

        List<Vector2> bullets;
        float bulletSpeed = 300f;
        Texture2D bulletForward;
        Vector2 bulletOffset;

        private Rectangle localBounds;
        /// <summary>
        /// Gets a rectangle which bounds this player in world space.
        /// </summary>
        public Rectangle BoundingRectangle {
            get {
                int left = (int)Math.Round(Position.X - sprite.Origin.X) + localBounds.X;
                int top = (int)Math.Round(Position.Y - sprite.Origin.Y) + localBounds.Y;

                return new Rectangle(left, top, localBounds.Width, localBounds.Height);
            }
        }

        /// <summary>
        /// Constructors a new player.
        /// </summary>
        public Player(Level level, Vector2 position) {
            this.level = level;

            LoadContent();
            Reset(position);
        }

        /// <summary>
        /// Loads the player sprite sheet and sounds.
        /// </summary>
        public void LoadContent() {
            // Load animated textures.
            idleAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/bro-idle"), 0.3f, true, 95, 95);
            runAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/bro-walk"), 0.1f, true, 95, 95);
            jumpAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/bro-jump"), 0.5f, false, 95, 95);
            celebrateAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/bro-idle"), 0.1f, false, 95, 95);
            climbAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/bro-climb"), 0.1f, true, 95, 95);
            dieAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/bro-die"), 0.1f, false, 95, 95);
            shootAnimation = new Animation(Level.Content.Load<Texture2D>("Sprites/Player/IdleFire"), 1.0f, true, 95, 95);

                
            // Calculate bounds within texture size.            
            int width = (int)(idleAnimation.FrameWidth * 0.4);
            int left = (idleAnimation.FrameWidth - width) / 2;
            int height = (int)(idleAnimation.FrameHeight * 0.65);
            int top = idleAnimation.FrameHeight - height;
            localBounds = new Rectangle(left, top, width, height);

            // Calculate bounds within texture size.            
            //int width = (int)(idleAnimation.FrameWidth * 0.4);
            //int left = (idleAnimation.FrameWidth - width) / 2;
            //int height = (int)(idleAnimation.FrameWidth * 0.8);
            //int top = idleAnimation.FrameHeight - height;
            //localBounds = new Rectangle(20, 63, 25, 38);

            //load bullets
            bullets = new List<Vector2>();
            bulletForward = Level.Content.Load<Texture2D>("Sprites/Projectiles/Bullet");

            //bulletOffset = new Vector2(position.Y += 2);

            // Load sounds.            
            killedSound = Level.Content.Load<SoundEffect>("Sounds/PlayerKilled");
            jumpSound = Level.Content.Load<SoundEffect>("Sounds/PlayerJump");
            fallSound = Level.Content.Load<SoundEffect>("Sounds/PlayerFall");
        }

        /// <summary>
        /// Resets the player to life.
        /// </summary>
        /// <param name="position">The position to come to life at.</param>
        public void Reset(Vector2 position) {
            Position = position;
            Velocity = Vector2.Zero;
            isAlive = true;
            sprite.PlayAnimation(idleAnimation);
        }

        /// <summary>
        /// Handles input, performs physics, and animates the player sprite.
        /// </summary>
        /// <remarks>
        /// We pass in all of the input states so that our game is only polling the hardware
        /// once per frame. We also pass the game's orientation because when using the accelerometer,
        /// we need to reverse our motion when the orientation is in the LandscapeRight orientation.
        /// </remarks>
        public void Update(GameTime gameTime) {
            GetInput();

            ApplyPhysics(gameTime);

            //LADDER
            if (IsAlive) {
                //This if statement deals with running/idling
                if (isOnGround) {
                    //If Velocity.X is > 0 in any direction, play runAnimation
                    if (Math.Abs(Velocity.X) - 0.02f > 0)
                        sprite.PlayAnimation(runAnimation);
                    //Otherwise, sit still (idleAnimation)
                    else
                        sprite.PlayAnimation(idleAnimation);
                }
                    //This if statement deals with ladder climbing
                else if (isClimbing) {
                    //If he's moving down play ladderDownAnimation
                    if (Velocity.Y - 0.02f > 0)
                        sprite.PlayAnimation(climbAnimation);
                    //If he's moving up play ladderUpAnimation
                    else if (Velocity.Y - 0.02f < 0)
                        sprite.PlayAnimation(climbAnimation);
                    //Otherwise, just stand on the ladder (idleAnimation)
                    else
                        sprite.PlayAnimation(idleAnimation);
                }
            }

            //Reset our variables every frame
            movement = Vector2.Zero;
            wasClimbing = isClimbing;
            isClimbing = false;

            // Clear input.
            isJumping = false;

            for (int i = 0; i < bullets.Count; i++)
            {
                float x = bullets[i].X;
                x += bulletSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                bullets[i] = new Vector2(x, bullets[i].Y); 
            }
        }

        /// <summary>
        /// Gets player horizontal movement and jump commands from input.
        /// </summary>
        private void GetInput() {
            // Get input state.
            GamePadState gamePadState = GamePad.GetState(PlayerIndex.One);
            KeyboardState keyboardState = Keyboard.GetState();

            // Get analog horizontal movement.
            //movement = gamePadState.ThumbSticks.Left.X * MoveStickScale;
            movement.X = gamePadState.ThumbSticks.Left.X * MoveStickScale;
            movement.Y = gamePadState.ThumbSticks.Left.Y * MoveStickScale;

            // Ignore small movements to prevent running in place.
            //if (Math.Abs(movement) < 0.5f)
            //    movement = 0.0f;
            if (Math.Abs(movement.X) < 0.5f)
                movement.X = 0.0f;
            if (Math.Abs(movement.Y) < 0.5f)
                movement.Y = 0.0f;

            // If any digital horizontal movement input is found, override the analog movement.
            if (gamePadState.IsButtonDown(Buttons.DPadLeft) ||
                keyboardState.IsKeyDown(Keys.Left) ||
                keyboardState.IsKeyDown(Keys.A)) {
                //movement = -1.0f;
                movement.X = -1.0f;
            } else if (gamePadState.IsButtonDown(Buttons.DPadRight) ||
                       keyboardState.IsKeyDown(Keys.Right) ||
                       keyboardState.IsKeyDown(Keys.D)) {
                //movement = 1.0f;
                movement.X = 1.0f;
            }

            //LADDER
            if (gamePadState.IsButtonDown(Buttons.DPadUp) ||
                //gamePadState.ThumbSticks.Left.Y > 0.75 ||
                keyboardState.IsKeyDown(Keys.Up) ||
                keyboardState.IsKeyDown(Keys.W)) {
                isClimbing = false;

                if (IsAlignedToLadder()) {
                    //We need to check the tile behind the player,
                    //not what he is standing on
                    if (level.GetTileCollisionBehindPlayer(position) == TileCollision.Ladder) {
                        isClimbing = true;
                        isJumping = false;
                        isOnGround = false;
                        movement.Y = -1.0f;
                    }
                }
            } else if (gamePadState.IsButtonDown(Buttons.DPadDown) ||
                //gamePadState.ThumbSticks.Left.Y < -0.75 ||
                       keyboardState.IsKeyDown(Keys.Down) ||
                       keyboardState.IsKeyDown(Keys.S)) {
                isClimbing = false;

                if (IsAlignedToLadder()) {
                    // Check the tile the player is standing on
                    if (level.GetTileCollisionBelowPlayer(level.Player.Position) == TileCollision.Ladder) {
                        isClimbing = true;
                        isJumping = false;
                        isOnGround = false;
                        movement.Y = 2.0f;
                    }
                }
            }


            // Check if the player wants to jump.
            isJumping =
                gamePadState.IsButtonDown(JumpButton) ||
                keyboardState.IsKeyDown(Keys.Space) ||
                //keyboardState.IsKeyDown(Keys.Up) ||
                keyboardState.IsKeyDown(Keys.W);


            if (keyboardState.IsKeyDown(Keys.X))
            {
                bullets.Add(position - new Vector2(-level.Player.BoundingRectangle.Width + 10, (level.Player.BoundingRectangle.Height / 2)-10));
                sprite.PlayAnimation(shootAnimation);
            }


        }

        //LADDER
        private bool IsAlignedToLadder()
        {
            int playerOffset = ((int)position.X % Tile.Width) - Tile.Center;

            if (Math.Abs(playerOffset) <= LadderAlignment &&
                level.GetTileCollisionBelowPlayer(new Vector2(
                    level.Player.position.X,
                    level.Player.position.Y + 1)) == TileCollision.Ladder ||
                level.GetTileCollisionBelowPlayer(new Vector2(
                    level.Player.position.X,
                    level.Player.position.Y - 1)) == TileCollision.Ladder) {
                // Align the player with the middle of the tile
                position.X -= playerOffset;
                return true;
            } else {
                return false;
            }
        }



        /// <summary>
        /// Updates the player's velocity and position based on input, gravity, etc.
        /// </summary>
        public void ApplyPhysics(GameTime gameTime) {
            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

            Vector2 previousPosition = Position;

            // Base velocity is a combination of horizontal movement control and
            // acceleration downward due to gravity.
            //velocity.X += movement * MoveAcceleration * elapsed;
            //velocity.Y = MathHelper.Clamp(velocity.Y + GravityAcceleration * elapsed, -MaxFallSpeed, MaxFallSpeed);

            // Ladder
            if (!isClimbing) {
                if (wasClimbing)
                    velocity.Y = 0;
                else
                    velocity.Y = MathHelper.Clamp(
                        velocity.Y + GravityAcceleration * elapsed,
                            -MaxFallSpeed,
                                MaxFallSpeed);
            } else {
                velocity.Y = movement.Y * MoveAcceleration * elapsed;
            }

            velocity.X += movement.X * MoveAcceleration * elapsed;

            velocity.Y = DoJump(velocity.Y, gameTime);

            // Apply pseudo-drag horizontally.
            if (IsOnGround)
                velocity.X *= GroundDragFactor;
            else
                velocity.X *= AirDragFactor;

            // Prevent the player from running faster than his top speed.            
            velocity.X = MathHelper.Clamp(velocity.X, -MaxMoveSpeed, MaxMoveSpeed);

            // Apply velocity.
            Position += velocity * elapsed;
            Position = new Vector2((float)Math.Round(Position.X), (float)Math.Round(Position.Y));

            // If the player is now colliding with the level, separate them.
            HandleCollisions();

            // If the collision stopped us from moving, reset the velocity to zero.
            if (Position.X == previousPosition.X)
                velocity.X = 0;

            if (Position.Y == previousPosition.Y)
                velocity.Y = 0;
        }

        /// <summary>
        /// Calculates the Y velocity accounting for jumping and
        /// animates accordingly.
        /// </summary>
        /// <remarks>
        /// During the accent of a jump, the Y velocity is completely
        /// overridden by a power curve. During the decent, gravity takes
        /// over. The jump velocity is controlled by the jumpTime field
        /// which measures time into the accent of the current jump.
        /// </remarks>
        /// <param name="velocityY">
        /// The player's current velocity along the Y axis.
        /// </param>
        /// <returns>
        /// A new Y velocity if beginning or continuing a jump.
        /// Otherwise, the existing Y velocity.
        /// </returns>
        private float DoJump(float velocityY, GameTime gameTime) {
            // If the player wants to jump
            if (isJumping) {
                // Begin or continue a jump
                if ((!wasJumping && IsOnGround) || jumpTime > 0.0f) {
                    if (jumpTime == 0.0f)
                        jumpSound.Play();

                    jumpTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
                    sprite.PlayAnimation(jumpAnimation);
                }

                // If we are in the ascent of the jump
                if (0.0f < jumpTime && jumpTime <= MaxJumpTime) {
                    // Fully override the vertical velocity with a power curve that gives players more control over the top of the jump
                    velocityY = JumpLaunchVelocity * (1.0f - (float)Math.Pow(jumpTime / MaxJumpTime, JumpControlPower));
                } else {
                    // Reached the apex of the jump
                    jumpTime = 0.0f;
                }
            } else {
                // Continues not jumping or cancels a jump in progress
                jumpTime = 0.0f;
            }
            wasJumping = isJumping;

            return velocityY;
        }

        /// <summary>
        /// Detects and resolves all collisions between the player and his neighboring
        /// tiles. When a collision is detected, the player is pushed away along one
        /// axis to prevent overlapping. There is some special logic for the Y axis to
        /// handle platforms which behave differently depending on direction of movement.
        /// </summary>
        private void HandleCollisions() {
            // Get the player's bounding rectangle and find neighboring tiles.
            Rectangle bounds = BoundingRectangle;
            int leftTile = (int)Math.Floor((float)bounds.Left / Tile.Width);
            int rightTile = (int)Math.Ceiling(((float)bounds.Right / Tile.Width)) - 1;
            int topTile = (int)Math.Floor((float)bounds.Top / Tile.Height);
            int bottomTile = (int)Math.Ceiling(((float)bounds.Bottom / Tile.Height)) - 1;

            // Reset flag to search for ground collision.
            isOnGround = false;

            // For each potentially colliding tile,
            for (int y = topTile; y <= bottomTile; ++y) {
                for (int x = leftTile; x <= rightTile; ++x) {
                    // If this tile is collidable,
                    TileCollision collision = Level.GetCollision(x, y);
                    if (collision != TileCollision.Passable) {
                        // Determine collision depth (with direction) and magnitude.
                        Rectangle tileBounds = Level.GetBounds(x, y);
                        Vector2 depth = RectangleExtensions.GetIntersectionDepth(bounds, tileBounds);
                        if (depth != Vector2.Zero) {
                            float absDepthX = Math.Abs(depth.X);
                            float absDepthY = Math.Abs(depth.Y);

                            // Resolve the collision along the shallow axis.
                            if (absDepthY < absDepthX || collision == TileCollision.Platform) {
                                // If we crossed the top of a tile, we are on the ground.
                                //LADDER
                                // If we crossed the top of a tile, we are on the ground.
                                if (previousBottom <= tileBounds.Top) {
                                    if (collision == TileCollision.Ladder) {
                                        if (!isClimbing && !isJumping) {
                                            // When walking over a ladder
                                            isOnGround = true;
                                        }
                                    } else {
                                        isOnGround = true;
                                        isClimbing = false;
                                        isJumping = false;
                                    }
                                }

                                // Ignore platforms, unless we are on the ground.
                                if (collision == TileCollision.Impassable || IsOnGround) {
                                    // Resolve the collision along the Y axis.
                                    Position = new Vector2(Position.X, Position.Y + depth.Y);

                                    // Perform further collisions with the new bounds.
                                    bounds = BoundingRectangle;
                                }
                            } else if (collision == TileCollision.Impassable) // Ignore platforms.
                            {
                                // Resolve the collision along the X axis.
                                Position = new Vector2(Position.X + depth.X, Position.Y);

                                // Perform further collisions with the new bounds.
                                bounds = BoundingRectangle;
                            } else if (collision == TileCollision.Ladder && !isClimbing) {
                                // When walking in front of a ladder, falling off a ladder
                                // but not climbing

                                // Resolve the collision along the Y axis.
                                Position = new Vector2(Position.X, Position.Y);

                                // Perform further collisions with the new bounds.
                                bounds = BoundingRectangle;
                            }
                        }
                    }
                }
            }

            // Save the new bounds bottom.
            previousBottom = bounds.Bottom;
        }

        /// <summary>
        /// Called when the player has been killed.
        /// </summary>
        /// <param name="killedBy">
        /// The enemy who killed the player. This parameter is null if the player was
        /// not killed by an enemy (fell into a hole).
        /// </param>
        public void OnKilled(Enemy killedBy) {
            isAlive = false;

            if (killedBy != null)
                killedSound.Play();
            else
                fallSound.Play();

            sprite.PlayAnimation(dieAnimation);
           
        }

        /// <summary>
        /// Called when this player reaches the level's exit.
        /// </summary>
        public void OnReachedExit() {
            sprite.PlayAnimation(celebrateAnimation);
        }


        /// <summary>
        /// Draws the animated player.
        /// </summary>
        public void Draw(GameTime gameTime, SpriteBatch spriteBatch) {
            // Flip the sprite to face the way we are moving.
            if (Velocity.X > 0)
                //flip = SpriteEffects.FlipHorizontally;
                flip = SpriteEffects.None;
            else if (Velocity.X < 0)
                //flip = SpriteEffects.None;
                flip = SpriteEffects.FlipHorizontally;

            // Draw that sprite.
            sprite.Draw(gameTime, spriteBatch, Position, flip);

            for (int i = 0; i < bullets.Count; i++)
            {
                spriteBatch.Draw(bulletForward, bullets[i], Color.White);
            }
        }
    }
}
