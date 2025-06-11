using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WonderGame.Core
{
    public static class Global
    {
        public static SpriteBatch? SpriteBatch { get; set; }
        public static Texture2D? Pixel { get; set; }

        public static void Initialize(GraphicsDevice graphicsDevice)
        {
            Pixel = new Texture2D(graphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });
        }
    }
} 