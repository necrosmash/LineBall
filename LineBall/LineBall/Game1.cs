using System;
using System.Collections.Generic;
using System.Linq;

// Needed for Accelerometer
using Microsoft.Devices.Sensors;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;

// Additional Farseer libraries
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;

// Additional OpenXLive libraries
using OpenXLive;
using OpenXLive.Forms;
using OpenXLive.Features;

namespace LineBall
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        XLiveFormManager manager;

        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;

        //private SpriteFont DisplayedTime;
        private World world;

        // Bodies
        private Body playerBallBody;
        private Body groundBody;
        private Body obstacleBody;
        private Body arrowBody;

        private Texture2D circleSprite;

        private Matrix view;
        private Vector2 cameraPosition;
        private Vector2 screenCenter;
        private Vector2 circleOrigin;
        private Vector2 circlePos;//, timerPos;
        private Vector2 groundPosition, circlePosition, obstaclePosition, arrowPosition;

        private TimeSpan tScore, tFinalScore;

        private double dLastGameOverCheck;
        private bool bAlreadyJumped;

        private Vector2 objectPos, objectOrigin;

        List<WorldObject> worldObjects;

        // Accelerometer
        Accelerometer accelerometer;

        // Constants
        // Increase value below for higher speed gains on slopes
        private const float GravityCoef = 10f;
        //private const float ACC_SHADOW_COEF = 0.05f;
        private const float BallCoef = 0.02f;
        private const float MeterInPixels = 64f;
        //private AsyncEventHandler lb_SubmitScoreCompleted;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);

            // Set resolution for WP
            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 480;

            Content.RootDirectory = "Content";

            // Enable flick gestures
            TouchPanel.EnabledGestures = GestureType.Flick;

            // Instantiate world object
            world = new World(new Vector2(0, 20));

            // Frame rate is 30 fps by default for Windows Phone.
            TargetElapsedTime = TimeSpan.FromTicks(333333);

            // Extend battery life under lock.
            InactiveSleepTime = TimeSpan.FromSeconds(1);
        }

        void lb_SubmitScoreCompleted(object sender, AsyncEventArgs e)
        {
            if (e.Result.ReturnValue)
            {
                // Succeed
                XLiveScoreForm form = new XLiveScoreForm(this.manager);
                form.FormResultEvent += new EventHandler<ScoreFormResultEventArgs>(form_FormResultEvent);
                form.Show("6070f347-e93d-4463-9954-cb66fddeaf7e", tFinalScore);
            }
            else
            {
                OpenXLive.MessageBox.Show(e.Result.ErrorMessage);
            }
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // KEYHERE
            manager = new XLiveFormManager(this, "GBhq7N43Dp9Vt5HRUYQWF8ug");
            Components.Add(manager);
            
            // Manager events
            manager.NewGameEvent += new EventHandler(manager_NewGameEvent);
            
            worldObjects = new List<WorldObject>();

            dLastGameOverCheck = 0;

            base.Initialize();
        }

        void manager_NewGameEvent(object sender, EventArgs e)
        {
            // Circle
            // Convert screen centre from pixels to meters
            if (circlePosition != null)
            {
                circlePosition = new Vector2();
                circlePosition = (screenCenter / MeterInPixels) + new Vector2(0, -1.5f);
            }

            if (playerBallBody != null)
            {
                playerBallBody.Dispose();
                playerBallBody = BodyFactory.CreateCircle(world, 96f / (2f * MeterInPixels), 1f, circlePosition);
                playerBallBody.BodyType = BodyType.Dynamic;

                // Ball bounce and friction
                playerBallBody.Restitution = 0.3f;
                playerBallBody.Friction = 0.5f;
            }
        }
        
        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {   
            // Initialize camera controls
            view = Matrix.Identity;
            cameraPosition = Vector2.Zero;

            screenCenter = new Vector2(graphics.GraphicsDevice.Viewport.Width / 2f, graphics.GraphicsDevice.Viewport.Height / 2f);

            spriteBatch = new SpriteBatch(graphics.GraphicsDevice);

            XLiveStartupForm form = new XLiveStartupForm(this.manager);
            form.Show();

            // SpriteFont
            //DisplayedTime = Content.Load<SpriteFont>("DisplayedTime");

            world.ContactManager.OnBroadphaseCollision += OnBroadphaseCollision;

            // Generate the level to be played
            GenerateLevel();

            tScore = TimeSpan.FromMilliseconds(1);

            StartAccelerometer();
        }

        public void OnBroadphaseCollision(ref FixtureProxy fp1, ref FixtureProxy fp2)
        {
            bAlreadyJumped = false;
        }

        private void GenerateLevel()
        {
            XLiveStartupForm form = new XLiveStartupForm(this.manager);
            form.Show();

            circleSprite = Content.Load<Texture2D>("playerBallSprite");

            // Circle
            // Convert screen centre from pixels to meters
            circlePosition = (screenCenter / MeterInPixels) + new Vector2(0, -1.5f);

            // Create the circle fixture
            playerBallBody = BodyFactory.CreateCircle(world, 96f / (2f * MeterInPixels), 1f, circlePosition);
            playerBallBody.BodyType = BodyType.Dynamic;

            // Ball bounce and friction
            playerBallBody.Restitution = 0.3f;
            playerBallBody.Friction = 0.5f;

            // First piece of ground
            groundPosition = (screenCenter / MeterInPixels) + new Vector2(0f, 1.25f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);

            groundBody.IsStatic = true;
            groundBody.Restitution = 0.3f;
            groundBody.Friction = 0.5f;

            // Add it to the list
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            // First obstacle
            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(3.7f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);

            obstacleBody.IsStatic = true;
            obstacleBody.Restitution = 0.3f;
            obstacleBody.Friction = 0.5f;

            // Add it to the list
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite2"), obstacleBody, obstaclePosition));

            // Right arrow above starting point
            arrowPosition = (screenCenter / MeterInPixels) + new Vector2(0f, -3.3f);
            arrowBody = BodyFactory.CreateRectangle(world, 117f / MeterInPixels, 73f / MeterInPixels, 1f, arrowPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("arrow_right"), arrowBody, arrowPosition));

            // The rest of the obstacles & ground:
            groundPosition = (screenCenter / MeterInPixels) + new Vector2(-3.0f, -5.0f);
            groundBody = BodyFactory.CreateRectangle(world, 4f / MeterInPixels, 800f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("medWallSprite"), groundBody, groundPosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(7.4f, 1.25f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            // First staircase
            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(11.1f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(12.5f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(12.5f, -0.78f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(16.2f, 1.25f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            // Second staircase
            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(19.9f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(21.3f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(21.3f, -0.78f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(22.7f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(22.7f, -0.78f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(22.7f, -2.14f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(26.4f, 1.25f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            // Final staircase
            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(30.1f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(31.5f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(31.5f, -0.78f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(32.9f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(32.9f, -0.78f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(32.9f, -2.14f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(34.3f, 0.58f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(34.3f, -0.78f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(34.3f, -2.14f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(34.3f, -3.5f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            // Ground after staircase
            groundPosition = (screenCenter / MeterInPixels) + new Vector2(38f, 1.25f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            // Down arrows
            arrowPosition = (screenCenter / MeterInPixels) + new Vector2(48f, -3.0f);
            arrowBody = BodyFactory.CreateRectangle(world, 73f / MeterInPixels, 117f / MeterInPixels, 1f, arrowPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("arrow_down"), arrowBody, arrowPosition));

            // Far wall
            groundPosition = (screenCenter / MeterInPixels) + new Vector2(50f, 2f);
            groundBody = BodyFactory.CreateRectangle(world, 4f / MeterInPixels, 1700f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("largeWallSprite"), groundBody, groundPosition));

            // First lower ground
            groundPosition = (screenCenter / MeterInPixels) + new Vector2(47f, 15.35f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(41.1f, 15.35f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(35.2f, 15.35f);
            groundBody = BodyFactory.CreateRectangle(world, 380f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("groundSprite"), groundBody, groundPosition));

            // First lower obstacle
            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(31.7f, 14.68f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(31f, 11.3f);
            groundBody = BodyFactory.CreateRectangle(world, 4f / MeterInPixels, 498f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("smallWallSprite"), groundBody, groundPosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(36f, 7.0f);
            groundBody = BodyFactory.CreateRectangle(world, 4f / MeterInPixels, 672f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("smallWallSprite2"), groundBody, groundPosition));

            groundPosition = (screenCenter / MeterInPixels) + new Vector2(34.5f, 12f);
            groundBody = BodyFactory.CreateRectangle(world, 175f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("hangingGroundSprite2"), groundBody, groundPosition));

            obstaclePosition = (screenCenter / MeterInPixels) + new Vector2(35.3f, 11.3f);
            obstacleBody = BodyFactory.CreateRectangle(world, 96f / MeterInPixels, 96f / MeterInPixels, 1f, obstaclePosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("obstacleSprite"), obstacleBody, obstaclePosition));
            
            groundPosition = (screenCenter / MeterInPixels) + new Vector2(32f, 9.4f);
            groundBody = BodyFactory.CreateRectangle(world, 175f / MeterInPixels, 4f / MeterInPixels, 1f, groundPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("hangingGroundSprite2"), groundBody, groundPosition));

            arrowPosition = (screenCenter / MeterInPixels) + new Vector2(28f, 7.4f);
            arrowBody = BodyFactory.CreateRectangle(world, 73f / MeterInPixels, 117f / MeterInPixels, 1f, arrowPosition);
            worldObjects.Add(new WorldObject(Content.Load<Texture2D>("arrow_down"), arrowBody, arrowPosition));
        }

        private void StartAccelerometer()
        {
            //throw new NotImplementedException();

            if (accelerometer == null)
            {
                // Instantiate the accelerometer.
                accelerometer = new Accelerometer();
                
                accelerometer.ReadingChanged += new EventHandler<AccelerometerReadingEventArgs>(accelerometer_ReadingChanged);
            }

            try
            {
                accelerometer.Start();
            }
            catch (InvalidOperationException)
            {
                // Accelerometer failed to start
                //this.Exit();
            }
        }

        
        void accelerometer_ReadingChanged(object sender, AccelerometerReadingEventArgs e)
        {
            if (world != null)
            {
                world.Gravity = new Vector2((float)e.Y * -GravityCoef, 20f);
            }
        }
        

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        /*
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
            content.Unload();
        }
        */

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            {
                if (manager.IsRunning)
                {
                    XLivePauseForm form = new XLivePauseForm(this.manager);
                    form.Show();
                }
            }

            if (manager.IsRunning)
            {
                while (TouchPanel.IsGestureAvailable)
                {
                    GestureSample gesture = TouchPanel.ReadGesture();

                    if (gesture.GestureType == GestureType.Flick && !bAlreadyJumped)
                    {
                        bAlreadyJumped = true;
                        playerBallBody.ApplyForce(Vector2.Divide(gesture.Delta, 7.5f));//8.0f //6.0f //8.0f //4.0f //2.0f
                    }
                }

                view = Matrix.CreateTranslation(circleOrigin.X + (400 - 48) - circlePos.X, circleOrigin.Y + (240 - 48) - circlePos.Y, 0.0f);

                if (dLastGameOverCheck >= 1000)
                {
                    dLastGameOverCheck = 0;

                    if(circlePos.X > 600 && circlePos.X < 6000 && circlePos.Y > 2000)
                    {
                        LoadScore(gameTime);
                    }
                }
                else
                {
                    dLastGameOverCheck += gameTime.ElapsedGameTime.TotalMilliseconds;
                }

                // Update the world
                world.Step((float)gameTime.ElapsedGameTime.TotalMilliseconds * 0.001f);
            }
            
            base.Update(gameTime);
        }

        private void LoadScore(GameTime gameTime)
        {
            //tScore = gameTime.TotalGameTime;
            tFinalScore = gameTime.TotalGameTime;

            GraphicsDevice.Clear(Color.Black);
            playerBallBody.Dispose();
            
            foreach (WorldObject obj in worldObjects)
            {
                obj.body.Dispose();
            }

            // KEYHERE
            Leaderboard lb = new Leaderboard(this.manager.CurrentSession, "6070f347-e93d-4463-9954-cb66fddeaf7e");
            lb.SubmitScoreCompleted += new AsyncEventHandler(lb_SubmitScoreCompleted);
            //lb.SubmitScore(tScore, 5.0f, "Nice job!");
            lb.SubmitScore(gameTime.ElapsedGameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            if (manager.IsRunning)
            {
                GraphicsDevice.Clear(Color.Black);

                /* Circle position and rotation */
                // Convert physics position (meters) to screen coordinates (pixels)
                circlePos = playerBallBody.Position * MeterInPixels;
                float circleRotation = playerBallBody.Rotation;

                // Align sprite center to body position
                circleOrigin = new Vector2(circleSprite.Width / 2f, circleSprite.Height / 2f);

                spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, view);

                //Draw circle
                spriteBatch.Draw(circleSprite, circlePos, null, Color.White, circleRotation, circleOrigin, 1f, SpriteEffects.None, 0f);

                foreach (WorldObject obj in worldObjects)
                {
                    objectPos = obj.body.Position * MeterInPixels;
                    objectOrigin = new Vector2(obj.sprite.Width / 2f, obj.sprite.Height / 2f);

                    spriteBatch.Draw(obj.sprite, objectPos, null, Color.White, 0f, objectOrigin, 1f, SpriteEffects.None, 0f);
                }

                // Draw the displayed time
                //timerPos.X = circlePos.X - 350;
                //timerPos.Y = circlePos.Y - 200;

                //spriteBatch.DrawString(DisplayedTime, gameTime.ElapsedGameTime.Milliseconds.ToString(), timerPos, Color.White);
                //spriteBatch.DrawString(DisplayedTime, tScore.TotalMilliseconds.ToString(), timerPos, Color.White);
                spriteBatch.End();
            }
            base.Draw(gameTime);
        }

        void form_FormResultEvent(object sender, ScoreFormResultEventArgs e)
        {
            switch (e.Result)
            {
                case ScoreFormResult.None:
                    break;
                case ScoreFormResult.Retry:
                    break;
                case ScoreFormResult.NextLevel:
                    break;
                case ScoreFormResult.ExitGame:
                    break;
            }
        }

        /*
        void manager_PauseGameEvent(object sender, EventArgs e)
        {
            pauseCirPos = playerBallBody.Position;
            playerBallBody.Dispose();
        }

        void manager_ResumeGameEvent(object sender, EventArgs e)
        {
            if (playerBallBody != null)
                playerBallBody.Dispose();

            playerBallBody = BodyFactory.CreateCircle(world, 96f / (2f * MeterInPixels), 1f, pauseCirPos);
            playerBallBody.BodyType = BodyType.Dynamic;

            // Ball bounce and friction
            playerBallBody.Restitution = 0.3f;
            playerBallBody.Friction = 0.5f;
        }
        */
    }
}
