using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;


namespace XNAPacMan {

    public enum GhostState { Home, Scatter, Attack, Blue, Dead }
    public enum Ghosts { Blinky, Pinky, Inky, Clyde }


    public class Ghost {
        public Ghost(Game game, Player player, Ghosts identity) {
            spriteBatch_ = (SpriteBatch)game.Services.GetService(typeof(SpriteBatch));
            ghostBase1_ = game.Content.Load<Texture2D>("sprites/GhostBase");
            ghostBase2_ = game.Content.Load<Texture2D>("sprites/GhostBase2");
            ghostChased_ = game.Content.Load<Texture2D>("sprites/GhostChased");
            eyesBase_ = game.Content.Load<Texture2D>("sprites/GhostEyes");
            eyesCenter_ = game.Content.Load<Texture2D>("sprites/GhostEyesCenter");
            colorBase_ = Constants.colors(identity);
            identity_ = identity;
            previousNumCrumps_ = 0;
            Reset(true, player);
            wiggle_ = true;
            direction_ = new Direction();
            lastJunction_ = new Point();
            scatterTiles_ = Constants.scatterTiles(identity);
        }

        public void Reset(bool newLevel, Player player) {
            State = GhostState.Home;
            previousState_ = GhostState.Home;
            updateCount_ = 0;
            initialJumps_ = Constants.InitialJumps(identity_, newLevel);
            position_ = Constants.startPosition(identity_);
            scheduleStateEval_ = true;
            lastJunction_ = new Point();
            player_ = player;
            scatterModesLeft_ = 4;
            UpdateSpeed();
        }

        public void LockTimer(GameTime gameTime) {
            timeInCurrentState += gameTime.ElapsedGameTime;
        }

        public void SetBlinky(Ghost blinky) {
            blinky_ = blinky;
        }

        public void Update(GameTime gameTime) {
            if (scheduleStateEval_) {
                StateEval();
            }
            if (position_.DeltaPixel == Point.Zero && IsAJunction(position_.Tile)) {
                lastJunction_ = position_.Tile;
            }
            Move();
        }


        void PlaySound() {
            if (State == GhostState.Attack || State == GhostState.Scatter || State == GhostState.Home) {
                if (Grid.NumCrumps < 50) {
                    GhostSoundsManager.playLoopAttackVeryFast();
                } else if (Grid.NumCrumps < 120) {
                    GhostSoundsManager.playLoopAttackFast();
                } else {
                    GhostSoundsManager.playLoopAttack();
                }
            } else if (State == GhostState.Blue) {
                GhostSoundsManager.playLoopBlue();
            } else if (State == GhostState.Dead) {
                GhostSoundsManager.playLoopDead();
            }
        }

        void StateEval() {

            GhostState initialState = State;
            scheduleStateEval_ = false;

            switch (State) {
                case GhostState.Home:
                    if (position_.Tile.Y == 11 && position_.DeltaPixel.Y == 0) {
                        if (Constants.scatterTiles(identity_)[0].X < 13) {
                            direction_ = Direction.Left;
                        } else {
                            direction_ = Direction.Right;
                        }
                        if (scatterModesLeft_ > 0) {
                            State = GhostState.Scatter;
                        } else {
                            State = GhostState.Attack;
                        }
                        return;
                    }
                    else if (position_.Tile.X == 13 && position_.DeltaPixel.X == 8) {
                        direction_ = Direction.Up;
                    }
                    else if ((position_.DeltaPixel.Y == 8) &&
                            ((position_.Tile.X == 11 && position_.DeltaPixel.X == 8) ||
                             (position_.Tile.X == 15 && position_.DeltaPixel.X == 8))) {
                        if (position_.Tile.Y == 14) {
                            initialJumps_--;
                            if (initialJumps_ == 0) {
                                if (position_.Tile.X == 11) {
                                    direction_ = Direction.Right;
                                } else {
                                    direction_ = Direction.Left;
                                }
                            } else {
                                direction_ = Direction.Up;
                            }
                        } else if (position_.Tile.Y == 13) {
                            direction_ = Direction.Down;
                        }
                    }
                    break;
                case GhostState.Scatter:
                    if (previousState_ == GhostState.Attack) {
                        scatterModesLeft_--;
                        if (NextTile(OppositeDirection(direction_)).IsOpen) {
                            direction_ = OppositeDirection(direction_);
                        }
                    }
                    AIScatter();
                    int timeInScatterMode = scatterModesLeft_ <= 2 ? 5 : 7;
                    if ((DateTime.Now - timeInCurrentState) > TimeSpan.FromSeconds(timeInScatterMode)) {
                        State = GhostState.Attack;
                    }
                    break;
                case GhostState.Dead:
                    if (previousState_ != GhostState.Dead && previousState_ != GhostState.Blue) {
                        if (NextTile(OppositeDirection(direction_)).IsOpen) {
                            direction_ = OppositeDirection(direction_);
                        }
                    } else {
                        AIDead();
                    }
                    if (position_.DeltaPixel.X == 8 && position_.DeltaPixel.Y == 8) {
                        if (position_.Tile.Y == 14) {
                            State = GhostState.Home;
                        }
                    }
                    break;
                case GhostState.Attack:
                    if (previousState_ != GhostState.Attack && previousState_ != GhostState.Blue) {
                        if (NextTile(OppositeDirection(direction_)).IsOpen) {
                            direction_ = OppositeDirection(direction_);
                        }
                    } else {
                        AIAttack();
                    }

                    if ((DateTime.Now - timeInCurrentState) > TimeSpan.FromSeconds(20)) {
                        State = GhostState.Scatter;
                    }
                    break;
                case GhostState.Blue:
                    if (previousState_ != GhostState.Blue) {
                        if (NextTile(OppositeDirection(direction_)).IsOpen) {
                            direction_ = OppositeDirection(direction_);
                        }
                    } else {
                        AIAttack();
                    }

                    if ((DateTime.Now - timeInCurrentState) > TimeSpan.FromSeconds(Constants.BlueTime())) {
                        State = GhostState.Attack;
                    }
                    break;
            }

            if ((initialState != previousState_) ||
                (Grid.NumCrumps == 199 && previousNumCrumps_ == 200) ||
                (Grid.NumCrumps == 19 && previousNumCrumps_ == 20)) {
                PlaySound();
            }
            previousState_ = initialState;
        }

