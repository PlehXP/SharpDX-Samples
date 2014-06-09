// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;

using SharpDX;
using SharpDX.Toolkit;

namespace MultiCube
{
    // Use this namespace here in case we need to use Direct3D11 namespace as well, as this
    // namespace will override the Direct3D11.
    using SharpDX.Toolkit.Graphics;
    using SharpDX.Direct3D11;
    using System.Threading.Tasks;
    using System.Reflection;
using System.Diagnostics;
    using SharpDX.Direct3D;
    using SharpDX.Toolkit.Input;

    /// <summary>
    /// Simple MultiCube application using SharpDX.Toolkit.
    /// The purpose of this application is to show a rotating cube using <see cref="BasicEffect"/>.
    /// </summary>
    public class MultiCubeGame : Game
    {
        /// <summary>
        /// State used to store testcase values.
        /// </summary>
        enum TestType
        {
            Immediate = 0,
            Deferred = 1,
            FrozenDeferred = 2
        }

        struct State
        {
            public bool Exit;
            public int CountCubes;
            public int ThreadCount;
            public TestType Type;
            public bool SimulateCpuUsage;
            public bool UseMap;
        }

        const int MaxNumberOfCubes = 256;
        const int MaxNumberOfThreads = 16;
        const int BurnCpuFactor = 50;
        const float viewZ = 5.0f;

        private State currentState, nextState;
        bool switchToNextState = false;

        private GraphicsDevice[] graphicsDevicePerThread;
        private DeviceContext[] deviceContextPerThread;
        private BasicEffect[] effectPerThread;
        private GraphicsDevice[] deferredGraphicsDevices;
        private DeviceContext[] deferredDeviceContexts;
        private CommandList[] commandLists;
        private DeviceContext immediateContext;
        private BasicEffect basicEffect;

        // Prepare matrices 
        Matrix view;
        Matrix proj;
        Matrix viewProj;

        private GraphicsDeviceManager graphicsDeviceManager;
        private Buffer<VertexPositionColor> vertices;
        private VertexInputLayout inputLayout;

        Stopwatch fpsTimer = new Stopwatch();
        int fpsCounter = 0;

        bool supportConcurentResources;
        bool supportCommandList;

        DepthStencilView depthStencilView;
        RenderTargetView[] renderTargets;

        private readonly KeyboardManager _keyboardManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiCubeGame" /> class.
        /// </summary>
        public MultiCubeGame()
        {
            // Creates a graphics manager. This is mandatory.
            graphicsDeviceManager = new GraphicsDeviceManager(this);

            graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;

            // Setup the relative directory to the executable directory
            // for loading contents with the ContentManager
            Content.RootDirectory = "Content";

            // initialize the keyboard manager
            _keyboardManager = new KeyboardManager(this);
        }

