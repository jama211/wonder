using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WonderGame.Screens;

public interface IScreen
{
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime);
} 