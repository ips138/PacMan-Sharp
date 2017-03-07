using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


namespace XNAPacMan {
    struct ScoreEvent {
        public ScoreEvent(Position position, DateTime when, int score) {
            Position = position;
            When = when;
            Score = score;
        }
        public Position Position;
        public DateTime When;
        public int Score;
    }
    public class GameLoop : Microsoft.Xna.Framework.DrawableGameComponent {
        public GameLoop(Game game)
            : base(game) {
        }

        public override void Initialize() {
            if (spriteBatch_ != null) {
                GhostSoundsManager.ResumeLoops();
                return;
            }
            GhostSoundsManager.Init(Game);

            Grid.Reset();
            Constants.Level = 1;
            spriteBatch_ = (SpriteBatch)Game.Services.GetService(typeof(SpriteBatch));
            graphics_ = (GraphicsDeviceManager)Game.Services.GetService(typeof(GraphicsDeviceManager));
            soundBank_ = (SoundBank)Game.Services.GetService(typeof(SoundBank));

            scoreFont_ = Game.Content.Load<SpriteFont>("Score");
            scoreEventFont_ = Game.Content.Load<SpriteFont>("ScoreEvent");
            xlife_ = Game.Content.Load<Texture2D>("sprites/ExtraLife");
            ppill_ = Game.Content.Load<Texture2D>("sprites/PowerPill");
            crump_ = Game.Content.Load<Texture2D>("sprites/Crump");
            board_ = Game.Content.Load<Texture2D>("sprites/Board");
            boardFlash_ = Game.Content.Load<Texture2D>("sprites/BoardFlash");
            bonusEaten_ = new Dictionary<string, int>();
            bonus_ = new Dictionary<string, Texture2D>(9);
            bonus_.Add("Apple", Game.Content.Load<Texture2D>("bonus/Apple"));
            bonus_.Add("Banana", Game.Content.Load<Texture2D>("bonus/Banana"));
            bonus_.Add("Bell", Game.Content.Load<Texture2D>("bonus/Bell"));
            bonus_.Add("Cherry", Game.Content.Load<Texture2D>("bonus/Cherry"));
            bonus_.Add("Key", Game.Content.Load<Texture2D>("bonus/Key"));
            bonus_.Add("Orange", Game.Content.Load<Texture2D>("bonus/Orange"));
            bonus_.Add("Pear", Game.Content.Load<Texture2D>("bonus/Pear"));
            bonus_.Add("Pretzel", Game.Content.Load<Texture2D>("bonus/Pretzel"));
            bonus_.Add("Strawberry", Game.Content.Load<Texture2D>("bonus/Strawberry"));

            scoreEvents_ = new List<ScoreEvent>(5);
            bonusPresent_ = false;
            bonusSpawned_ = 0;
            eatenGhosts_ = 0;
            Score = 0;
            xlives_ = 2;
            paChomp_ = true;
            playerDied_ = false;
            player_ = new Player(Game);
            ghosts_ = new List<Ghost> { new Ghost(Game, player_, Ghosts.Blinky), new Ghost(Game, player_, Ghosts.Clyde),
                                        new Ghost(Game, player_, Ghosts.Inky), new Ghost(Game, player_, Ghosts.Pinky)};
            ghosts_[2].SetBlinky(ghosts_[0]);
            soundBank_.PlayCue("Intro");
            LockTimer = TimeSpan.FromMilliseconds(4500);

            base.Initialize();
        }

