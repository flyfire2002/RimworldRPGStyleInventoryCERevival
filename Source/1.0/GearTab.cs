using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using CombatExtended;

namespace Sandy_Detailed_RPG_Inventory
{
    class Benchmarker
    {
        Stopwatch stopwatch = new Stopwatch();
        private long nanoPerTick = (1000L * 1000L * 1000L) / Stopwatch.Frequency;
        private float avg = 0f;
        private float count = 0f;

        public void Start()
        {
            stopwatch.Start();
        }

        public void Stop()
        {
            stopwatch.Stop();
            var us = stopwatch.ElapsedTicks * nanoPerTick / 1000f;
            avg = avg + (us - avg) / (count += 1f);
            Log.Message($"Curr: {us}us; Avg: {avg}us over {count}");
            stopwatch.Reset();
        }
    }

    public class Sandy_Detailed_RPG_GearTab : ITab_Pawn_Gear
    {
        private Benchmarker benchmarker = new Benchmarker();

        private Vector2 scrollPosition = Vector2.zero;

        private float scrollViewHeight;

        // DrawColonist consts
        public static readonly Vector3 PawnTextureCameraOffset = new Vector3(0f, 0f, 0f);

        // Inventory list vars
        private static List<Thing> workingInvList = new List<Thing>();

        public static readonly new Color HighlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        // RPG inventory GUI consts
        private const float UniversalMargin = 10f;
        private const float SmallIconSize = 24f;
        private const float SmallIconMargin = 2f;

        private const float CheckboxHeight = 20f;
        private const float CEAreaHeight = 60f;

        private const float statBoxWidth = 128f;

        // Used too many times per tick; keep only one instance and only Get it once to
        // save 80 microsec (-10% time) per tick.
        private static Texture2D itemBackground;

        #region CE_Field
        private const float _barHeight = 20f;
        private const float _margin = 15f;
        private const float _thingIconSize = 28f;
        private const float _thingLeftX = 36f;
        private const float _thingRowHeight = 28f;
        private const float _topPadding = 20f;
        private const float _standardLineHeight = 22f;
        private static readonly Color _highlightColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        private static readonly Color _thingLabelColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        #endregion CE_Field

        private bool viewlist = false;

        public Sandy_Detailed_RPG_GearTab()
        {
            size = new Vector2(550f, 500f);
            labelKey = "TabGear";
            tutorTag = "Gear";
        }

