using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FarseerPhysics.Dynamics;

namespace LineBall
{
    class WorldObject
    {
        //public WorldObject(Texture2D nSprite, Body nBody, Vector2 nPosition, Vector2 nOrigin)
        public WorldObject(Texture2D nSprite, Body nBody, Vector2 nPosition)
        {
            this.sprite = nSprite;
            this.body = nBody;
            this.objectPosition = nPosition;
            //this.objectOrigin = nOrigin;
        }

        public Texture2D sprite
        {
            get;
            set;
        }

        public Body body
        {
            get;
            set;
        }

        public Vector2 objectPosition
        {
            get;
            set;
        }

        /*
        public Vector2 objectOrigin
        {
            get;
            set;
        }
        */
    }
}
