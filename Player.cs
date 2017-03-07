using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


namespace XNAPacMan {
    public struct Position {
        public Position(Point Tile, Point DeltaPixel) {
            this.Tile = Tile;
            this.DeltaPixel = DeltaPixel;
        }
        public Point Tile;
        public Point DeltaPixel;
    }

    public enum Direction { Up, Down, Left, Right };
    public enum State { Start, Normal, Dying };

    public class Player {

        public Player(Game game) {
            Reset();
            this.game = game;
            updatesPerPixel_ = Constants.PacManSpeed();
            spriteBatch_ = (SpriteBatch)game.Services.GetService(typeof(SpriteBatch));
            eatingFrames_ = new Texture2D[] {
                game.Content.Load<Texture2D>("sprites/PacManEating1"),
                game.Content.Load<Texture2D>("sprites/PacManEating2"),
                game.Content.Load<Texture2D>("sprites/PacManEating3"),
                game.Content.Load<Texture2D>("sprites/PacManEating4"),
                game.Content.Load<Texture2D>("sprites/PacManEating5"),
                game.Content.Load<Texture2D>("sprites/PacManEating6"),
                game.Content.Load<Texture2D>("sprites/PacManEating7"),
                game.Content.Load<Texture2D>("sprites/PacManEating8"),
                game.Content.Load<Texture2D>("sprites/PacManEating9"),
            };
            dyingFrames_ = game.Content.Load<Texture2D>("sprites/DyingSheetNew");
        }

        public void Update(GameTime gameTime) {
            Keys[] validKeys = { Keys.Up, Keys.Down, Keys.Left, Keys.Right };
            Keys[] pressedKeys = (from k in Keyboard.GetState().GetPressedKeys()
                                  where validKeys.Contains(k)
                                  select k).ToArray();

            if (pressedKeys.Length == 1) {
                if (state_ == State.Start) {
                    if (pressedKeys[0] == Keys.Left || pressedKeys[0] == Keys.Right) {
                        state_ = State.Normal;
                    }
                    if (pressedKeys[0] == Keys.Left) {
                        TryTurn(pressedKeys[0]);
                    }
                }
                else if ((direction_.ToString() != pressedKeys[0].ToString())) {
                    TryTurn(pressedKeys[0]);
                }
            }

            TryMove();
        }

        void TryMove() {
            if (state_ == State.Start) {
                return;
            }
            if (position_.DeltaPixel != Point.Zero) {
                DoMove();
            }
            else if ((position_.Tile == new Point(0, 14) && direction_ == Direction.Left) ||
                     (position_.Tile == new Point(27, 14) && direction_ == Direction.Right)) {
                DoMove();
            }
            else if ((direction_ == Direction.Up && Grid.TileGrid[position_.Tile.X, position_.Tile.Y - 1].Type == TileTypes.Open) ||
                      (direction_ == Direction.Down && Grid.TileGrid[position_.Tile.X, position_.Tile.Y + 1].Type == TileTypes.Open) ||
                      (direction_ == Direction.Left && Grid.TileGrid[position_.Tile.X - 1, position_.Tile.Y].Type == TileTypes.Open) ||
                      (direction_ == Direction.Right && Grid.TileGrid[position_.Tile.X + 1, position_.Tile.Y].Type == TileTypes.Open)) {
                DoMove();
            }

        }

        void DoMove() {
            updateCount_++;
            updateCount_ %= updatesPerPixel_;
            if (updateCount_ == 0) {

                if (Ghost.NextTile(direction_, position_).HasCrump) {
                    updatesPerPixel_ = Constants.PacManSpeed() + 2;
                }
                else {
                    updatesPerPixel_ = Constants.PacManSpeed();
                }

                if (direction_ == Direction.Up) {
                    position_.DeltaPixel.Y--;
                }
                else if (direction_ == Direction.Down) {
                    position_.DeltaPixel.Y++;
                }
                else if (direction_ == Direction.Left) {
                    position_.DeltaPixel.X--;
                }
                else if (direction_ == Direction.Right) {
                    position_.DeltaPixel.X++;
                }

                if (position_.DeltaPixel.X == 16) {
                    if (position_.Tile.X == 27) {
                        position_.Tile.X = 0;
                    }
                    else {
                        position_.Tile.X++;
                    }
                    position_.DeltaPixel.X = 0;
                }
                else if (position_.DeltaPixel.X == -16) {
                    if (position_.Tile.X == 0) {
                        position_.Tile.X = 27;
                    }
                    else {
                        position_.Tile.X--;
                    }
                    position_.DeltaPixel.X = 0;
                }
                else if (position_.DeltaPixel.Y == 16) {
                    position_.Tile.Y++;
                    position_.DeltaPixel.Y = 0;
                }
                else if (position_.DeltaPixel.Y == -16) {
                    position_.Tile.Y--;
                    position_.DeltaPixel.Y = 0;
                }
            }
        }

        public void Draw(GameTime gameTime, Vector2 boardPosition) {
            Vector2 position;
            position.X = boardPosition.X + (position_.Tile.X * 16) + position_.DeltaPixel.X - ((eatingFrames_[0].Width - 16) / 2);
            position.Y = boardPosition.Y + (position_.Tile.Y * 16) + position_.DeltaPixel.Y - ((eatingFrames_[0].Height - 16) / 2);

            if (state_ == State.Start) {
                spriteBatch_.Draw(eatingFrames_[0], position, Color.White);
            }

            else if (state_ == State.Normal) {
                int frame = Math.Abs(position_.DeltaPixel.X + position_.DeltaPixel.Y) / (16 / usedFramesIndex_.Length);
                frame = (int)MathHelper.Clamp(frame, 0, usedFramesIndex_.Length - 1);
                RenderSprite(eatingFrames_[usedFramesIndex_[frame]], null, boardPosition, position);
            }

            else if (state_ == State.Dying) {
                int timeBetweenFrames = 90;
                timer_ += gameTime.ElapsedGameTime;
                int index = (timer_.Seconds * 1000 + timer_.Milliseconds) / timeBetweenFrames;
                if (index > 19) {
                    return;
                }
                RenderSprite(dyingFrames_, new Rectangle(26 * index, 0, 26, 26), boardPosition, position);
            }

        }