        public override void Update(GameTime gameTime) {
            if (DateTime.Now - eventTimer_ < LockTimer) {
                ghosts_.ForEach(i => i.LockTimer(gameTime));
                bonusSpawnedTime_ += gameTime.ElapsedGameTime;
                return;
            }
            scoreEvents_.RemoveAll(i => DateTime.Now - i.When > TimeSpan.FromSeconds(5));

            if (playerDied_) {
                xlives_--;
                if (xlives_ >= 0) {
                    playerDied_ = false;
                    player_ = new Player(Game);
                    ghosts_.ForEach(i => i.Reset(false, player_));
                    scoreEvents_.Clear();
                }
                else {
                    Menu.SaveHighScore(Score);
                    Game.Components.Add(new Menu(Game, null));
                    Game.Components.Remove(this);
                    GhostSoundsManager.StopLoops();
                    return;
                }
            }

            if (noCrumpsLeft()) {
                if (Constants.Level < 21) {
                    bonusSpawned_ = 0;
                    Grid.Reset();
                    player_ = new Player(Game);
                    ghosts_.ForEach(i => i.Reset(true, player_));
                    soundBank_.PlayCue("NewLevel");
                    LockTimer = TimeSpan.FromSeconds(2);
                    Constants.Level++;
                    return;
                }
                else {
                    Menu.SaveHighScore(Score);
                    Game.Components.Add(new Menu(Game, null));
                    Game.Components.Remove(this);
                    GhostSoundsManager.StopLoops();
                    return;
                }
            }

            Keys[] inputKeys = Keyboard.GetState().GetPressedKeys();
            if (inputKeys.Contains(Keys.Escape)) {
                Game.Components.Add(new Menu(Game, this));
                Game.Components.Remove(this);
                GhostSoundsManager.PauseLoops();
                return;
            }

            if (player_.Position.DeltaPixel == Point.Zero) {
                Point playerTile = player_.Position.Tile;
                if (Grid.TileGrid[playerTile.X, playerTile.Y].HasCrump) {
                    soundBank_.PlayCue(paChomp_ ? "PacMAnEat1" : "PacManEat2");
                    paChomp_ = !paChomp_;
                    Score += 10;
                    Grid.TileGrid[playerTile.X, playerTile.Y].HasCrump = false;
                    if (Grid.TileGrid[playerTile.X, playerTile.Y].HasPowerPill) {
                        Score += 40;
                        eatenGhosts_ = 0;
                        for (int i = 0; i < ghosts_.Count; i++) {
                            if (ghosts_[i].State == GhostState.Attack || ghosts_[i].State == GhostState.Scatter ||
                                ghosts_[i].State == GhostState.Blue) {
                                ghosts_[i].State = GhostState.Blue;
                            }
                        }
                        Grid.TileGrid[playerTile.X, playerTile.Y].HasPowerPill = false;
                    }

                    if (noCrumpsLeft()) {
                        GhostSoundsManager.StopLoops();
                        LockTimer = TimeSpan.FromSeconds(2);
                        return;
                    }
                }
            }

            if (bonusPresent_ && player_.Position.Tile.Y == 17 &&
                ((player_.Position.Tile.X == 13 && player_.Position.DeltaPixel.X == 8) ||
                  (player_.Position.Tile.X == 14 && player_.Position.DeltaPixel.X == -8))) {
                LockTimer = TimeSpan.FromSeconds(1.5);
                Score += Constants.BonusScores();
                scoreEvents_.Add(new ScoreEvent(player_.Position, DateTime.Now, Constants.BonusScores()));
                soundBank_.PlayCue("fruiteat");
                bonusPresent_ = false;
                if (bonusEaten_.ContainsKey(Constants.BonusSprite())) {
                    bonusEaten_[Constants.BonusSprite()]++;
                }
                else {
                    bonusEaten_.Add(Constants.BonusSprite(), 1);
                }
            }

            if (bonusPresent_ && ((DateTime.Now - bonusSpawnedTime_) > TimeSpan.FromSeconds(10))) {
                bonusPresent_ = false;
            }

            foreach (Ghost ghost in ghosts_) {
                Rectangle playerArea = new Rectangle((player_.Position.Tile.X * 16) + player_.Position.DeltaPixel.X,
                                                     (player_.Position.Tile.Y * 16) + player_.Position.DeltaPixel.Y,
                                                      26,
                                                      26);
                Rectangle ghostArea = new Rectangle((ghost.Position.Tile.X * 16) + ghost.Position.DeltaPixel.X,
                                                    (ghost.Position.Tile.Y * 16) + ghost.Position.DeltaPixel.Y,
                                                    22,
                                                    22);
                if (!Rectangle.Intersect(playerArea, ghostArea).IsEmpty) {
                    if (ghost.State == GhostState.Blue) {
                        GhostSoundsManager.StopLoops();
                        soundBank_.PlayCue("EatGhost");
                        ghost.State = GhostState.Dead;
                        eatenGhosts_++;
                        int bonus = (int)(100 * Math.Pow(2, eatenGhosts_));
                        Score += bonus;
                        scoreEvents_.Add(new ScoreEvent(ghost.Position, DateTime.Now, bonus));
                        LockTimer = TimeSpan.FromMilliseconds(900);
                        return;
                    }
                    else if (ghost.State != GhostState.Dead ) {
                        KillPacMan();
                        return;
                    }
                }
            }

            if ((Grid.NumCrumps == 180 || Grid.NumCrumps == 80) && bonusSpawned_ < 2 &&
                ! (player_.Position.Tile.Y == 17 &&
                    ((player_.Position.Tile.X == 13 && player_.Position.DeltaPixel.X == 8) ||
                    (player_.Position.Tile.X == 14 && player_.Position.DeltaPixel.X == -8)))) {
                bonusPresent_ = true;
                bonusSpawned_++;
                bonusSpawnedTime_ = DateTime.Now;

            }

            player_.Update(gameTime);
            ghosts_.ForEach(i => i.Update(gameTime));

            base.Update(gameTime);
        }

        bool noCrumpsLeft() {
            return Grid.NumCrumps == 0;
        }

        void KillPacMan() {
            player_.State = State.Dying;
            GhostSoundsManager.StopLoops();
            soundBank_.PlayCue("Death");
            LockTimer = TimeSpan.FromMilliseconds(1811);
            playerDied_ = true;
            bonusPresent_ = false;
            bonusSpawned_ = 0;
        }