        void Move() {
            updateCount_++;
            updateCount_ %= updatesPerPixel_;
            if (updateCount_ == 0) {
                scheduleStateEval_ = true;
                UpdateSpeed();

                switch (direction_) {
                    case Direction.Up:
                        position_.DeltaPixel.Y--;
                        if (position_.DeltaPixel.Y < 0) {
                            position_.DeltaPixel.Y = 15;
                            position_.Tile.Y--;
                        }
                        break;
                    case Direction.Down:
                        position_.DeltaPixel.Y++;
                        if (position_.DeltaPixel.Y > 15) {
                            position_.DeltaPixel.Y = 0;
                            position_.Tile.Y++;
                        }
                        break;
                    case Direction.Left:
                        position_.DeltaPixel.X--;
                        if (position_.DeltaPixel.X < 0) {
                            position_.DeltaPixel.X = 15;
                            position_.Tile.X--;
                            if (position_.Tile.X < 0) {
                                position_.Tile.X = Grid.Width - 1;
                            }
                        }
                        break;
                    case Direction.Right:
                        position_.DeltaPixel.X++;
                        if (position_.DeltaPixel.X > 15) {
                            position_.DeltaPixel.X = 0;
                            position_.Tile.X++;
                            if (position_.Tile.X > Grid.Width - 1) {
                                position_.Tile.X = 0;
                            }
                        }
                        break;
                }
            }
        }

        void UpdateSpeed() {
            int baseSpeed = Constants.GhostSpeed();
            if (State == GhostState.Home) {
                updatesPerPixel_ = baseSpeed * 2;
            } else if (State == GhostState.Blue) {
                updatesPerPixel_ = (int)(baseSpeed * 1.5);
            } else if (identity_ == Ghosts.Blinky && Grid.NumCrumps <= Constants.CruiseElroyTimer()) {
                updatesPerPixel_ = baseSpeed - 1;
            }
              else if (position_.Tile.Y == 14 &&
                  ((0 <= position_.Tile.X && position_.Tile.X <= 5) ||
                    (22 <= position_.Tile.X && position_.Tile.X <= 27))) {
                updatesPerPixel_ = baseSpeed + 5;
            } else {
                updatesPerPixel_ = baseSpeed;
            }

        }

