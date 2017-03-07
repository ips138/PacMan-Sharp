using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace XNAPacMan {
    public class XNAPacMan : Microsoft.Xna.Framework.Game {
        GraphicsDeviceManager graphics_;
        SpriteBatch spriteBatch_;
        AudioEngine audioEngine_;
        WaveBank waveBank_;
        SoundBank soundBank_;

        public XNAPacMan() {
            Window.Title = "PAC-MAN";
            graphics_ = new GraphicsDeviceManager(this);
            graphics_.PreferredBackBufferHeight = 720;
            graphics_.PreferredBackBufferWidth = 640;

            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromMilliseconds(1);

            Components.Add(new Menu(this, null));

            Content.RootDirectory = "Content";
        }

        protected override void Initialize() {
            audioEngine_ = new AudioEngine("Content/Audio/YEPAudio.xgs");
            waveBank_ = new WaveBank(audioEngine_, "Content/Audio/Wave Bank.xwb");
            soundBank_ = new SoundBank(audioEngine_, "Content/Audio/Sound Bank.xsb");
            Services.AddService(typeof(AudioEngine), audioEngine_);
            Services.AddService(typeof(SoundBank), soundBank_);

            spriteBatch_ = new SpriteBatch(GraphicsDevice);
            Services.AddService(typeof(SpriteBatch), spriteBatch_);
            Services.AddService(typeof(GraphicsDeviceManager), graphics_);
            base.Initialize();
        }

        protected override void Update(GameTime gameTime) {
            audioEngine_.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            GraphicsDevice.Clear(Color.Black);

            base.Draw(gameTime);
        }
    }
}
