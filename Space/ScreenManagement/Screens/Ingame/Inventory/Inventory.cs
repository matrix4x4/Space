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
using Space.ScreenManagement.Screens.Ingame.Inventory;
using Engine.ComponentSystem.RPG.Components;
using Space.Data;

namespace Space.ScreenManagement.Screens.Ingame.Hud
{

    class Inventory : AbstractGuiElement
    {
        /// <summary>
        /// The global item selection manager.
        /// </summary>
        ItemSelectionManager _itemSelection;

        /// <summary>
        /// The dynamic item list object.
        /// </summary>
        InventoryItems _list;

        /// <summary>
        /// Constructor
        /// </summary>
        public Inventory(GameClient client, ItemSelectionManager itemSelection, TextureManager textureManager)
            : base(client)
        {
            _itemSelection = itemSelection;

            _list = new InventoryItems(client, itemSelection, textureManager);
        }

        public override void LoadContent(IngameScreen ingame, ContentManager content)
        {
            base.LoadContent(ingame, content);
            base.Enabled = true;

            _list.LoadContent(ingame, content);
        }

        public override void Draw()
        {

            if (Visible)
            {
                _spriteBatch.Begin();

                _basicForms.FillRectangle(_scale.X(GetPosition().X),
                    _scale.Y(GetPosition().Y),
                    _scale.X(GetWidth()),
                    _scale.Y(GetHeight()), Color.Black * 0.6f);

                _fonts.DrawString(Fonts.Types.Strasua24, "Inventory", 
                    new Vector2(_scale.X(GetPosition().X + 5), _scale.Y(GetPosition().Y + 20)),
                    Color.White, true);

                _spriteBatch.End();

                _list.Draw();
            }
        }

        public override void SetPosition(float x, float y)
        {
            base.SetPosition(x, y);
            _list.SetPosition(x + 5, y + 50);
        }

        #region Listener

        public override bool DoHandleMousePressed(MouseButtons buttons)
        {
            if (!IsMouseClickedInElement()&&!_itemSelection.ItemIsSelected)
            {
                return false;
            }
            return _list.DoHandleMousePressed(buttons);
            
        }

        public override bool DoHandleMouseReleased(MouseButtons buttons)
        {
            if (!IsMouseClickedInElement()&&!_itemSelection.ItemIsSelected)
            {
                return false;
            }
            return _list.DoHandleMouseReleased(buttons);
        }

        #endregion

    }
}