        void AIScatter() {
            if (position_.DeltaPixel != Point.Zero || !IsAJunction(position_.Tile)) {
                return;
            }

            if (AIOverride()) {
                return;
            }

            int favoredTile = scatterTiles_.FindIndex(i => i == position_.Tile);
            int nextFavoredTile = (favoredTile + 1) % (scatterTiles_.Count);
            if (favoredTile != -1) {
                direction_ = FindDirection(scatterTiles_[nextFavoredTile]);
            }
            else if (!scatterTiles_.Contains(lastJunction_)) {
                List<Point> orderedTiles = scatterTiles_.ToList();
                orderedTiles.Sort((a, b) => Vector2.Distance(new Vector2(position_.Tile.X, position_.Tile.Y),
                                                             new Vector2(a.X, a.Y)).
                                            CompareTo(
                                            Vector2.Distance(new Vector2(position_.Tile.X, position_.Tile.Y),
                                                             new Vector2(b.X, b.Y))));
                direction_ = FindDirection(orderedTiles[0]);
            }
        }

        void AIAttack() {
            if (position_.DeltaPixel != Point.Zero || !IsAJunction(position_.Tile)) {
                return;
            }

            if (AIOverride()) {
                return;
            }

            switch (identity_) {
                case Ghosts.Blinky:
                    AttackAIBlinky();
                    break;
                case Ghosts.Pinky:
                    AttackAIPinky();
                    break;
                case Ghosts.Inky:
                    AttackAIInky();
                    break;
                case Ghosts.Clyde:
                    AttackAIClyde();
                    break;
                default:
                    throw new ArgumentException();
            }
        }

        void AIDead() {

            if (position_.Tile.Y == 11 && position_.Tile.X == 13 && position_.DeltaPixel.X == 8) {
                direction_ = Direction.Down;
            }
            else if (position_.DeltaPixel == Point.Zero) {
                if (position_.Tile.X == 13 && position_.Tile.Y == 11) {
                    direction_ = Direction.Right;
                } else if (position_.Tile.X == 14 && position_.Tile.Y == 11) {
                    direction_ = Direction.Left;
                }
                else if (IsAJunction(position_.Tile)) {
                    if (position_.Tile.X > 13) {
                        direction_ = FindDirection(new Point(14, 11));
                    } else {
                        direction_ = FindDirection(new Point(13, 11));
                    }
                }
            }
        }

        void AttackAIBlinky() {
            direction_ = FindDirection(player_.Position.Tile);
        }

        void AttackAIPinky() {
            Tile nextTile = NextTile(player_.Direction, player_.Position);
            Tile nextNextTile = NextTile(player_.Direction, new Position(nextTile.ToPoint, Point.Zero));
            direction_ = FindDirection(nextNextTile.ToPoint);
        }

        void AttackAIInky() {
            Tile nextTile = NextTile(player_.Direction, player_.Position);
            Tile nextNextTile = NextTile(player_.Direction, new Position(nextTile.ToPoint, Point.Zero));
            Vector2 line = new Vector2(blinky_.Position.Tile.X - nextNextTile.ToPoint.X, blinky_.Position.Tile.Y - nextNextTile.ToPoint.Y);
            line *= 2;
            Point destination = new Point(position_.Tile.X + (int)line.X, position_.Tile.Y + (int)line.Y);
            destination.X = (int)MathHelper.Clamp(destination.X, 0, Grid.Width - 1);
            destination.Y = (int)MathHelper.Clamp(destination.Y, 0, Grid.Height - 1);
            direction_ = FindDirection(destination);
        }

        void AttackAIClyde() {
            float distanceToPlayer = Vector2.Distance(
                new Vector2(player_.Position.Tile.X, player_.Position.Tile.Y),
                new Vector2(position_.Tile.X, position_.Tile.Y));
            if (distanceToPlayer >= 8) {
                AttackAIBlinky();
            } else {
                AIScatter();
            }
        }

        bool AIOverride() {
            if (position_.Tile.Y == 11 && (position_.Tile.X == 12 || position_.Tile.X == 15)) {
                return (direction_ == Direction.Right || direction_ == Direction.Left);
            } else if (position_.Tile.Y == 20 && (position_.Tile.X == 9 || position_.Tile.X == 18)) {
                return (direction_ == Direction.Right || direction_ == Direction.Left);
            } else {
                return false;
            }
        }

        Direction OppositeDirection(Direction d) {
            switch (d) {
                case Direction.Up:
                    return Direction.Down;
                case Direction.Down:
                    return Direction.Up;
                case Direction.Left:
                    return Direction.Right;
                case Direction.Right:
                    return Direction.Left;
                default:
                    throw new ArgumentException();
            }
        }

