// COPYRIGHT 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

/// This module ...
/// 
/// Author: Stéfan Paitoni
/// Updates : 
/// 

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.Xna.Framework;
using MSTS;
using ORTS;
using LibAE;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orts.Formats.OR;
using ORTS.Common;


namespace LibAE.Formats
{
    #region Activity_def
    [Flags]
    enum TypeEvent
    {
        ACTIVITY_EVENT = 0,
        ACTIVITY_START = 1,
        ACTIVITY_STOP = 2,
        ACTIVITY_WAIT = 3
    };
#endregion

    #region PathEventItem


    public class PathEventItem : GlobalItem
    {
        [JsonProperty("nameEvent")]
        public string nameEvent { get; set; }
        [JsonProperty("nameVisible")]
        public bool nameVisible;
        [JsonProperty("typeEvent")]
        public int typeEvent;


        public PathEventItem(TypeEditor interfaceType)
        {
            typeItem = (int)TypeEvent.ACTIVITY_EVENT;
            alignEdition(interfaceType, null);
        }

        public override void alignEdition(TypeEditor interfaceType, GlobalItem ownParent)
        {
            if (interfaceType == TypeEditor.ACTIVITY)
            {
                setMovable();
                setLineSnap();
                setEditable();
                setActEdit();
            }
        }

        public void setName(int info)
        {
        }

        /// <summary>
        /// Name of the embedded icon resource (in this assembly) used to draw this event, or null.
        /// </summary>
        /// <remarks>Was a System.Drawing.Icon, which is not available on non-Windows platforms.</remarks>
        public virtual string getIconName() { return null; }

        /// <summary>Open the embedded icon resource named by <see cref="getIconName"/>.</summary>
        public Stream getIconStream()
        {
            string name = getIconName();
            return name == null ? null : typeof(PathEventItem).Assembly.GetManifestResourceStream(name);
        }
    }

    public class ActStartItem : PathEventItem
    {
        public ActStartItem(TypeEditor interfaceType)
            : base(interfaceType)
        {

            typeItem = (int)TypeEvent.ACTIVITY_START;
 
        }

        public void setNameStart(int info)
        {
            nameEvent = "start" + info;
        }

        public override void configCoord(MSTSCoord coord)
        {
            base.configCoord(coord);
            typeItem = (int)TypeItem.ACTIVITY_ITEM;
            nameVisible = false;
        }

        public override void Update(MSTSCoord coord)
        {
            base.configCoord(coord);
        }

        public override string getIconName() { return "LibAE.Icon.Start.ico"; }

    }

    public class ActStopItem : PathEventItem
    {

        public ActStopItem(TypeEditor interfaceType)
            : base(interfaceType)
        {

            typeItem = (int)TypeEvent.ACTIVITY_STOP;

        }

        public void setNameStop(int info)
        {
            nameEvent = "stop" + info;
        }

        public override void configCoord(MSTSCoord coord)
        {
            base.configCoord(coord);
            typeItem = (int)TypeItem.ACTIVITY_ITEM;
            nameVisible = false;
        }

        public override void Update(MSTSCoord coord)
        {
            base.configCoord(coord);
        }

        public override string getIconName() { return "LibAE.Icon.Stop.ico"; }

    }

    public class ActWaitItem : PathEventItem
    {

        public ActWaitItem(TypeEditor interfaceType)
            : base(interfaceType)
        {

            typeItem = (int)TypeEvent.ACTIVITY_WAIT;

        }

        public void setNameWait(int info)
        {
            nameEvent = "wait" + info;
        }

        public override void configCoord(MSTSCoord coord)
        {
            base.configCoord(coord);
            typeItem = (int)TypeItem.ACTIVITY_ITEM;
            nameVisible = false;
        }

        public override void Update(MSTSCoord coord)
        {
            base.configCoord(coord);
        }

        public override string getIconName() { return "LibAE.Icon.Wait.ico"; }

    }


    #endregion


}