        protected override void LoadContent()
        {
            basicEffect = ToDisposeContent(new BasicEffect(GraphicsDevice.MainDevice)
            {
                VertexColorEnabled = true,
                View = Matrix.LookAtRH(new Vector3(0, 0, 5), new Vector3(0, 0, 0), Vector3.UnitY),
                Projection = Matrix.PerspectiveFovRH((float)Math.PI / 4.0f, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 100.0f),
                World = Matrix.Identity
            });

            for (int i = 0; i < deferredGraphicsDevices.Length; i++)
                effectPerThread[i] = 
                // Creates a basic effect
                ToDisposeContent(new BasicEffect(graphicsDevicePerThread[i])
                {
                    VertexColorEnabled = true,
                    View = Matrix.LookAtRH(new Vector3(0, 0, 5), new Vector3(0, 0, 0), Vector3.UnitY),
                    Projection = Matrix.PerspectiveFovRH((float)Math.PI / 4.0f, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 100.0f),
                    World = Matrix.Identity
                });

            // Creates vertices for the cube
            vertices = ToDisposeContent(SharpDX.Toolkit.Graphics.Buffer.Vertex.New(
                GraphicsDevice,
                new[]
                    {
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, 1.0f), Color.Orange), // Back
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, 1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, 1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, 1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, 1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, 1.0f), Color.Orange),

                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, -1.0f), Color.Orange), // Front
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, -1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, -1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, -1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, -1.0f), Color.Orange),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, -1.0f), Color.Orange),

                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, 1.0f), Color.OrangeRed), // Top
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, -1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, -1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, 1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, -1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, 1.0f), Color.OrangeRed),

                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, 1.0f), Color.OrangeRed), // Bottom
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, -1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, -1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, 1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, 1.0f), Color.OrangeRed),
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, -1.0f), Color.OrangeRed),

                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, 1.0f), Color.DarkOrange), // Left
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, -1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, -1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(-1.0f, -1.0f, 1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, -1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(-1.0f, 1.0f, 1.0f), Color.DarkOrange),

                        new VertexPositionColor(new Vector3(1.0f, -1.0f, 1.0f), Color.DarkOrange), // Right
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, -1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, -1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(1.0f, -1.0f, 1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, 1.0f), Color.DarkOrange),
                        new VertexPositionColor(new Vector3(1.0f, 1.0f, -1.0f), Color.DarkOrange),
                    }));
            ToDisposeContent(vertices);

            // Create an input layout from the vertices
            inputLayout = VertexInputLayout.FromBuffer(0, vertices);

            base.LoadContent();
        }

        protected override void Initialize()
        {
            Window.Title = "MultiCube demo";

            // Initial state
            currentState = new State
            {
                // Set the number of cubes to display (horizontally and vertically) 
                CountCubes = 64,
                // Number of threads to run concurrently 
                ThreadCount = 1,
                // Use deferred by default
                Type = TestType.Deferred,
                // BurnCpu by default
                SimulateCpuUsage = true,
                // Default is using Map/Unmap
                UseMap = true,
            };
            nextState = currentState;




            
            renderTargets = GraphicsDevice.GetRenderTargets(out depthStencilView);
            

            // PreCreate deferred contexts 
            deferredGraphicsDevices = new GraphicsDevice[MaxNumberOfThreads];
            for (int i = 0; i < deferredGraphicsDevices.Length; i++)
            {
                var deferred = ToDispose(GraphicsDevice.NewDeferred());

                
                //deferred.SetRasterizerState(GraphicsDevice.RasterizerStates.Default);
                //deferred.AutoViewportFromRenderTargets = true;
                //deferred.SetViewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
                //deferred.Viewport = GraphicsDevice.MainDevice.Viewport;
                //deferred.Presenter = GraphicsDevice.MainDevice.Presenter;


                //var presentationParameters = new PresentationParameters(Window.ClientBounds.Width, Window.ClientBounds.Height, Window.NativeWindow, GraphicsDevice.Presenter.Description.BackBufferFormat) { DepthStencilFormat = GraphicsDevice.Presenter.Description.DepthStencilFormat };
                //presentationParameters.PresentationInterval = PresentInterval.One;
                //deferred.Presenter = new SwapChainGraphicsPresenter(GraphicsDevice, presentationParameters);

                deferredGraphicsDevices[i] = deferred;
            }

            deferredDeviceContexts = new DeviceContext[MaxNumberOfThreads];
            for (int i = 0; i < deferredGraphicsDevices.Length; i++)
                deferredDeviceContexts[i] = GetInternalContext(deferredGraphicsDevices[i]);




            immediateContext = GetInternalContext(GraphicsDevice.MainDevice);

            effectPerThread = new BasicEffect[MaxNumberOfThreads];

            // Allocate rendering context array 
            graphicsDevicePerThread = new GraphicsDevice[MaxNumberOfThreads];
            graphicsDevicePerThread[0] = GraphicsDevice.MainDevice;

            deviceContextPerThread = new DeviceContext[MaxNumberOfThreads];
            deviceContextPerThread[0] = immediateContext;

            commandLists = new CommandList[MaxNumberOfThreads];
            CommandList[] frozenCommandLists = null;

            // Check if driver is supporting natively CommandList
            //bool supportConcurentResources;
            //bool supportCommandList;
            //GraphicsDevice.MainDevice.CheckThreadingSupport(out supportConcurentResources, out supportCommandList);

            view = Matrix.LookAtLH(new Vector3(0, 0, -viewZ), new Vector3(0, 0, 0), Vector3.UnitY);
            proj = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, Window.ClientBounds.Width / (float)Window.ClientBounds.Height, 0.1f, 100.0f);
            viewProj = Matrix.Multiply(view, proj);


            Array.Copy(deferredDeviceContexts, deviceContextPerThread, deviceContextPerThread.Length);
            Array.Copy(deferredGraphicsDevices, graphicsDevicePerThread, graphicsDevicePerThread.Length);

            fpsTimer.Start();

            immediateContext.Device.CheckThreadingSupport(out supportConcurentResources, out supportCommandList);

            base.Initialize();
        }

        private DeviceContext GetInternalContext(GraphicsDevice device)
        {
            var props = device.GetType().GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var prop = device.GetType().GetField("Context", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            DeviceContext context = prop.GetValue(device) as DeviceContext;
            return context;

            //return ((Device)device).ImmediateContext;
        }

        protected override void Update(GameTime gameTime)
        {
            // Rotate the cube.
            //var time = (float)gameTime.TotalGameTime.TotalSeconds;
            //basicEffect.World = Matrix.RotationX(time) * Matrix.RotationY(time * 2.0f) * Matrix.RotationZ(time * .7f);
            
            //basicEffect.View = Matrix.LookAtLH(new Vector3(0, 0, -viewZ), new Vector3(0, 0, 0), Vector3.UnitY);
            //basicEffect.Projection = Matrix.PerspectiveFovLH((float)Math.PI / 4.0f, Window.ClientBounds.Width / (float)Window.ClientBounds.Height, 0.1f, 100.0f);

            var keyboardState = _keyboardManager.GetState();

            if (keyboardState.IsKeyPressed(Keys.Left) && nextState.CountCubes > 1)
                nextState.CountCubes--; switchToNextState = true;
            if (keyboardState.IsKeyPressed(Keys.Right) && nextState.CountCubes < MaxNumberOfCubes)
                nextState.CountCubes++; switchToNextState = true;

            if (keyboardState.IsKeyPressed(Keys.F1))
                nextState.Type = (TestType)((((int)nextState.Type) + 1) % 3); switchToNextState = true;
            if (keyboardState.IsKeyPressed(Keys.F2))
                nextState.UseMap = !nextState.UseMap; switchToNextState = true;
            if (keyboardState.IsKeyPressed(Keys.F3))
                nextState.SimulateCpuUsage = !nextState.SimulateCpuUsage; switchToNextState = true;

            if (nextState.Type == TestType.Deferred)
            {
                if (keyboardState.IsKeyPressed(Keys.Down) && nextState.ThreadCount > 1)
                    nextState.ThreadCount--; switchToNextState = true;
                if (keyboardState.IsKeyPressed(Keys.Up) && nextState.ThreadCount < MaxNumberOfThreads)
                    nextState.ThreadCount++; switchToNextState = true;
            }
            if (keyboardState.IsKeyPressed(Keys.Escape))
                nextState.Exit = true; switchToNextState = true;


            // Handle base.Update
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {

            fpsCounter++;
            if (fpsTimer.ElapsedMilliseconds > 1000)
            {
                var typeStr = currentState.Type.ToString();
                if (currentState.Type != TestType.Immediate && !supportCommandList) typeStr += "*";

                Window.Title = string.Format("SharpDX - MultiCube D3D11 - (F1) {0} - (F2) {1} - (F3) {2} - Threads ↑↓{3} - Count ←{4}→ - FPS: {5:F2} ({6:F2}ms)", typeStr, currentState.UseMap ? "Map/UnMap" : "UpdateSubresource", currentState.SimulateCpuUsage ? "BurnCPU On" : "BurnCpu Off", currentState.Type == TestType.Deferred ? currentState.ThreadCount : 1, currentState.CountCubes * currentState.CountCubes, 1000.0 * fpsCounter / fpsTimer.ElapsedMilliseconds, (float)fpsTimer.ElapsedMilliseconds / fpsCounter);
                fpsTimer.Reset();
                fpsTimer.Stop();
                fpsTimer.Start();
                fpsCounter = 0;
            }




            // Clears the screen with the Color.CornflowerBlue
            GraphicsDevice.Clear(Color.CornflowerBlue);





            int threadCount = 1;
            if (currentState.Type != TestType.Immediate)
            {
                threadCount = currentState.Type == TestType.Deferred ? currentState.ThreadCount : 1;
                Array.Copy(deferredDeviceContexts, deviceContextPerThread, deviceContextPerThread.Length);
                Array.Copy(deferredGraphicsDevices, graphicsDevicePerThread, graphicsDevicePerThread.Length);
            }
            else
            {
                deviceContextPerThread[0] = immediateContext;
                graphicsDevicePerThread[0] = GraphicsDevice.MainDevice;
                effectPerThread[0] = basicEffect;
            }

            for (int i = 0; i < threadCount; i++)
            {
                var renderingGraphicsDevice = graphicsDevicePerThread[i];

                renderingGraphicsDevice.SetRenderTargets(GraphicsDevice.DepthStencilBuffer, GraphicsDevice.BackBuffer);

                // Setup the vertices
                renderingGraphicsDevice.SetVertexBuffer(vertices);
                renderingGraphicsDevice.SetVertexInputLayout(inputLayout);
            }

            var time = (float)gameTime.TotalGameTime.TotalSeconds;


            // Execute on the rendering thread when ThreadCount == 1 or No deferred rendering is selected
            if (currentState.Type == TestType.Immediate || (currentState.Type == TestType.Deferred && currentState.ThreadCount == 1))
            {
                RenderRow(0, 0, currentState.CountCubes, time);
            }

            // In case of deferred context, use of FinishCommandList / ExecuteCommandList
            if (currentState.Type != TestType.Immediate)
            {
                if (currentState.Type == TestType.FrozenDeferred)
                {
                    if (commandLists[0] == null)
                        RenderDeferred(1, time);
                }
                else if (currentState.ThreadCount > 1)
                {
                    RenderDeferred(currentState.ThreadCount, time);
                }

                for (int i = 0; i < currentState.ThreadCount; i++)
                {
                    var commandList = commandLists[i];
                    // Execute the deferred command list on the immediate context
                     immediateContext.ExecuteCommandList(commandList, false);

                    // For classic deferred we release the command list. Not for frozen
                    if (currentState.Type == TestType.Deferred)
                    {
                        // Release the command list
                        commandList.Dispose();
                        commandLists[i] = null;
                    }
                }
            }

            if (switchToNextState)
            {
                currentState = nextState;
                switchToNextState = false;
            }


            // Handle base.Draw
            base.Draw(gameTime);
        }

        private Action<int, int, int> RenderRow(int contextIndex, int fromY, int toY, Single time)
        {
            var renderingGraphicsDevice = graphicsDevicePerThread[contextIndex];
            var renderingContext = deviceContextPerThread[contextIndex];
            var renderingEffect = effectPerThread[contextIndex];

            //if (contextIndex == 0)
            //{
            //    deviceContextPerThread[0].ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
            //    deviceContextPerThread[0].ClearRenderTargetView(renderView, Color.Black);
            //}

            int count = currentState.CountCubes;
            float divCubes = (float)count / (viewZ - 1);
            var rotateMatrix = Matrix.Scaling(1.0f / count) * Matrix.RotationX(time) * Matrix.RotationY(time * 2) * Matrix.RotationZ(time * .7f);

            renderingEffect.View = view;
            renderingEffect.Projection = proj;
            

            for (int y = fromY; y < toY; y++)
            {
                for (int x = 0; x < count; x++)
                {
                    rotateMatrix.M41 = (x + .5f - count * .5f) / divCubes;
                    rotateMatrix.M42 = (y + .5f - count * .5f) / divCubes;

                    //// Update WorldViewProj Matrix 
                    //Matrix worldViewProj;
                    //Matrix.Multiply(ref rotateMatrix, ref viewProj, out worldViewProj);
                    //worldViewProj.Transpose();
                    //// Simulate CPU usage in order to see benefits of worldViewProj

                    //if (currentState.SimulateCpuUsage)
                    //{
                    //    for (int i = 0; i < BurnCpuFactor; i++)
                    //    {
                    //        Matrix.Multiply(ref rotateMatrix, ref viewProj, out worldViewProj);
                    //        worldViewProj.Transpose();
                    //    }
                    //}

                    //if (currentState.UseMap)
                    //{
                    //    var dataBox = renderingContext.MapSubresource(dynamicConstantBuffer, 0, MapMode.WriteDiscard, MapFlags.None);
                    //    Utilities.Write(dataBox.DataPointer, ref worldViewProj);
                    //    renderingContext.UnmapSubresource(dynamicConstantBuffer, 0);
                    //}
                    //else
                    //{
                    //    renderingContext.UpdateSubresource(ref worldViewProj, staticContantBuffer);
                    //}

                    // Draw the cube 
                    renderingEffect.World = rotateMatrix;
                    renderingEffect.CurrentTechnique.Passes[0].Apply();
                    renderingGraphicsDevice.Draw(PrimitiveType.TriangleList, vertices.ElementCount);
                }
            }

            if (currentState.Type != TestType.Immediate)
                commandLists[contextIndex] = renderingContext.FinishCommandList(false);

            return null;
        }

        private Action<int> RenderDeferred(int threadCount, Single time)
        {
            int deltaCube = currentState.CountCubes / threadCount;
            if (deltaCube == 0) deltaCube = 1;
            int nextStartingRow = 0;
            var tasks = new Task[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                var threadIndex = i;
                int fromRow = nextStartingRow;
                int toRow = (i + 1) == threadCount ? currentState.CountCubes : fromRow + deltaCube;
                if (toRow > currentState.CountCubes)
                    toRow = currentState.CountCubes;
                nextStartingRow = toRow;

                tasks[i] = new Task(() => RenderRow(threadIndex, fromRow, toRow, time));
                tasks[i].Start();
            }
            Task.WaitAll(tasks);

            return null;
        }

    }
}
