﻿using System;
using System.Windows.Forms;
using Engine.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Space.ComponentSystem.Factories;
using Space.Graphics;

namespace Space.Tools.DataEditor
{
    class PlanetPreviewControl : GraphicsDeviceControl
    {
        public PlanetFactory Planet
        {
            set
            {
                if (_timer == null)
                {
                    _timer = new Timer
                    {
                        Interval = 1000 / 30,
                        Enabled = false
                    };
                    _timer.Tick += TimerOnTick;
                }
                _factory = value;
                if (value == null)
                {
                    _timer.Enabled = false;
                }
                else if (_planet != null)
                {
                    LoadContent();
                }
            }
        }

        private PlainContentManager _content;

        private PlanetFactory _factory;

        private Planet _planet;

        private Ellipse _circle;

        private Timer _timer;

        protected override void Initialize()
        {
            if (_content == null)
            {
                _content = new PlainContentManager(Services);
            }
            if (_planet == null)
            {
                try
                {
                    _planet = new Planet(_content, GraphicsDevice);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed initializing planet: " + ex);
                }
                LoadContent();
            }
            if (_circle == null)
            {
                try
                {
                    _circle = new Ellipse(_content, GraphicsDevice)
                    {
                        Thickness = 2,
                        Color = Color.SlateGray * 0.5f
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed initializing ellipse: " + ex);
                }
            }
            GraphicsDevice.DeviceReset += GraphicsDeviceOnDeviceReset;
        }

        private void GraphicsDeviceOnDeviceReset(object sender, EventArgs eventArgs)
        {
            _planet.InvalidateVertices();
        }

        private void LoadContent()
        {
            if (_factory != null)
            {
                _planet.SurfaceRotation = new Vector2(_factory.RotationSpeed.High, _factory.RotationSpeed.Low);
                _planet.SurfaceTint = _factory.SurfaceTint;
                _planet.AtmosphereTint = _factory.AtmosphereTint;
                _planet.SetSize(_factory.Radius.Low * 2);
                if (_circle != null)
                {
                    _circle.SetSize(_factory.Radius.High * 2);
                }
                Control control = FindForm();
                if (control == null)
                {
                    control = this;
                }
                control.Cursor = Cursors.WaitCursor;
                try
                {
                    _planet.SurfaceTexture = _content.Load<Texture2D>(_factory.Texture);
                    _timer.Enabled = true;
                }
                catch
                {
                }
                finally
                {
                    control.Cursor = Cursors.Default;
                }
            }
        }

        private void TimerOnTick(object sender, EventArgs eventArgs)
        {
            Refresh();
        }

        protected override void Draw()
        {
            GraphicsDevice.Clear(Color.FromNonPremultiplied(64, 64, 64, 255));
            if (_planet == null || _factory == null)
            {
                return;
            }
            _planet.Time = (float)(DateTime.UtcNow - DateTime.Today).TotalMilliseconds / 1000;
            Vector2 toLight;
            var mousePos = PointToClient(MousePosition);
            toLight.X = mousePos.X - Width / 2f;
            toLight.Y = mousePos.Y - Height / 2f;
            toLight.Normalize();
            _planet.LightDirection = toLight;
            _planet.SetCenter(Width / 2f, Height / 2f);
            try
            {
                _planet.Draw();
            }
            catch
            {
            }
            if (_circle != null)
            {
                _circle.SetCenter(Width / 2f, Height / 2f);
                _circle.Draw();
            }
        }
    }
}
