using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WonderGame.Core
{
    /// <summary>
    /// Helper to get a 1x1 white pixel texture for drawing solid rectangles.
    /// </summary>
    public static class GraphicsDeviceExtensions
    {
        private static Texture2D? _whitePixel;

        public static Texture2D GetWhitePixel(this GraphicsDevice graphicsDevice)
        {
            if (_whitePixel == null)
            {
                _whitePixel = new Texture2D(graphicsDevice, 1, 1);
                _whitePixel.SetData(new[] { Color.White });
            }
            return _whitePixel;
        }
    }
} 