        protected override void FillTab()
        {
            benchmarker.Start();
            Text.Font = GameFont.Small;
            Rect checkBox = new Rect(CheckboxHeight, 0f, 100f, 30f);
            Widgets.CheckboxLabeled(checkBox, "Sandy_ViewList".Translate(), ref viewlist, false, null, null, false);

            // Delegate to vanilla Filltab (sans drawing CE loadout bars) if show as list is chosen, or if the pawn is not human
            // (e.g. muffalo with cargo)
            if (viewlist || !SelPawnForGear.RaceProps.Humanlike)
            {
                // Set an enclosing GUI group that contains the group from base.FillTab
                // and the CE loadout bar.
                Rect listViewPosition = new Rect(0f, 0f, size.x, size.y);
                GUI.BeginGroup(listViewPosition);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                // Hack. Base Filltab use size.y to set BeginGroup. Change it here so the list GUI
                // group doesn't overlap with CE loadout bars.
                size.Set(size.x, size.y - CEAreaHeight);
                base.FillTab();
                // Restore the size.y, otherwise the tab will shrink by 60px per frame.
                size.Set(size.x, size.y + CEAreaHeight);

                // Shift the bar to compensate for the margin not set in current GUI group;
                // same about the y.
                TryDrawCEloadout(UniversalMargin, listViewPosition.height - CEAreaHeight - UniversalMargin, listViewPosition.width - CheckboxHeight - UniversalMargin * 2);
                GUI.EndGroup();
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                benchmarker.Stop();
                return;
            }

            if (itemBackground == null) itemBackground = ContentFinder<Texture2D>.Get("UI/Widgets/DesButBG");

            Rect rect = new Rect(0f, CheckboxHeight, size.x, size.y - CheckboxHeight);
            Rect rect2 = rect.ContractedBy(UniversalMargin);
            Rect position = new Rect(rect2.x, rect2.y, rect2.width, rect2.height);

            GUI.BeginGroup(position);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect outRect = new Rect(0f, 0f, position.width, position.height - CEAreaHeight);
            Rect viewRect = new Rect(0f, 0f, position.width - UniversalMargin * 2, scrollViewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float num = 0f;

            Vector2 statStart = new Vector2(374f, 0f);
            DrawMassInfo(statStart);
            statStart.Set(statStart.x, statStart.y + SmallIconSize + SmallIconMargin);
            DrawComfyTemperatureRange(statStart);

            // Don't check if should show armor for pawn. Being humanlike is suffice to show
            // armor, and the pawn must be humanlike at this point.
            Rect armorRect = new Rect(374f, 3 * (SmallIconSize + SmallIconMargin) + 6f,
                statBoxWidth, 3 * SmallIconSize + 4 * SmallIconMargin);
            TooltipHandler.TipRegion(armorRect, "OverallArmor".Translate());
            Rect rectsharp = new Rect(armorRect.x, armorRect.y, armorRect.width, SmallIconSize);
            DrawOverallArmor(rectsharp, StatDefOf.ArmorRating_Sharp, "ArmorSharp".Translate(),
                ContentFinder<Texture2D>.Get("UI/Icons/Sandy_ArmorSharp_Icon"));
            Rect rectblunt = new Rect(armorRect.x, armorRect.y + SmallIconSize + 2 * SmallIconMargin,
                armorRect.width, SmallIconSize);
            DrawOverallArmor(rectblunt, StatDefOf.ArmorRating_Blunt, "ArmorBlunt".Translate(),
                ContentFinder<Texture2D>.Get("UI/Icons/Sandy_ArmorBlunt_Icon"));
            Rect rectheat = new Rect(armorRect.x, armorRect.y + 2 * (SmallIconSize + 2 * SmallIconMargin),
                armorRect.width, SmallIconSize);
            DrawOverallArmor(rectheat, StatDefOf.ArmorRating_Heat, "ArmorHeat".Translate(),
                ContentFinder<Texture2D>.Get("UI/Icons/Sandy_ArmorHeat_Icon"));

            if (this.IsVisible)
            {
                //Hats
                Rect newRect1 = new Rect(150f, 0f, 64f, 64f);
                GUI.DrawTexture(newRect1, itemBackground);
                Rect tipRect1 = newRect1.ContractedBy(12f);
                TooltipHandler.TipRegion(tipRect1, "Sandy_Head".Translate());
                //Vests
                Rect newRect2 = new Rect(76f, 148f, 64f, 64f);
                GUI.DrawTexture(newRect2, itemBackground);
                Rect tipRect2 = newRect2.ContractedBy(12f);
                TooltipHandler.TipRegion(tipRect2, "Sandy_TorsoMiddle".Translate());
                //Shirts
                Rect newRect3 = new Rect(150f, 148f, 64f, 64f);
                GUI.DrawTexture(newRect3, itemBackground);
                Rect tipRect3 = newRect3.ContractedBy(12f);
                TooltipHandler.TipRegion(tipRect3, "Sandy_TorsoOnSkin".Translate());
                //Dusters
                Rect newRect4 = new Rect(224f, 148f, 64f, 64f);
                GUI.DrawTexture(newRect4, itemBackground);
                Rect tipRect4 = newRect4.ContractedBy(12f);
                TooltipHandler.TipRegion(tipRect4, "Sandy_TorsoShell".Translate());
                //Belts
                Rect newRect5 = new Rect(150f, 222f, 64f, 64f);
                GUI.DrawTexture(newRect5, itemBackground);
                Rect tipRect5 = newRect5.ContractedBy(12f);
                TooltipHandler.TipRegion(tipRect5, "Sandy_Belt".Translate());
                //Pants
                Rect newRect6 = new Rect(150f, 296f, 64f, 64f);
                GUI.DrawTexture(newRect6, itemBackground);
                Rect tipRect6 = newRect6.ContractedBy(12f);
                TooltipHandler.TipRegion(tipRect6, "Sandy_Pants".Translate());
                Color color = new Color(1f, 1f, 1f, 1f);
                GUI.color = color;
                //Pawn
                Rect PawnRect = new Rect(374f, 172f, 128f, 128f);
                this.DrawColonist(PawnRect, this.SelPawnForGear);
            }

            if (this.ShouldShowEquipment(this.SelPawnForGear))
            {
                foreach (ThingWithComps current in this.SelPawnForGear.equipment.AllEquipmentListForReading)
                {
                    if (current == this.SelPawnForGear.equipment.Primary)
                    {
                        Rect newRect = new Rect(406f, 296f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current, false);
                        if (this.SelPawnForGear.story.traits.HasTrait(TraitDefOf.Brawler) && this.SelPawnForGear.equipment.Primary != null && this.SelPawnForGear.equipment.Primary.def.IsRangedWeapon)
                        {
                            Rect rect6 = new Rect(newRect.x, newRect.yMax - 20f, 20f, 20f);
                            GUI.DrawTexture(rect6, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Forced_Icon", true));
                            TooltipHandler.TipRegion(rect6, "BrawlerHasRangedWeapon".Translate());
                        }
                    }
                    else 
                    {
                        Rect newRect1 = new Rect(406f, 370f, 64f, 64f);
                        GUI.DrawTexture(newRect1, itemBackground);
                        this.DrawThingRow1(newRect1, current, false);
                        if (this.SelPawnForGear.story.traits.HasTrait(TraitDefOf.Brawler) && this.SelPawnForGear.equipment.Primary != null && current.def.IsRangedWeapon)
                        {
                            Rect rect6 = new Rect(newRect1.x, newRect1.yMax - 20f, 20f, 20f);
                            GUI.DrawTexture(rect6, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Forced_Icon", true));
                            TooltipHandler.TipRegion(rect6, "BrawlerHasRangedWeapon".Translate());
                        }
                    }
                }
            }

            if (this.ShouldShowApparel(this.SelPawnForGear))
            {
                foreach (Apparel current2 in this.SelPawnForGear.apparel.WornApparel)
                {
                    /*switch (current2.def.apparel)
                    {
                        case ApparelProperties a when (a.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead) || a.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead)
                        && a.layers.Contains(ApparelLayerDefOf.Overhead)):
                        break;
                    }*/
                    //Head
                    if ((current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead) || current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead))
                       && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead))
                    {
                        Rect newRect = new Rect(150f, 0f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead))
                    {
                        Rect newRect = new Rect(224f, 0f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Teeth) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes))
                    {
                        Rect newRect = new Rect(76f, 0f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Torso
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle))
                    {
                        Rect newRect = new Rect(76f, 148f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin))
                    {
                        Rect newRect = new Rect(150f, 148f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(224f, 148f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Belt
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Waist) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Belt))
                    {
                        Rect newRect = new Rect(150f, 222f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Jetpack
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Waist) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(224f, 222f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Legs
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle))
                    {
                        Rect newRect = new Rect(76f, 296f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin))
                    {
                        Rect newRect = new Rect(150f, 296f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(224f, 296f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Feet
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Feet) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle))
                    {
                        Rect newRect = new Rect(76f, 370f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Feet) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin))
                    {
                        Rect newRect = new Rect(150f, 370f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Feet) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs)
                        && (current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) || current2.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead)))
                    {
                        Rect newRect = new Rect(224f, 370f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Hands
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = new Rect(10f, 126f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && (current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) || current2.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead))
                        && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = new Rect(10f, 63f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = new Rect(10f, 189f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Shoulders
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)
                        && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand))
                    {
                        Rect newRect = new Rect(298f, 189f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)
                        && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)
                        && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand))
                    {
                        Rect newRect = new Rect(298f, 126f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)
                        && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin) && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle)
                        && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand))
                    {
                        Rect newRect = new Rect(298f, 63f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //RightHand
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(298f, 315f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = new Rect(298f, 378f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(298f, 252f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //LeftHand
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(10f, 315f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = new Rect(10f, 378f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && current2.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) && !current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = new Rect(10f, 252f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    //Neck
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders)
                             && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Belt) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = new Rect(76f, 74f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders)
                             && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = new Rect(150f, 74f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && !current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders)
                             && current2.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) && !current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = new Rect(224f, 74f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }

                    //this part add jewelry support
                    //They currently overlape with some appearoll 2 stuff
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && (current2.def.apparel.layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = new Rect(76f, 74f, 64f, 64f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Ears) && (current2.def.apparel.layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = new Rect(298f, 0f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    else if (current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && (current2.def.apparel.layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = new Rect(10f, 0f, 56f, 56f);
                        GUI.DrawTexture(newRect, itemBackground);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    /* Could be the code for ammo vests and backpack 
                    if (current2.def.apparel.layers.Contains(ApparelLayerDefOf.Belt) && current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = new Rect(76f, 178f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    if (current2.def.apparel.layers.Contains(ApparelLayerDefOf.Belt) && current2.def.apparel.bodyPartGroups.Contains(Sandy_Gear_DefOf.Waist)) {
                        Rect newRect = new Rect(150f, 178f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    }
                    if (current2.def.apparel.layers.Contains(ApparelLayerDefOf.Belt) && current2.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)) {
                        Rect newRect = new Rect(224f, 178f, 64f, 64f);
                        this.DrawThingRow1(newRect, current2, false);
                    */
                }
            }

            // Do not check if should show (text) inventory for pawn; the pawn being humanlike is suffice, and
            // at this point the pawn must be humanlike.
            num = 440f;
            Widgets.ListSeparator(ref num, viewRect.width, "Inventory".Translate());
            Sandy_Detailed_RPG_GearTab.workingInvList.Clear();
            Sandy_Detailed_RPG_GearTab.workingInvList.AddRange(this.SelPawnForGear.inventory.innerContainer);
            for (int i = 0; i < Sandy_Detailed_RPG_GearTab.workingInvList.Count; i++)
            {
                this.DrawThingRow(ref num, viewRect.width, Sandy_Detailed_RPG_GearTab.workingInvList[i], true);
            }
            Sandy_Detailed_RPG_GearTab.workingInvList.Clear();

            if (Event.current.type == EventType.Layout)
            {
                this.scrollViewHeight = num + 30f;
            }

            Widgets.EndScrollView();
            TryDrawCEloadout(0, position.height - 60f, viewRect.width);
            GUI.EndGroup();
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            benchmarker.Stop();
        }

        private void DrawColonist(Rect rect, Pawn pawn)
        {
            Vector2 pos = new Vector2(rect.width, rect.height);
            GUI.DrawTexture(rect, PortraitsCache.Get(pawn, pos, PawnTextureCameraOffset, 1.28205f));
        }

        private void DrawThingRow1(Rect rect, Thing thing, bool inventory = false)
        {
            QualityCategory c;
            if (thing.TryGetQuality(out c))
            {
                switch(c)
                {
                    case QualityCategory.Legendary:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Legendary", true));
                        break;
                    }
                    case QualityCategory.Masterwork:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Masterwork", true));
                        break;
                    }
                    case QualityCategory.Excellent:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Excellent", true));
                        break;
                    }
                    case QualityCategory.Good:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Good", true));
                        break;
                    }
                    case QualityCategory.Normal:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Normal", true));
                        break;
                    }
                    case QualityCategory.Poor:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Poor", true));
                        break;
                    }
                    case QualityCategory.Awful:
                    {
                        GUI.DrawTexture(rect, ContentFinder<Texture2D>.Get("UI/Frames/RPG_Awful", true));
                        break;
                    }
                }
            }
            float mass = thing.GetStatValue(StatDefOf.Mass, true) * (float)thing.stackCount;
            string smass = mass.ToString("G") + " kg";
            string text = thing.LabelCap;
            Rect rect5 = rect.ContractedBy(2f);
            float num2 = rect5.height * ((float) thing.HitPoints / (float) thing.MaxHitPoints);
            rect5.yMin = rect5.yMax - num2;
            rect5.height = num2;
            GUI.DrawTexture(rect5, SolidColorMaterials.NewSolidColorTexture(new Color(0.4f, 0.47f, 0.53f, 0.44f)));
            if ((float)thing.HitPoints <= ((float)thing.MaxHitPoints/2))
            {
                Rect tattered = rect5;
                GUI.DrawTexture(rect5, SolidColorMaterials.NewSolidColorTexture(new Color(1f, 0.5f, 0.31f, 0.44f)));
            }
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null)
            {
                Rect rect1 = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
                Widgets.ThingIcon(rect1, thing, 1f);
            }
            if (Mouse.IsOver(rect))
            {
                GUI.color = Sandy_Detailed_RPG_GearTab.HighlightColor;
                GUI.DrawTexture(rect, TexUI.HighlightTex);
                Widgets.InfoCardButton(rect.x, rect.y, thing);
                if (this.CanControl && (inventory || this.CanControlColonist || (this.SelPawnForGear.Spawned && !this.SelPawnForGear.Map.IsPlayerHome)))
                {
                    Rect rect2 = new Rect(rect.xMax - SmallIconSize, rect.y, SmallIconSize, SmallIconSize);
                    TooltipHandler.TipRegion(rect2, "DropThing".Translate());
                    if (Widgets.ButtonImage(rect2, ContentFinder<Texture2D>.Get("UI/Buttons/Drop", true)))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        this.InterfaceDrop(thing);
                    }
                }
            }
            Apparel apparel = thing as Apparel;
            if (apparel != null && this.SelPawnForGear.outfits != null && apparel.WornByCorpse)
            {
                Rect rect3 = new Rect(rect.xMax - 20f, rect.yMax - 20f, 20f, 20f);
                GUI.DrawTexture(rect3, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Tainted_Icon", true));
                TooltipHandler.TipRegion(rect3, "WasWornByCorpse".Translate());
            }
            if (apparel != null && this.SelPawnForGear.outfits != null && this.SelPawnForGear.outfits.forcedHandler.IsForced(apparel))
            {
                text = text + ", " + "ApparelForcedLower".Translate();
                Rect rect4 = new Rect(rect.x, rect.yMax - 20f, 20f, 20f);
                GUI.DrawTexture(rect4, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Forced_Icon", true));
                TooltipHandler.TipRegion(rect4, "ForcedApparel".Translate());
            }
            Text.WordWrap = true;
            string text2 = thing.DescriptionDetailed;
            string text3 = text + "\n" + text2 + "\n" + smass;
            if (thing.def.useHitPoints)
            {
                string text4 = text3;
                text3 = string.Concat(new object[]
                {
                    text4,
                    "\n",
                    thing.HitPoints,
                    " / ",
                    thing.MaxHitPoints
                });
            }
            TooltipHandler.TipRegion(rect, text3);
        }

        private void DrawMassInfo(Vector2 topLeft)
        {
            if (SelPawnForGear.Dead || !ShouldShowInventory(SelPawnForGear))
            {
                return;
            }
            Rect iconRect = new Rect(topLeft.x, topLeft.y, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_MassCarried_Icon", true));
            TooltipHandler.TipRegion(iconRect, "SandyMassCarried".Translate());

            float mass = MassUtility.GearAndInventoryMass(SelPawnForGear);
            float capacity = MassUtility.Capacity(SelPawnForGear, null);
            // No clue where that 6px come from. Arbitrary I guess.
            Rect textRect = new Rect(topLeft.x + SmallIconSize + 6f, topLeft.y + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(textRect, "SandyMassValue".Translate(mass.ToString("0.##"), capacity.ToString("0.##")));
        }

        private void DrawComfyTemperatureRange(Vector2 topLeft)
        {
            if (this.SelPawnForGear.Dead)
            {
                return;
            }
            Rect iconRect = new Rect(topLeft.x, topLeft.y, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("UI/Icons/Min_Temperature"));
            TooltipHandler.TipRegion(iconRect, "ComfyTemperatureRange".Translate());
            float statValue = SelPawnForGear.GetStatValue(StatDefOf.ComfyTemperatureMin);
            // No clue where that 6px come from. Arbitrary I guess.
            Rect textRect = new Rect(topLeft.x + SmallIconSize + 6f, topLeft.y + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(textRect, " " + statValue.ToStringTemperature("F0"));

            iconRect.Set(iconRect.x, iconRect.y + SmallIconSize + SmallIconMargin, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("UI/Icons/Max_Temperature"));
            TooltipHandler.TipRegion(iconRect, "ComfyTemperatureRange".Translate());
            statValue = SelPawnForGear.GetStatValue(StatDefOf.ComfyTemperatureMax);
            textRect.Set(textRect.x, textRect.y + SmallIconSize + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(textRect, " " + statValue.ToStringTemperature("F0"));
        }

        private void DrawOverallArmor(Rect rect, StatDef stat, string label, Texture image)
        {
            // Dark magic from vanilla code calculating armor value until the blank line.
            float num = 0f;
            float num2 = Mathf.Clamp01(this.SelPawnForGear.GetStatValue(stat, true) / 2f);
            List<BodyPartRecord> allParts = this.SelPawnForGear.RaceProps.body.AllParts;
            List<Apparel> list = (this.SelPawnForGear.apparel == null) ? null : this.SelPawnForGear.apparel.WornApparel;
            for (int i = 0; i < allParts.Count; i++)
            {
                float num3 = 1f - num2;
                if (list != null)
                {
                    for (int j = 0; j < list.Count; j++)
                    {
                        if (list[j].def.apparel.CoversBodyPart(allParts[i]))
                        {
                            float num4 = Mathf.Clamp01(list[j].GetStatValue(stat, true) / 2f);
                            num3 *= 1f - num4;
                        }
                    }
                }
                num += allParts[i].coverageAbs * (1f - num3);
            }
            num = Mathf.Clamp(num * 2f, 0f, 2f);

            Rect iconRect = new Rect(rect.x, rect.y, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, image);
            TooltipHandler.TipRegion(iconRect, label);
            // the 36px can make the percentage number look like it is centered in the field. Brilliant Sandy.
            Rect valRect = new Rect(rect.x + SmallIconSize + 36f, rect.y + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(valRect, num.ToStringPercent());
        }

        // xShift: how much to right to adjust the two bars
        private void TryDrawCEloadout(float xShift, float y, float width) {
            CompInventory comp = SelPawn.TryGetComp<CompInventory>();
            if (comp != null) {

                PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_InventoryWeightBulk, KnowledgeAmount.FrameDisplayed);
                // adjust rects if comp found
                Rect weightRect = new Rect(_margin + xShift, y + _margin / 2, width, _barHeight);
                Rect bulkRect = new Rect(_margin + xShift, weightRect.yMax + _margin / 2, width, _barHeight);

                // draw bars
                Utility_Loadouts.DrawBar(bulkRect, comp.currentBulk, comp.capacityBulk, "CE_Bulk".Translate(), SelPawn.GetBulkTip());
                Utility_Loadouts.DrawBar(weightRect, comp.currentWeight, comp.capacityWeight, "CE_Weight".Translate(), SelPawn.GetWeightTip());

                // draw text overlays on bars
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;

                string currentBulk = CE_StatDefOf.CarryBulk.ValueToString(comp.currentBulk, CE_StatDefOf.CarryBulk.toStringNumberSense);
                string capacityBulk = CE_StatDefOf.CarryBulk.ValueToString(comp.capacityBulk, CE_StatDefOf.CarryBulk.toStringNumberSense);
                Widgets.Label(bulkRect, currentBulk + "/" + capacityBulk);

                string currentWeight = comp.currentWeight.ToString("0.#");
                string capacityWeight = CE_StatDefOf.CarryWeight.ValueToString(comp.capacityWeight, CE_StatDefOf.CarryWeight.toStringNumberSense);
                Widgets.Label(weightRect, currentWeight + "/" + capacityWeight);

                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        /*
         * Everything below is duplicated from the base class since they are private to it. Damn, Tynan.
         */

        private void DrawThingRow(ref float y, float width, Thing thing, bool inventory = false)
        {
            Rect rect = new Rect(0f, y, width, 28f);
            Widgets.InfoCardButton(rect.width - SmallIconSize, y, thing);
            rect.width -= SmallIconSize;
            if (this.CanControl && (inventory || this.CanControlColonist || (this.SelPawnForGear.Spawned && !this.SelPawnForGear.Map.IsPlayerHome)))
            {
                Rect rect2 = new Rect(rect.width - SmallIconSize, y, SmallIconSize, SmallIconSize);
                TooltipHandler.TipRegion(rect2, "DropThing".Translate());
                if (Widgets.ButtonImage(rect2, ContentFinder<Texture2D>.Get("UI/Buttons/Drop", true)))
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                    this.InterfaceDrop(thing);
                }
                rect.width -= SmallIconSize;
            }
            if (this.CanControlColonist)
            {
                if ((thing.def.IsNutritionGivingIngestible || thing.def.IsNonMedicalDrug) && thing.IngestibleNow && base.SelPawn.WillEat(thing, null))
                {
                    Rect rect3 = new Rect(rect.width - SmallIconSize, y, SmallIconSize, SmallIconSize);
                    TooltipHandler.TipRegion(rect3, "ConsumeThing".Translate(thing.LabelNoCount, thing));
                    if (Widgets.ButtonImage(rect3, ContentFinder<Texture2D>.Get("UI/Buttons/Ingest", true)))
                    {
                        SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
                        this.InterfaceIngest(thing);
                    }
                }
                rect.width -= SmallIconSize;
            }
            Rect rect4 = rect;
            rect4.xMin = rect4.xMax - 60f;
            CaravanThingsTabUtility.DrawMass(thing, rect4);
            rect.width -= 60f;
            if (Mouse.IsOver(rect))
            {
                GUI.color = Sandy_Detailed_RPG_GearTab.HighlightColor;
                GUI.DrawTexture(rect, TexUI.HighlightTex);
            }
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing, 1f);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Sandy_Detailed_RPG_GearTab.ThingLabelColor;
            Rect rect5 = new Rect(36f, y, rect.width - 36f, rect.height);
            string text = thing.LabelCap;
            Apparel apparel = thing as Apparel;
            if (apparel != null && this.SelPawnForGear.outfits != null && this.SelPawnForGear.outfits.forcedHandler.IsForced(apparel))
            {
                text = text + ", " + "ApparelForcedLower".Translate();
            }
            Text.WordWrap = false;
            Widgets.Label(rect5, text.Truncate(rect5.width, null));
            Text.WordWrap = true;
            string text2 = thing.DescriptionDetailed;
            if (thing.def.useHitPoints)
            {
                string text3 = text2;
                text2 = string.Concat(new object[]
                {
                    text3,
                    "\n",
                    thing.HitPoints,
                    " / ",
                    thing.MaxHitPoints
                });
            }
            TooltipHandler.TipRegion(rect, text2);
            y += 28f;
        }

        private bool CanControl
        {
            get
            {
                Pawn selPawnForGear = this.SelPawnForGear;
                return !selPawnForGear.Downed && !selPawnForGear.InMentalState
                    && (selPawnForGear.Faction == Faction.OfPlayer || selPawnForGear.IsPrisonerOfColony)
                    && (!selPawnForGear.IsPrisonerOfColony || !selPawnForGear.Spawned || selPawnForGear.Map.mapPawns.AnyFreeColonistSpawned)
                    && (!selPawnForGear.IsPrisonerOfColony || (!PrisonBreakUtility.IsPrisonBreaking(selPawnForGear) && (selPawnForGear.CurJob == null || !selPawnForGear.CurJob.exitMapOnArrival)));
            }
        }

        private bool CanControlColonist
        {
            get
            {
                return this.CanControl && this.SelPawnForGear.IsColonistPlayerControlled;
            }
        }

        private Pawn SelPawnForGear
        {
            get
            {
                if (base.SelPawn != null)
                {
                    return base.SelPawn;
                }
                Corpse corpse = base.SelThing as Corpse;
                if (corpse != null)
                {
                    return corpse.InnerPawn;
                }
                throw new InvalidOperationException("Gear tab on non-pawn non-corpse " + base.SelThing);
            }
        }

        private void InterfaceDrop(Thing t)
        {
            ThingWithComps thingWithComps = t as ThingWithComps;
            Apparel apparel = t as Apparel;
            if (apparel != null && this.SelPawnForGear.apparel != null && this.SelPawnForGear.apparel.WornApparel.Contains(apparel))
            {
                this.SelPawnForGear.jobs.TryTakeOrderedJob(new Job(JobDefOf.RemoveApparel, apparel), JobTag.Misc);
            }
            else if (thingWithComps != null && this.SelPawnForGear.equipment != null && this.SelPawnForGear.equipment.AllEquipmentListForReading.Contains(thingWithComps))
            {
                this.SelPawnForGear.jobs.TryTakeOrderedJob(new Job(JobDefOf.DropEquipment, thingWithComps), JobTag.Misc);
            }
            else if (!t.def.destroyOnDrop)
            {
                Thing thing;
                this.SelPawnForGear.inventory.innerContainer.TryDrop(t, this.SelPawnForGear.Position, this.SelPawnForGear.Map, ThingPlaceMode.Near, out thing, null, null);
            }
        }

        private void InterfaceIngest(Thing t)
        {
            Job job = new Job(JobDefOf.Ingest, t);
            job.count = Mathf.Min(t.stackCount, t.def.ingestible.maxNumToIngestAtOnce);
            job.count = Mathf.Min(job.count, FoodUtility.WillIngestStackCountOf(this.SelPawnForGear, t.def, t.GetStatValue(StatDefOf.Nutrition, true)));
            this.SelPawnForGear.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private bool ShouldShowInventory(Pawn p)
        {
            return p.RaceProps.Humanlike || p.inventory.innerContainer.Any;
        }

        private bool ShouldShowApparel(Pawn p)
        {
            return p.apparel != null && (p.RaceProps.Humanlike || p.apparel.WornApparel.Any<Apparel>());
        }

        private bool ShouldShowEquipment(Pawn p)
        {
            return p.equipment != null;
        }
    }
}