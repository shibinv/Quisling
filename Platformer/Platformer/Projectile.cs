using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quisling
{
    class Projectile
    {
        private Animation forwardProjectile;
        private Animation reverseProjectile;


        public Level Level
        {
            get { return level; }
        }
        Level level;


        public void LoadContent()
        {
            forwardProjectile = new Animation(Level.Content.Load<Texture2D>("Sprites/Projectiles/BulletMain"), 0.3f, true, 95, 95);  
    
        }


        public void Draw(GameTime gametime, SpriteBatch spritebatch)
        {

        }
    }
}