        Direction FindDirection(Point destination) {
            int xDistance = destination.X - position_.Tile.X;
            int yDistance = destination.Y - position_.Tile.Y;

            var directions = new List<Direction>(4);

            if (Math.Abs(xDistance) > Math.Abs(yDistance)) {
                if (xDistance > 0) {
                    if (yDistance < 0) {
                        directions = new List<Direction> { Direction.Right, Direction.Up, Direction.Down, Direction.Left };
                    } else {
                        directions = new List<Direction> { Direction.Right, Direction.Down, Direction.Up, Direction.Left };
                    }
                } else {
                    if (yDistance < 0) {
                        directions = new List<Direction> { Direction.Left, Direction.Up, Direction.Down, Direction.Right };
                    } else {
                        directions = new List<Direction> { Direction.Left, Direction.Down, Direction.Up, Direction.Right };
                    }
                }
            } else {
                if (xDistance > 0) {
                    if (yDistance < 0) {
                        directions = new List<Direction> { Direction.Up, Direction.Right, Direction.Left, Direction.Down };
                    } else {
                        directions = new List<Direction> { Direction.Down, Direction.Right, Direction.Left, Direction.Up };
                    }
                } else {
                    if (yDistance < 0) {
                        directions = new List<Direction> { Direction.Up, Direction.Left, Direction.Right, Direction.Down };
                    } else {
                        directions = new List<Direction> { Direction.Down, Direction.Left, Direction.Right, Direction.Up };
                    }
                }
            }

            int index = directions.FindIndex(i => i != OppositeDirection(direction_) && NextTile(i).IsOpen);
            if (index != -1) {
                return directions[index];
            } else {
                return directions.Find(i => NextTile(i).IsOpen);
            }
        }

        Tile NextTile(Direction d) {
            return NextTile(d, position_);
        }

        public static Tile NextTile(Direction d, Position p) {
            switch (d) {
                case Direction.Up:
                    if (p.Tile.Y - 1 < 0) {
                        return Grid.TileGrid[p.Tile.X, p.Tile.Y];
                    } else {
                        return Grid.TileGrid[p.Tile.X, p.Tile.Y - 1];
                    }
                case Direction.Down:
                    if (p.Tile.Y + 1 >= Grid.Height) {
                        return Grid.TileGrid[p.Tile.X, p.Tile.Y];
                    } else {
                        return Grid.TileGrid[p.Tile.X, p.Tile.Y + 1];
                    }
                case Direction.Left:
                    if (p.Tile.X == 0) {
                        return Grid.TileGrid[Grid.Width - 1, p.Tile.Y];
                    } else {
                        return Grid.TileGrid[p.Tile.X - 1, p.Tile.Y];
                    }
                case Direction.Right:
                    if (p.Tile.X + 1 >= Grid.Width) {
                        return Grid.TileGrid[0, p.Tile.Y];
                    } else {
                        return Grid.TileGrid[p.Tile.X + 1, p.Tile.Y];
                    }
                default:
                    throw new ArgumentException();
            }
        }

        bool IsAJunction(Point tile) {
            if (NextTile(direction_).Type == TileTypes.Open) {
                if (direction_ == Direction.Up || direction_ == Direction.Down) {
                    return ((NextTile(Direction.Left).IsOpen) ||
                            (NextTile(Direction.Right).IsOpen));
                } else {
                    return ((NextTile(Direction.Up).IsOpen) ||
                            (NextTile(Direction.Down).IsOpen));
                }
            }
            else {
                return true;
            }
        }

