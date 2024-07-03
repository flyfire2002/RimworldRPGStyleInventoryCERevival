using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace Sandy_Detailed_RPG_Inventory
{
    [DefOf]
    public static class Sandy_Gear_DefOf
    {	
        public static BodyPartGroupDef Teeth;
        public static BodyPartGroupDef Mouth;
        public static BodyPartGroupDef Neck;
        public static BodyPartGroupDef Shoulders;
        public static BodyPartGroupDef Arms;
        public static BodyPartGroupDef Hands;
        public static BodyPartGroupDef Waist;
        public static BodyPartGroupDef Feet;
        // CE Layers
        public static ApparelLayerDef Webbing;
        public static ApparelLayerDef Backpack;
        public static ApparelLayerDef Shield;
        public static ApparelLayerDef OnHead;
        public static ApparelLayerDef StrappedHead;
        // CE BodyPartGroups
        public static BodyPartGroupDef LeftShoulder;
        public static BodyPartGroupDef RightShoulder;
        public static BodyPartGroupDef LeftArm;
        public static BodyPartGroupDef RightArm;
        //This was added for Jewelry
        //Two defs file was added, they are in Defs\Jewelry_compat
        public static BodyPartGroupDef Ears;
        public static ApparelLayerDef Accessories;
        // Added for Research Reinvented
        // Def files in Defs\Research_Reinvented
        public static ApparelLayerDef Satchel;

        public static bool ContainsCEHeadLayer(List<ApparelLayerDef> layers)
        {
            return layers.Contains(StrappedHead) || layers.Contains(OnHead);
        }
    }
}
