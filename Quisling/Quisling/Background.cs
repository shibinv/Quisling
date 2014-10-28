using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quisling
{
    class Background
    {
        Texture2D texture;
        Vector2[] position;
        int speed;

        public void Initalize(ContentManager content, String texturePath, int screenWidth, int speed)
        {

            texture = content.Load<Texture2D>(texturePath);

            this.speed = speed;

            position = new Vector2[screenWidth / texture.Width + 2];

            for (int i = 0; i < position.Length; i++)
            {
                position[i] = new Vector2(i * texture.Width, 0);
            }

        }

        public void Update()
        {
            for (int i = 0; i < position.Length; i++)
            {
                position[i].X += speed;
               
                if (speed <= 0)
                {
                    if (position[i].X <= -texture.Width)
                    {
                        position[i].X = texture.Width * (position.Length - 1);
                    }
                }
                else
                {
                    if (position[i].X >= texture.Width * (position.Length - 1))
                    {
                        position[i].X = -texture.Width;
                    }
                }
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            for (int i = 0; i < position.Length; i++)
            {
                spriteBatch.Draw(texture, position[i], Color.White);
            }
        }
    }
}
