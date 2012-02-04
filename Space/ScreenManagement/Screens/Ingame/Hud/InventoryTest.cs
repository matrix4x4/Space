﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Space.Control;
using Space.ScreenManagement.Screens.Ingame.Interfaces;
using Nuclex.Input;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Space.ScreenManagement.Screens.Helper;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Space.ScreenManagement.Screens.Ingame.GuiElementManager;

namespace Space.ScreenManagement.Screens.Ingame.Hud
{
    class InventoryTest : AbstractGuiElement, IItem
    {
        InventoryManagerTest _manager;
        ItemSelectionManager _itemSelection;

        public InventoryTest(GameClient client, ItemSelectionManager itemSelection)
            : base(client)
        {
            _manager = new InventoryManagerTest(client);
            _itemSelection = itemSelection;
        }

        public override void LoadContent(SpriteBatch spriteBatch, ContentManager content)
        {
            base.LoadContent(spriteBatch, content);
            SetHeight(500);
            SetWidth(400);
            base.Enabled = true;
        }

        public override void Draw()
        {
            _spriteBatch.Begin();
            _basicForms.FillRectangle((int)GetPosition().X, (int)GetPosition().Y, (int)GetWidth(), (int)GetHeight(), Color.Black * 0.8f);

            for (int i = 0; i < 4; i++)
            {
                _basicForms.FillRectangle((int)GetPosition().X + (i + 1) * 52, (int)GetPosition().Y + 52, 50, 50, Color.White * 0.2f);
                var image = _manager.GetImage(i);
                if (image != null && !(_itemSelection.SelectedId == i && _itemSelection.SelectedClass == this))
                {
                    _spriteBatch.Draw(image, new Rectangle((int)GetPosition().X + (i + 1) * 52, (int)GetPosition().Y + 52, 50, 50), Color.White);
                }
            }
            _spriteBatch.End();
        }

        public override bool DoHandleMousePressed(MouseButtons buttons)
        {
            if (!(Mouse.GetState().X >= GetPosition().X && Mouse.GetState().X <= GetPosition().X + GetWidth() && Mouse.GetState().Y >= GetPosition().Y && Mouse.GetState().Y <= GetPosition().Y + GetHeight()))
            {
                return false;
            }

            return true;
        }

        public override bool DoHandleMouseReleased(MouseButtons buttons)
        {
            if (!(Mouse.GetState().X >= GetPosition().X && Mouse.GetState().X <= GetPosition().X + GetWidth() && Mouse.GetState().Y >= GetPosition().Y && Mouse.GetState().Y <= GetPosition().Y + GetHeight()))
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (Mouse.GetState().X >= GetPosition().X + (i + 1) * 52 && Mouse.GetState().X <= GetPosition().X + (i + 1) * 52 + 50 && Mouse.GetState().Y >= GetPosition().Y + 52 && Mouse.GetState().Y <= GetPosition().Y + 52 + 50)
                {
                    var image = _manager.GetImage(i);
                    if (_itemSelection.ItemIsSelected)
                    {
                        _itemSelection.RemoveSelection();
                    }
                    else
                    {
                        if (image != null)
                        {
                            _itemSelection.SetSelection(this, i, image);
                        }
                    }
                }
            }

            return true;
        }
    }
}
