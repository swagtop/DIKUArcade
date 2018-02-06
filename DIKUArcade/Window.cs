﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DIKUArcade.EventBus;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DIKUArcade {
    /// <summary>
    /// Aspect ratio for a DIKUArcade.Window object, defining
    /// width as a function of height.
    /// </summary>
    public enum AspectRatio {
        R4X3,
        R16X9,
        R1X1
    }

    /// <summary>
    /// Create an OpenTK window wrapper, where we only show
    /// relevant data members and hide unneeded functionality.
    /// </summary>
    public class Window {
        private static uint screenShotCounter;

        /// <summary>
        /// Every DIKUArcade.Window instance has its own private
        /// OpenTK.GameWindow object.
        /// </summary>
        private GameWindow window;

        // This is the signature for a key event handler:
        //private delegate void KeyEventHandler(object sender, KeyboardKeyEventArgs e);
        private EventHandler<KeyboardKeyEventArgs> defaultKeyHandler = null;
        private EventHandler<EventArgs> defaultResizeHandler = null;

        private bool isRunning;

        private uint width, height;

        private string title;

        private GameEventBus<object> eventBus;

        private void ActivateThisWindowContext() {
            window = new GameWindow((int) this.width, (int) this.height);

            GL.ClearDepth(1);
            GL.ClearColor(Color.Black);

            AddDefaultKeyEventHandler();
            AddDefaultResizeHandler();

            isRunning = true;
            window.Context.MakeCurrent(window.WindowInfo);
            window.Visible = true;
        }

        public Window(string title, uint width, uint height)
        {
            this.width = width;
            this.height = height;
            this.title = title;
            isRunning = true;
            ActivateThisWindowContext();
        }

        public Window(string title, uint height, AspectRatio aspect) {
            this.height = height;
            switch (aspect) {
            case AspectRatio.R1X1:
                width = this.height;
                break;
            case AspectRatio.R4X3:
                width = this.height * 4 / 3;
                break;
            case AspectRatio.R16X9:
                width = this.height * 16 / 9;
                break;
            default:
                throw new InvalidEnumArgumentException();
            }
            ActivateThisWindowContext();
        }

        /// <summary>
        /// Register an event bus to this window instance. The specified
        /// bus will be used for capturing input events, such as keyboard presses.
        /// </summary>
        /// <param name="bus">A GameEventBus to register for this window</param>
        /// <returns>False if an event bus was already registered, true otherwise.</returns>
        public bool RegisterEventBus(GameEventBus<object> bus) {
            if (eventBus != null) {
                // an event bus was already registered!
                // TODO: Should it be possible to swap event bus?
                return false;
            }
            eventBus = bus;
            window.Keyboard.KeyDown += RegisterEvent;
            window.Keyboard.KeyUp += RegisterEvent;
            RemoveDefaultKeyEventHandler();
            return true;
        }

        private void RegisterEvent(object sender, KeyboardKeyEventArgs e) {
            var keyAction = (e.Keyboard.IsKeyDown(e.Key)) ? "KEY_PRESS" : "KEY_RELEASE";
            var newEvent = GameEventFactory<object>.CreateGameEventForAllProcessors(
                GameEventType.InputEvent, this, Input.KeyTransformer.GetKeyString(e.Key), keyAction, "");
            eventBus.RegisterEvent(newEvent);
        }

        #region WINDOW_RESIZE

        private bool resizable = true;

        /// <summary>
        /// Get or set if this Window instance should be resizable.
        /// </summary>
        public bool Resizable {
            get {
                return resizable;
            }
            set {
                if (value) {
                    RemoveDefaultResizeHandler();
                } else {
                    AddDefaultResizeHandler();
                }
            }
        }

        private void AddDefaultResizeHandler() {
            if (defaultResizeHandler != null) {
                return;
            }

            defaultResizeHandler = delegate(object sender, EventArgs args) {
                GL.Viewport(0, 0, window.Width, window.Height);
                width = (uint) window.Width;
                height = (uint) window.Height;

                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();
                //GL.Ortho(-1.0, 1.0, -1.0, 1.0, 0.0, 4.0);
                GL.Ortho(0.0,1.0,0.0,1.0, 0.0, 4.0);
                };
            window.Resize += defaultResizeHandler;
        }

        private void RemoveDefaultResizeHandler() {
            if (defaultResizeHandler != null) {
                window.Resize -= defaultResizeHandler;
                defaultResizeHandler = null;
            }
        }

        #endregion WINDOW_RESIZE

        #region KEY_EVENT_HANDLERS

        private void AddDefaultKeyEventHandler() {
            if (defaultKeyHandler != null) {
                return;
            }

            defaultKeyHandler = delegate(object sender, KeyboardKeyEventArgs e) {
                if (e.Key == Key.Escape) {
                    CloseWindow();
                    return;
                }
                if (e.Key == Key.F12) {
                    SaveScreenShot();
                }
            };
            window.Keyboard.KeyDown += defaultKeyHandler;
        }

        private void RemoveDefaultKeyEventHandler() {
            if (defaultKeyHandler != null) {
                window.Keyboard.KeyDown -= defaultKeyHandler;
                defaultKeyHandler = null;
            }
        }

        /// <summary>
        /// Function signature for a key event handler method.
        /// </summary>
        /// <param name="keyArgs">OpenTK.Input.KeyboardKeyEventArgs</param>
        public delegate void WindowKeyHandler(KeyboardKeyEventArgs keyArgs);

        /// <summary>
        /// Add an event handler for when any keyboard key is pressed.
        /// </summary>
        /// <param name="method">Method with the signature of a Window.WindowKeyHandler delegate.</param>
        public void AddKeyPressEventHandler(WindowKeyHandler method) {
            //RemoveDefaultKeyEventHandler();
            window.Keyboard.KeyUp += delegate(object sender, KeyboardKeyEventArgs args) {
                method(args);
            };
        }

        /// <summary>
        /// Add an event handler for when any keyboard key is released.
        /// </summary>
        /// <param name="method">Delegate method</param>
        public void AddKeyReleaseEventHandler(EventHandler<KeyboardKeyEventArgs> method) {
            //RemoveDefaultKeyEventHandler();
            window.Keyboard.KeyDown += method;
        }

        #endregion KEY_EVENT_HANDLERS

        /// <summary>
        /// Check if the Window is still running.
        /// </summary>
        public bool IsRunning() {
            return isRunning;
        }

        /// <summary>
        /// Close the Window.
        /// </summary>
        public void CloseWindow() {
            isRunning = false;
            window.Close();
        }

        /// <summary>
        /// Clear the Window with a uniform background color.
        /// </summary>
        public void Clear() {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        /// <summary>
        /// Set color to be used as clear color when using the Window.Clear() method.
        /// </summary>
        /// <param name="vec">Vec3F containing the RGB color values.</param>
        /// <exception cref="ArgumentOutOfRangeException">Normalized color values must be
        /// between 0 and 1.</exception>
        public void SetClearColor(Math.Vec3F vec) {
            if (vec.X < 0.0f || vec.X > 1.0f ||
                vec.Y < 0.0f || vec.Y > 1.0f ||
                vec.Z < 0.0f || vec.Z > 1.0f) {
                throw new ArgumentOutOfRangeException($"RGB Color values must be between 0 and 1: {vec}");
            }
            GL.ClearColor(vec.X, vec.Y, vec.Z, 1.0f);
        }

        /// <summary>
        /// Set color to be used as clear color when using the Window.Clear() method.
        /// </summary>
        /// <param name="vec">Vec3I containing the RGB color values.</param>
        /// <exception cref="ArgumentOutOfRangeException">Color values must be between 0 and 255.</exception>
        public void SetClearColor(Math.Vec3I vec) {
            if (vec.X < 0 || vec.X > 255 ||
                vec.Y < 0 || vec.Y > 255 ||
                vec.Z < 0 || vec.Z > 255) {
                throw new ArgumentOutOfRangeException($"RGB Color values must be between 0 and 255: {vec}");
            }
            GL.ClearColor((float)vec.X / 255.0f, (float)vec.Y / 255.0f, (float)vec.Z / 255.0f, 1.0f);
        }

        /// <summary>
        /// Set color to be used as clear color when using the Window.Clear() method.
        /// </summary>
        /// <param name="color">System.Drawing.Color object containing color channel values.</param>
        public void SetClearColor(System.Drawing.Color color) {
            SetClearColor(new Math.Vec3I((int) color.R, (int) color.G, (int) color.B));
        }

        /// <summary>
        /// Swap double buffers for the Window.
        /// </summary>
        public void SwapBuffers() {
            window.SwapBuffers();
            //window.Context.SwapBuffers();
        }

        /// <summary>
        /// Check for incoming keyboard or mouse events.
        /// </summary>
        public void PollEvents() {
            window.ProcessEvents();
        }

        /// <summary>
        /// Save a screenshot of the Window's current state.
        /// </summary>
        /// <exception cref="GraphicsContextMissingException"></exception>
        public void SaveScreenShot() {
            if (GraphicsContext.CurrentContext == null) {
                throw new GraphicsContextMissingException();
            }

            var bmp = new Bitmap(window.ClientSize.Width, window.ClientSize.Height);
            var data =
                bmp.LockBits(window.ClientRectangle, ImageLockMode.WriteOnly,
                    PixelFormat.Format24bppRgb);
            GL.ReadPixels(0, 0, window.ClientSize.Width, window.ClientSize.Height,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
                PixelType.UnsignedByte, data.Scan0);
            bmp.UnlockBits(data);

            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

            // save screenshot, not in bin/Debug (et sim.), but in a logical place
            var dir = new DirectoryInfo(Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location));

            // TODO: Place this functionality elsewhere!
            var loop = true;
            while (loop) {
                dir = dir.Parent;
                foreach (var d in dir.Parent.GetDirectories()) {
                    if (d.Name == "DIKUArcade") {
                        loop = false;
                        break;
                    }
                }
                //Console.WriteLine($"DIR:        {dir}");
                //Console.WriteLine($"DIR.Name:   {dir.Name}");
                //Console.WriteLine($"DIR.Parent: {dir.Parent}");
                //Console.WriteLine($"DIR.Root:   {dir.Root}");
            }

            // build the save path
            var saveName = $"screenShot_{Window.screenShotCounter++}.bmp";
            var folder = Path.Combine(dir.ToString(), "screenShots");
            var path = Path.Combine(folder, saveName);

            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }

            bmp.Save(path);
        }
    }
}