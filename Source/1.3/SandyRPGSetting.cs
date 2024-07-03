// This file is basically
// https://github.com/SandyTheGreat/RPG-Style-Inventory/blob/master/Source_1_2/Sandy_Detailed_RPG_Inventory/Sandy_Detailed_RPG_Inventory/RPG_Settings.cs
// I do not wish to provide an option to change the window width. Sorry.

using System;
using UnityEngine;
using Verse;

namespace Sandy_Detailed_RPG_Inventory
{
    class Sandy_Detailed_RPG_Settings : ModSettings
    {
        public static float defaultRpgTabHeight = 565f;
        public static float rpgTabHeight = defaultRpgTabHeight;

        public static float defaultRpgTabWidth = 550f;
        public static float rpgTabWidth = defaultRpgTabWidth;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref rpgTabHeight, "rpgTabHeight", defaultRpgTabHeight);
            Scribe_Values.Look(ref rpgTabWidth, "rpgTabWidth", defaultRpgTabWidth);
            base.ExposeData();
        }
    }

    class Sandy_Detailed_RPG_Inventory : Mod
    {
        Sandy_Detailed_RPG_Settings settings;

        string tabHeight;
        // No, I DO NOT want you to be able to set the window width. Sorry.

        public Sandy_Detailed_RPG_Inventory(ModContentPack content) : base(content)
        {
            this.settings = GetSettings<Sandy_Detailed_RPG_Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.Label(
                String.Format("{0}({1}: {2:D})",
                "Sandy_Inventory_Height".Translate(),
                "default".Translate(),
                (int)Sandy_Detailed_RPG_Settings.defaultRpgTabHeight));
            listingStandard.TextFieldNumeric(ref Sandy_Detailed_RPG_Settings.rpgTabHeight, ref tabHeight, 0, 102400);
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Sandy_RPG_Style_Inventory_Title".Translate();
        }
    }
}