        public override void Draw(GameTime gameTime) {
            base.Draw(gameTime);
            spriteBatch_.Begin();

            Vector2 boardPosition = new Vector2(
                (graphics_.PreferredBackBufferWidth / 2) - (board_.Width / 2),
                (graphics_.PreferredBackBufferHeight / 2) - (board_.Height / 2)
                );

            if (noCrumpsLeft()) {
                spriteBatch_.Draw(((DateTime.Now.Second * 1000 + DateTime.Now.Millisecond) / 350) % 2 == 0 ? board_ : boardFlash_, boardPosition, Color.White);
                player_.Draw(gameTime, boardPosition);
                spriteBatch_.End();
                return;
            }
            spriteBatch_.Draw(board_, boardPosition, Color.White);

            Tile[,] tiles = Grid.TileGrid;
            for (int j = 0; j < Grid.Height; j++) {
                for (int i = 0; i < Grid.Width; i++) {
                    if (tiles[i, j].HasPowerPill) {
                        spriteBatch_.Draw(ppill_, new Vector2(
                            boardPosition.X + 3 + (i * 16),
                            boardPosition.Y + 3 + (j * 16)),
                            Color.White);
                    }
                    else if (tiles[i, j].HasCrump) {
                        spriteBatch_.Draw(crump_, new Vector2(
                            boardPosition.X + 5 + (i * 16),
                            boardPosition.Y + 5 + (j * 16)),
                            Color.White);
                    }
                }
            }

            for (int i = 0; i < xlives_ && i < 20; i++) {
                spriteBatch_.Draw(xlife_, new Vector2(boardPosition.X + 10 + (20 * i), board_.Height + boardPosition.Y + 10), Color.White);
            }

            spriteBatch_.DrawString(scoreFont_, "SCORE", new Vector2(boardPosition.X + 30, boardPosition.Y - 50), Color.White);
            spriteBatch_.DrawString(scoreFont_, Score.ToString(), new Vector2(boardPosition.X + 30, boardPosition.Y - 30), Color.White);

            spriteBatch_.DrawString(scoreFont_, "LEVEL", new Vector2(boardPosition.X + board_.Width - 80, boardPosition.Y - 50), Color.White);
            spriteBatch_.DrawString(scoreFont_, Constants.Level.ToString(), new Vector2(boardPosition.X + board_.Width - 80, boardPosition.Y - 30), Color.White);

            if (bonusPresent_) {
                spriteBatch_.Draw(bonus_[Constants.BonusSprite()], new Vector2(boardPosition.X + (13 * 16) + 2, boardPosition.Y + (17 * 16) - 8), Color.White);
            }

            int k = 0;
            foreach (KeyValuePair<string, int> kvp in bonusEaten_) {
                for (int i = 0; i < kvp.Value; i++) {
                    spriteBatch_.Draw(bonus_[kvp.Key], new Vector2(boardPosition.X + 10 + (22 * (k + i)), board_.Height + boardPosition.Y + 22), Color.White);
                }
                k += kvp.Value; 
            }

            ghosts_.ForEach( i => i.Draw(gameTime, boardPosition));
            player_.Draw(gameTime, boardPosition);

            foreach (ScoreEvent se in scoreEvents_) {
                spriteBatch_.DrawString(scoreEventFont_, se.Score.ToString(), new Vector2(boardPosition.X + (se.Position.Tile.X * 16) + se.Position.DeltaPixel.X + 4,
                                                                                           boardPosition.Y + (se.Position.Tile.Y * 16) + se.Position.DeltaPixel.Y + 4), Color.White);            
            }

            if (player_.State == State.Start) {
                spriteBatch_.DrawString(scoreFont_, "GET READY!", new Vector2(boardPosition.X + (board_.Width / 2) - 58, boardPosition.Y + 273), Color.Yellow);
            }
            spriteBatch_.End();
        }



        Dictionary<string, Texture2D> bonus_;
        Texture2D xlife_;
        Texture2D board_;
        Texture2D boardFlash_;
        Texture2D crump_;
        Texture2D ppill_;
        SpriteFont scoreFont_;
        SpriteFont scoreEventFont_;
        SoundBank soundBank_;
        GraphicsDeviceManager graphics_;
        SpriteBatch spriteBatch_;

        List<Ghost> ghosts_;
        Player player_;
        TimeSpan lockTimer_;
        DateTime eventTimer_;
        int bonusSpawned_;
        bool bonusPresent_;
        DateTime bonusSpawnedTime_;
        Dictionary<string, int> bonusEaten_;
        bool playerDied_;
        bool paChomp_;
        int xlives_;
        int score_;
        int eatenGhosts_;
        List<ScoreEvent> scoreEvents_;

        public int Score {
            get { return score_; }
            private set {
                if ((value / 10000) > (score_ / 10000)) {
                    soundBank_.PlayCue("ExtraLife");
                    xlives_++;
                }
                score_ = value; 
            }
        }

        private TimeSpan LockTimer {
            get { return lockTimer_; }
            set { eventTimer_ = DateTime.Now; lockTimer_ = value; }
        }
    }
}