        public void Draw(GameTime gameTime, Vector2 boardPosition) {
            if (((DateTime.Now.Millisecond / 125) % 2) == 0 ^ wiggle_) {
                wiggle_ = !wiggle_;
            }
            Vector2 position;
            position.X = boardPosition.X + (position_.Tile.X * 16) + (position_.DeltaPixel.X) - 6;
            position.Y = boardPosition.Y + (position_.Tile.Y * 16) + (position_.DeltaPixel.Y) - 6;
            Vector2 eyesBasePosition;
            eyesBasePosition.X = position.X + 4;
            eyesBasePosition.Y = position.Y + 6;
            Vector2 eyesCenterPosition = new Vector2();
            switch (direction_) {
                case Direction.Up:
                    eyesBasePosition.Y -= 2;
                    eyesCenterPosition.X = eyesBasePosition.X + 2;
                    eyesCenterPosition.Y = eyesBasePosition.Y;
                    break;
                case Direction.Down:
                    eyesBasePosition.Y += 2;
                    eyesCenterPosition.X = eyesBasePosition.X + 2;
                    eyesCenterPosition.Y = eyesBasePosition.Y + 6;
                    break;
                case Direction.Left:
                    eyesBasePosition.X -= 2;
                    eyesCenterPosition.X = eyesBasePosition.X;
                    eyesCenterPosition.Y = eyesBasePosition.Y + 3;
                    break;
                case Direction.Right:
                    eyesBasePosition.X += 2;
                    eyesCenterPosition.X = eyesBasePosition.X + 4;
                    eyesCenterPosition.Y = eyesBasePosition.Y + 3;
                    break;
            }
            if (State == GhostState.Blue) {
                if (((DateTime.Now - timeInCurrentState).Seconds < 0.5 * Constants.BlueTime())) {
                    RenderSprite(wiggle_ ? ghostBase1_ : ghostBase2_, null, boardPosition, position, Color.Blue);
                    RenderSprite(ghostChased_, null, boardPosition, position, Color.White);
                } else {
                    bool flash = (DateTime.Now.Second + DateTime.Now.Millisecond / 200) % 2 == 0;
                    RenderSprite(wiggle_ ? ghostBase1_ : ghostBase2_, null, boardPosition, position, flash ? Color.Blue : Color.White);
                    RenderSprite(ghostChased_, null, boardPosition, position, flash ? Color.White : Color.Blue);
                }
            } else if (State == GhostState.Dead) {
                RenderSprite(eyesBase_, null, boardPosition, eyesBasePosition, Color.White);
                RenderSprite(eyesCenter_, null, boardPosition, eyesCenterPosition, Color.White);
            } else {
                RenderSprite(wiggle_ ? ghostBase1_ : ghostBase2_, null, boardPosition, position, colorBase_);
                RenderSprite(eyesBase_, null, boardPosition, eyesBasePosition, Color.White);
                RenderSprite(eyesCenter_, null, boardPosition, eyesCenterPosition, Color.White);
            }

        }

        void RenderSprite(Texture2D spriteSheet, Rectangle? rectangle, Vector2 boardPosition, Vector2 position, Color color) {

            Rectangle rect = rectangle == null ? new Rectangle(0, 0, spriteSheet.Width, spriteSheet.Height) :
                                                rectangle.Value;
            int textureWidth = rectangle == null ? spriteSheet.Width : rectangle.Value.Width;
            int textureHeight = rectangle == null ? spriteSheet.Height : rectangle.Value.Height;

            if (position.X < boardPosition.X) {
                int deltaPixel = (int)(boardPosition.X - position.X);
                var leftPortion = new Rectangle(rect.X + deltaPixel, rect.Y, textureWidth - deltaPixel, textureHeight);
                var leftPortionPosition = new Vector2(boardPosition.X, position.Y);
                var rightPortion = new Rectangle(rect.X, rect.Y, deltaPixel, textureHeight);
                var rightPortionPosition = new Vector2(boardPosition.X + (16 * 28) - deltaPixel, position.Y);
                spriteBatch_.Draw(spriteSheet, leftPortionPosition, leftPortion, color);
                spriteBatch_.Draw(spriteSheet, rightPortionPosition, rightPortion, color);
            }
            else if (position.X > (boardPosition.X + (16 * 28) - textureWidth)) {
                int deltaPixel = (int)((position.X + textureWidth) - (boardPosition.X + (16 * 28)));
                var leftPortion = new Rectangle(rect.X + textureWidth - deltaPixel, rect.Y, deltaPixel, textureHeight);
                var leftPortionPosition = new Vector2(boardPosition.X, position.Y);
                var rightPortion = new Rectangle(rect.X, rect.Y, textureWidth - deltaPixel, textureHeight);
                var rightPortionPosition = new Vector2(position.X, position.Y);
                spriteBatch_.Draw(spriteSheet, leftPortionPosition, leftPortion, color);
                spriteBatch_.Draw(spriteSheet, rightPortionPosition, rightPortion, color);
            }
            else {
                spriteBatch_.Draw(spriteSheet, position, rect, color);
            }
        }

        SpriteBatch spriteBatch_;
        Texture2D ghostBase1_;
        Texture2D ghostBase2_;
        Texture2D ghostChased_;
        Texture2D eyesBase_;
        Texture2D eyesCenter_;
        Color colorBase_;
        bool wiggle_;
        
        Ghost blinky_;
        Ghosts identity_;
        Direction direction_;
        Position position_;
        GhostState state_;
        GhostState previousState_;
        List<Point> scatterTiles_;
        Point lastJunction_;
        DateTime timeInCurrentState;
        Player player_;
        int updatesPerPixel_;
        bool scheduleStateEval_;
        int scatterModesLeft_;
        int initialJumps_;
        int previousNumCrumps_;
        int updateCount_;

        public GhostState State { get { return state_; } set { state_ = value; timeInCurrentState = DateTime.Now; } }
        public Position Position { get { return position_; } }
        public Ghosts Identity { get { return identity_; } }
    }
}