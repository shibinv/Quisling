using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quisling
{
    class Animation
    {
        Texture2D spriteSheet;
        float scale;
        int elapsedTime;
        int frameTime;
        Point frameCount;
        Point currentFrame;
        Color color;
        Rectangle sourceRect = new Rectangle();
        Rectangle destRect = new Rectangle();
        public bool Active;
        public bool Looping;
        public Vector2 Position;


        public void Initalize()
        {

        }

        public void Update(GameTime gameTime)
        {

        }

        public void Draw(SpriteBatch spriteBatch)
        {

        }
    }
}