        void RenderSprite(Texture2D spriteSheet, Rectangle? rectangle, Vector2 boardPosition, Vector2 position) {

            Rectangle rect = rectangle == null ? new Rectangle(0, 0, spriteSheet.Width, spriteSheet.Height) :
                                                rectangle.Value;

            if (position.X < boardPosition.X) {
                int deltaPixel = (int)(boardPosition.X - position.X);
                var leftPortion = new Rectangle(rect.X + deltaPixel, rect.Y, 26 - deltaPixel, 26);
                var leftPortionPosition = new Vector2(boardPosition.X, position.Y);
                var rightPortion = new Rectangle(rect.X, rect.Y, deltaPixel, 26);
                var rightPortionPosition = new Vector2(boardPosition.X + (16 * 28) - deltaPixel, position.Y);
                spriteBatch_.Draw(spriteSheet, leftPortionPosition, leftPortion, Color.White);
                spriteBatch_.Draw(spriteSheet, rightPortionPosition, rightPortion, Color.White);
            }
            else if (position.X > (boardPosition.X + (16 * 28) - 26)) {
                int deltaPixel = (int)((position.X + 26) - (boardPosition.X + (16 * 28)));
                var leftPortion = new Rectangle(rect.X + 26 - deltaPixel, rect.Y, deltaPixel, 26);
                var leftPortionPosition = new Vector2(boardPosition.X, position.Y);
                var rightPortion = new Rectangle(rect.X, rect.Y, 26 - deltaPixel, 26);
                var rightPortionPosition = new Vector2(position.X, position.Y);
                spriteBatch_.Draw(spriteSheet, leftPortionPosition, leftPortion, Color.White);
                spriteBatch_.Draw(spriteSheet, rightPortionPosition, rightPortion, Color.White);
            }
            else {
                spriteBatch_.Draw(spriteSheet, position, rect, Color.White);
            }
        }

        void Reset() {
            state_ = State.Start;
            direction_ = Direction.Right;
            usedFramesIndex_ = new int[] { 0, 1, 2 };
            position_ = new Position { Tile = new Point(13, 23), DeltaPixel = new Point(8, 0) };
            updateCount_ = 0;
        }

        void TryTurn(Keys input) {
            if (position_.DeltaPixel != Point.Zero) {
                if ((direction_ == Direction.Up && input == Keys.Down) ||
                    (direction_ == Direction.Down && input == Keys.Up) ||
                    (direction_ == Direction.Left && input == Keys.Right) ||
                    (direction_ == Direction.Right && input == Keys.Left)) {
                    DoTurn(input);
                }
            }
            else if ((input == Keys.Left && position_.Tile.X == 0) ||
                      (input == Keys.Right && position_.Tile.X == 27)) {
                DoTurn(input);
            }
            else if ((input == Keys.Up && Grid.TileGrid[position_.Tile.X, position_.Tile.Y - 1].Type == TileTypes.Open) ||
                      (input == Keys.Down && Grid.TileGrid[position_.Tile.X, position_.Tile.Y + 1].Type == TileTypes.Open) ||
                      (input == Keys.Left && Grid.TileGrid[position_.Tile.X - 1, position_.Tile.Y].Type == TileTypes.Open) ||
                      (input == Keys.Right && Grid.TileGrid[position_.Tile.X + 1, position_.Tile.Y].Type == TileTypes.Open)) {
                DoTurn(input);
            }

        }

        void DoTurn(Keys newDirection) {

            switch (newDirection) {
                case Keys.Up:
                    direction_ = Direction.Up;
                    usedFramesIndex_ = new int[] { 0, 7, 8 };
                    if (position_.DeltaPixel != Point.Zero) {
                        position_.Tile.Y += 1;
                        position_.DeltaPixel.Y -= 16;
                    }
                    break;
                case Keys.Down:
                    direction_ = Direction.Down;
                    usedFramesIndex_ = new int[] { 0, 3, 4 };
                    if (position_.DeltaPixel != Point.Zero) {
                        position_.Tile.Y -= 1;
                        position_.DeltaPixel.Y += 16;
                    }
                    break;
                case Keys.Left:
                    direction_ = Direction.Left;
                    usedFramesIndex_ = new int[] { 0, 5, 6 };
                    if (position_.DeltaPixel != Point.Zero) {
                        position_.Tile.X += 1;
                        position_.DeltaPixel.X -= 16;
                    }
                    break;
                case Keys.Right:
                    direction_ = Direction.Right;
                    usedFramesIndex_ = new int[] { 0, 1, 2 };
                    if (position_.DeltaPixel != Point.Zero) {
                        position_.Tile.X -= 1;
                        position_.DeltaPixel.X += 16;
                    }
                    break;
            }
        }

        Game game;
        TimeSpan timer_;
        SpriteBatch spriteBatch_;
        Texture2D dyingFrames_;
        Texture2D[] eatingFrames_;
        int[] usedFramesIndex_;
        int updatesPerPixel_;
        int updateCount_;
        Position position_;
        Direction direction_;
        State state_;

        public State State { get { return state_; } set { state_ = value; } }
        public Direction Direction { get { return direction_; } }
        public Position Position { get { return position_; } }
        
    }
}