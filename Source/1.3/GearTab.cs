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
#if DEBUG
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
#endif
    
    [StaticConstructorOnStartup]
    public class Sandy_Detailed_RPG_GearTab : CombatExtended.ITab_Inventory
    {
#if DEBUG
        private Benchmarker benchmarker = new Benchmarker();
#endif

        private Vector2 scrollPosition = Vector2.zero;

        private float scrollViewHeight;

        // DrawColonist consts
        public static readonly Vector3 PawnTextureCameraOffset = new Vector3(0f, 0f, 0f);

        // Inventory list vars
        private static List<Thing> workingInvList = new List<Thing>();

        // RPG inventory GUI consts
        private const float CheckboxHeight = 20f;
        private const float CEAreaHeight = 60f;

        private const float MainItemSize = 64f;
        private const float MainItemMargin = 10f;
        private const float MiscItemSize = 56f;
        private const float MiscItemMargin = 7f;
        private const float MainEquipmentSize = 72f;
        private const float EquipmentMargin = 8f;
        private const float MediumMargin = 6f;

        private const float MainItemAreaX = MiscItemSize + 2 * MainItemMargin;
        private const float MiscItemAreaX = MainItemMargin;

        private const float SmallIconSize = 24f;
        private const float SmallIconMargin = 2f;

        // 374 = 2 miscItem + 3 mainItem in a row + 10px margin in between each of them + two 10px margins
        // on the side + another 10px margin
        private const float statBoxX = 2 * MiscItemSize + 3 * MainItemSize + (4 + 2 + 1) * MainItemMargin;
        private const float statBoxWidth = 128f;

        // Used too many times per tick; keep only one instance and only Get it once to
        // save 80 microsec (-10% time) per tick.
        private static Texture2D itemBackground = null;

        private Vector2 equipmentsAreaTopLeft;

        private static readonly Color itemNonTatteredHPColor = new Color(0.4f, 0.47f, 0.53f, 0.44f);
        private static readonly Color itemTatteredHPColor = new Color(1f, 0.5f, 0.31f, 0.44f);

        #region CE_Field
        private const float _barHeight = 20f;
        private const float _margin = 15f;
        #endregion CE_Field

        private bool viewlist = false;

        public Sandy_Detailed_RPG_GearTab()
        {
            size = new Vector2(Sandy_Detailed_RPG_Settings.rpgTabWidth, Sandy_Detailed_RPG_Settings.rpgTabHeight);
            labelKey = "TabGear";
            tutorTag = "Gear";
        }
        protected override void UpdateSize()
        {
            this.size = new Vector2(Sandy_Detailed_RPG_Settings.rpgTabWidth, Sandy_Detailed_RPG_Settings.rpgTabHeight);
        }

        public override void FillTab()
        {
#if DEBUG
            benchmarker.Start();
#endif
            Text.Font = GameFont.Small;
            Rect checkBox = new Rect(20f, 0f, 140f, 30f);
            Widgets.CheckboxLabeled(checkBox, "Sandy_ViewList".Translate(), ref viewlist, false, null, null, false);

            // Delegate to vanilla Filltab (sans drawing CE loadout bars) if show as list is chosen, or if the pawn is not human
            // (e.g. muffalo with cargo)
            if (viewlist || !SelPawnForGear.RaceProps.Humanlike)
            {
                // Set an enclosing GUI group that contains the group from base.FillTab
                // and the CE loadout bar.
                Rect listViewPosition = new Rect(0f, MainItemMargin, size.x, size.y);
                GUI.BeginGroup(listViewPosition);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;

                // Hack. Vanilla Filltab use size.y to set BeginGroup. Change it here so the list GUI
                // group doesn't overlap with CE loadout bars.
                // Not needed if inheriting CE's inventory tab since it draws the bars
                //
                // size.Set(size.x, size.y - CEAreaHeight);
                base.FillTab();
                // Restore the size.y, otherwise the tab will shrink by 60px per frame.
                //
                // size.Set(size.x, size.y + CEAreaHeight);

                // Shift the bar to compensate for the margin not set in current GUI group;
                // same about the y.
                //
                // TryDrawCEloadout(MainItemMargin, listViewPosition.height - CEAreaHeight - MainItemMargin, listViewPosition.width - CheckboxHeight - MainItemMargin * 2);
                GUI.EndGroup();
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
#if DEBUG
                benchmarker.Stop();
#endif
                return;
            }

            if (itemBackground == null) itemBackground = ContentFinder<Texture2D>.Get("UI/Widgets/DesButBG");

            Rect rect = new Rect(0f, CheckboxHeight, size.x, size.y - CheckboxHeight);
            Rect rect2 = rect.ContractedBy(MainItemMargin);
            Rect position = new Rect(rect2.x, rect2.y, rect2.width, rect2.height);

            GUI.BeginGroup(position);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Rect outRect = new Rect(0f, 0f, position.width, position.height - CEAreaHeight);
            Rect viewRect = new Rect(0f, 0f, position.width - MainItemMargin * 2, scrollViewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float num = 0f;

            // Draw always shown sections: mass/temp/armor, background for main body parts, and pawn portrait
            float statBoxYMax = DrawStatBox();
            DrawMainItemAreaBackground();
            Rect pawnRect = new Rect(statBoxX, statBoxYMax + MiscItemMargin, statBoxWidth, statBoxWidth);
            DrawColonist(pawnRect, SelPawnForGear);
            equipmentsAreaTopLeft = new Vector2(statBoxX + EquipmentMargin / 2, pawnRect.yMax);
            DrawEquipmentsAreaBackground();

            if (ShouldShowEquipment(SelPawnForGear))
            {
                DrawEquipments();
            }

            if (ShouldShowApparel(SelPawnForGear))
            {
                foreach (Apparel current2 in SelPawnForGear.apparel.WornApparel)
                {
                    var bodyPartGroups = current2.def.apparel.bodyPartGroups;
                    var layers = current2.def.apparel.layers;
                    //Head
                    if ((bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead) || bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead))
                        && (layers.Contains(ApparelLayerDefOf.Overhead)))
                    {
                        Rect newRect = RectAtMainItemArea(1, 0);
                        DrawThingRow1(newRect, current2, false);
                        continue;
                    }
                    else if ((bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead) || bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead))
                        && Sandy_Gear_DefOf.ContainsCEHeadLayer(layers))
                    {
                        Rect newRect = RectAtMainItemArea(1, 1);
                        DrawThingRow1(newRect, current2, false);
                        continue;
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes) && !bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead)
                        && (layers.Contains(ApparelLayerDefOf.Overhead) || Sandy_Gear_DefOf.ContainsCEHeadLayer(layers)))
                    {
                        Rect newRect = RectAtMainItemArea(2, 0);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Teeth) && !bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead)
                        && (layers.Contains(ApparelLayerDefOf.Overhead) || Sandy_Gear_DefOf.ContainsCEHeadLayer(layers))
                        && !bodyPartGroups.Contains(BodyPartGroupDefOf.Eyes))
                    {
                        Rect newRect = RectAtMainItemArea(0, 0);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Torso
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && layers.Contains(ApparelLayerDefOf.Middle))
                    {
                        Rect newRect = RectAtMainItemArea(0, 2);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && layers.Contains(ApparelLayerDefOf.OnSkin))
                    {
                        Rect newRect = RectAtMainItemArea(1, 2);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMainItemArea(2, 2);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Belt
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Waist) && layers.Contains(ApparelLayerDefOf.Belt))
                    {
                        Rect newRect = RectAtMainItemArea(1, 3);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Jetpack
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Waist) && layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMainItemArea(2, 3);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Legs
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) && layers.Contains(ApparelLayerDefOf.Middle))
                    {
                        Rect newRect = RectAtMainItemArea(0, 4);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) && layers.Contains(ApparelLayerDefOf.OnSkin))
                    {
                        Rect newRect = RectAtMainItemArea(1, 4);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.Legs) && layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMainItemArea(2, 4);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Feet
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Feet) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Legs)
                        && layers.Contains(ApparelLayerDefOf.Middle))
                    {
                        Rect newRect = RectAtMainItemArea(0, 5);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Feet) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Legs)
                        && layers.Contains(ApparelLayerDefOf.OnSkin))
                    {
                        Rect newRect = RectAtMainItemArea(1, 5);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Feet) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Legs)
                        && (layers.Contains(ApparelLayerDefOf.Shell) || layers.Contains(ApparelLayerDefOf.Overhead)))
                    {
                        Rect newRect = RectAtMainItemArea(2, 5);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Hands
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && layers.Contains(ApparelLayerDefOf.Middle) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 2);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    // Hands cont. - Removed shoulder check to allow some gloves to show up here
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && (layers.Contains(ApparelLayerDefOf.Shell) || layers.Contains(ApparelLayerDefOf.Overhead)))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && layers.Contains(ApparelLayerDefOf.OnSkin) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 3);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Shoulders
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) && !layers.Contains(ApparelLayerDefOf.Shell)
                        && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && !bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)
                        && layers.Contains(ApparelLayerDefOf.Middle) && !bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 3);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) && layers.Contains(ApparelLayerDefOf.Shell)
                        && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && !bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)
                        && !bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 2);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) && !layers.Contains(ApparelLayerDefOf.Shell)
                        && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && !bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)
                        && layers.Contains(ApparelLayerDefOf.OnSkin) && !layers.Contains(ApparelLayerDefOf.Middle)
                        && !bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //RightHand
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && layers.Contains(ApparelLayerDefOf.Middle) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 5);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && layers.Contains(ApparelLayerDefOf.Shell) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 6);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && layers.Contains(ApparelLayerDefOf.OnSkin) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !layers.Contains(ApparelLayerDefOf.Middle) && !layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 4);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //LeftHand
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && layers.Contains(ApparelLayerDefOf.Middle) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 5);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && layers.Contains(ApparelLayerDefOf.Shell) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 6);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        && layers.Contains(ApparelLayerDefOf.OnSkin) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                        && !layers.Contains(ApparelLayerDefOf.Middle) && !layers.Contains(ApparelLayerDefOf.Shell))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 4);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    //Neck
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders)
                             && layers.Contains(ApparelLayerDefOf.Belt) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = RectAtMainItemArea(0, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders)
                             && (layers.Contains(ApparelLayerDefOf.Overhead) || Sandy_Gear_DefOf.ContainsCEHeadLayer(layers))
                             && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = RectAtMainItemArea(1, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && !bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders)
                             && layers.Contains(ApparelLayerDefOf.Shell) && !bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = RectAtMainItemArea(2, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }

                    //this part add jewelry support
                    //They currently overlape with some appearoll 2 stuff
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Neck) && (layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(Sandy_Gear_DefOf.Ears) && (layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand) && (layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = RectAtMiscItemArea(0, 0);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (bodyPartGroups.Contains(BodyPartGroupDefOf.RightHand) && (layers.Contains(Sandy_Gear_DefOf.Accessories)))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 0);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    // CE TacVest
                    else if (layers.Contains(Sandy_Gear_DefOf.Webbing) && bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = RectAtMainItemArea(0, 3);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    // CE Backpack
                    else if (layers.Contains(Sandy_Gear_DefOf.Backpack) && bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = RectAtMainItemArea(2, 3);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if ((layers.Contains(Sandy_Gear_DefOf.Backpack) || layers.Contains(Sandy_Gear_DefOf.Webbing))
                        && bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 3);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    // CE Shield, Belt layer to make anything not yet moved to Shield-layer-style compatible
                    else if ((layers.Contains(Sandy_Gear_DefOf.Shield) || layers.Contains(ApparelLayerDefOf.Belt))
                        && (bodyPartGroups.Contains(Sandy_Gear_DefOf.Shoulders) || bodyPartGroups.Contains(Sandy_Gear_DefOf.Arms) || bodyPartGroups.Contains(Sandy_Gear_DefOf.Hands)
                        || bodyPartGroups.Contains(Sandy_Gear_DefOf.LeftShoulder) || bodyPartGroups.Contains(Sandy_Gear_DefOf.LeftArm) || bodyPartGroups.Contains(BodyPartGroupDefOf.LeftHand)))
                    {
                        Rect newRect = RectAtEquipmentArea(1, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    // Intended for the Exoframe from Vanilla Weapon Expended. I suspect that this definition might be too wide but we will see.
                    else if (layers.Contains(ApparelLayerDefOf.Belt) && bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))
                    {
                        Rect newRect = RectAtEquipmentArea(0, 1);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                    else if (layers.Contains(Sandy_Gear_DefOf.Satchel) && bodyPartGroups.Contains(Sandy_Gear_DefOf.Waist))
                    {
                        Rect newRect = RectAtMiscItemArea(1, 6);
                        GUI.DrawTexture(newRect, itemBackground);
                        DrawThingRow1(newRect, current2, false);
                    }
                }
            }

            // Do not check if should show (text) inventory for pawn; the pawn being humanlike is suffice, and
            // at this point the pawn must be humanlike.
            // 440 seems to be couple pxs below the end of item area on Y axis.
            num = 440f;
            Widgets.ListSeparator(ref num, viewRect.width, "Inventory".Translate());
            Sandy_Detailed_RPG_GearTab.workingInvList.Clear();
            Sandy_Detailed_RPG_GearTab.workingInvList.AddRange(SelPawnForGear.inventory.innerContainer);
            for (int i = 0; i < Sandy_Detailed_RPG_GearTab.workingInvList.Count; i++)
            {
                DrawThingRow(ref num, viewRect.width, Sandy_Detailed_RPG_GearTab.workingInvList[i], true);
            }
            Sandy_Detailed_RPG_GearTab.workingInvList.Clear();

            if (Event.current.type == EventType.Layout)
            {
                scrollViewHeight = num + 30f;
            }

            Widgets.EndScrollView();
            TryDrawCEloadout(0, position.height - 60f, viewRect.width);
            GUI.EndGroup();
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
#if DEBUG
            benchmarker.Stop();
#endif
        }

        private void DrawColonist(Rect rect, Pawn pawn)
        {
            Vector2 pos = new Vector2(rect.width, rect.height);
            GUI.DrawTexture(rect, PortraitsCache.Get(pawn, pos, Verse.Rot4.South, PawnTextureCameraOffset, 1.28205f));
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
            Texture2D durationColor = ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Not_Tattered");
            if ((float)thing.HitPoints <= ((float)thing.MaxHitPoints / 2))
            {
                durationColor = ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Tattered");
            }
            GUI.DrawTexture(rect5, durationColor);
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null)
            {
                Rect rect1 = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
                Widgets.ThingIcon(rect1, thing, 1f);
            }

            bool flag = false;
            if (Mouse.IsOver(rect))
            {
                GUI.color = HighlightColor;
                GUI.DrawTexture(rect, TexUI.HighlightTex);
                Widgets.InfoCardButton(rect.x, rect.y, thing);
                if (CanControl && (inventory || CanControlColonist || (SelPawnForGear.Spawned && !SelPawnForGear.Map.IsPlayerHome)))
                {
                    Rect rect2 = new Rect(rect.xMax - SmallIconSize, rect.y, SmallIconSize, SmallIconSize);
                    bool flag2 = this.SelPawnForGear.IsQuestLodger() && !(thing is Apparel);
					Apparel apparel;
					bool flag3 = (apparel = (thing as Apparel)) != null && this.SelPawnForGear.apparel != null && this.SelPawnForGear.apparel.IsLocked(apparel);
					flag = (flag2 || flag3);
					if (Mouse.IsOver(rect2))
					{
						if (flag3)
						{
							TooltipHandler.TipRegion(rect2, "DropThingLocked".Translate());
						}
						else if (flag2)
						{
							TooltipHandler.TipRegion(rect2, "DropThingLodger".Translate());
						}
						else
						{
							TooltipHandler.TipRegion(rect2, "DropThing".Translate());
						}
					}
					Color color = flag ? Color.grey : Color.white;
					Color mouseoverColor = flag ? color : GenUI.MouseoverColor;
					if (Widgets.ButtonImage(rect2, ContentFinder<Texture2D>.Get("UI/Buttons/Drop", true), color, mouseoverColor, !flag) && !flag)
					{
						SoundDefOf.Tick_High.PlayOneShotOnCamera(null);
						this.InterfaceDrop(thing);
					}
                }
            }

            Apparel apparel2 = thing as Apparel;
            if (apparel2 != null && SelPawnForGear.outfits != null)
            {
                if (apparel2.WornByCorpse)
                {
                    Rect rect3 = new Rect(rect.xMax - SmallIconSize, rect.yMax - SmallIconSize, SmallIconSize, SmallIconSize);
                    GUI.DrawTexture(rect3, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Tainted_Icon", true));
                    TooltipHandler.TipRegion(rect3, "WasWornByCorpse".Translate());
                }
                if (SelPawnForGear.outfits.forcedHandler.IsForced(apparel2))
                {
                    text += ", " + "ApparelForcedLower".Translate();
                    Rect rect4 = new Rect(rect.x, rect.yMax - SmallIconSize, SmallIconSize, SmallIconSize);
                    GUI.DrawTexture(rect4, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Forced_Icon", true));
                    TooltipHandler.TipRegion(rect4, "ForcedApparel".Translate());
                }
            }

            if (flag)
            {
                text += " (" + "ApparelLockedLower".Translate() + ")";
            }

            Text.WordWrap = true;
            string text2 = $"{text}\n{thing.DescriptionDetailed}\n{smass}";
            if (thing.def.useHitPoints)
            {
                string text3 = text2;
                text2 = $"{text3}\n{thing.HitPoints} / {thing.MaxHitPoints}";
            }
            TooltipHandler.TipRegion(rect, text2);
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
            Rect textRect = new Rect(topLeft.x + SmallIconSize + MediumMargin, topLeft.y + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(textRect, "SandyMassValue".Translate(mass.ToString("0.##"), capacity.ToString("0.##")));
        }

        private void DrawComfyTemperatureRange(Vector2 topLeft)
        {
            if (SelPawnForGear.Dead)
            {
                return;
            }
            Rect iconRect = new Rect(topLeft.x, topLeft.y, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("UI/Icons/Min_Temperature"));
            TooltipHandler.TipRegion(iconRect, "ComfyTemperatureRange".Translate());
            float statValue = SelPawnForGear.GetStatValue(StatDefOf.ComfyTemperatureMin);
            Rect textRect = new Rect(topLeft.x + SmallIconSize + MediumMargin, topLeft.y + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(textRect, " " + statValue.ToStringTemperature("F0"));

            iconRect.Set(iconRect.x, iconRect.y + SmallIconSize + SmallIconMargin, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, ContentFinder<Texture2D>.Get("UI/Icons/Max_Temperature"));
            TooltipHandler.TipRegion(iconRect, "ComfyTemperatureRange".Translate());
            statValue = SelPawnForGear.GetStatValue(StatDefOf.ComfyTemperatureMax);
            textRect.Set(textRect.x, textRect.y + SmallIconSize + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            Widgets.Label(textRect, " " + statValue.ToStringTemperature("F0"));
        }

        private string formatArmorValue(float value, string unit)
        {
            var asPercent = unit.Equals("%");
            if (asPercent)
            {
                value *= 100f;
            }
            return value.ToStringByStyle(asPercent ? ToStringStyle.FloatMaxOne : ToStringStyle.FloatMaxTwo) + unit;
        }

        private void DrawOverallArmor(Rect rect, StatDef stat, string label, string unit, Texture image)
        {
            Rect iconRect = new Rect(rect.x, rect.y, SmallIconSize, SmallIconSize);
            GUI.DrawTexture(iconRect, image);
            TooltipHandler.TipRegion(iconRect, label);

            Rect valRect = new Rect(rect.x + SmallIconSize + MediumMargin, rect.y + SmallIconMargin, statBoxWidth - SmallIconSize, SmallIconSize);
            float num = 0f;
            List<Apparel> wornApparel = SelPawnForGear.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                num += wornApparel[i].GetStatValue(stat, true) * wornApparel[i].def.apparel.HumanBodyCoverage;
            }
            if (num > 0.005f)
            {
                BodyPartRecord bpr = new BodyPartRecord();
                List<BodyPartRecord> bpList = SelPawnForGear.RaceProps.body.AllParts;
                string text = "";
                for (int i = 0; i < bpList.Count; i++)
                {
                    float armorValue = 0f;
                    BodyPartRecord part = bpList[i];
                    if (part.depth == BodyPartDepth.Outside && (part.coverage >= 0.1 || (part.def == BodyPartDefOf.Eye || part.def == BodyPartDefOf.Neck)))
                    {
                        text += part.LabelCap + ": ";
                        for (int j = wornApparel.Count - 1; j >= 0; j--)
                        {
                            Apparel apparel = wornApparel[j];
                            if (apparel.def.apparel.CoversBodyPart(part))
                            {
                                armorValue += apparel.GetStatValue(stat, true);
                            }
                        }
                        text += formatArmorValue(armorValue, unit) + "\n";
                    }
                }
                TooltipHandler.TipRegion(valRect, text);
                Widgets.Label(valRect, formatArmorValue(num, unit));
            }
            else
            {
                Widgets.Label(valRect, formatArmorValue(0.0f, unit));
            }
        }

        private float DrawStatBox()
        {
            var massStart = new Vector2(statBoxX, 0f);
            DrawMassInfo(massStart);
            var tempStart = new Vector2(statBoxX, SmallIconSize + SmallIconMargin);
            DrawComfyTemperatureRange(tempStart);

            // Don't check if should show armor for pawn. Being humanlike is suffice to show
            // armor, and the pawn must be humanlike at this point.
            Rect armorRect = new Rect(statBoxX, tempStart.y + 2 * (SmallIconSize + SmallIconMargin) + MediumMargin,
                statBoxWidth, 3 * SmallIconSize + 4 * SmallIconMargin);
            TooltipHandler.TipRegion(armorRect, "OverallArmor".Translate());
            Rect rectsharp = new Rect(armorRect.x, armorRect.y, armorRect.width, SmallIconSize);
            DrawOverallArmor(rectsharp, StatDefOf.ArmorRating_Sharp, "ArmorSharp".Translate(), "CE_mmRHA".Translate(),
                ContentFinder<Texture2D>.Get("UI/Icons/Sandy_ArmorSharp_Icon"));
            Rect rectblunt = new Rect(armorRect.x, armorRect.y + SmallIconSize + 2 * SmallIconMargin,
                armorRect.width, SmallIconSize);
            DrawOverallArmor(rectblunt, StatDefOf.ArmorRating_Blunt, "ArmorBlunt".Translate(), " " + "CE_MPa".Translate(),
                ContentFinder<Texture2D>.Get("UI/Icons/Sandy_ArmorBlunt_Icon"));
            Rect rectheat = new Rect(armorRect.x, armorRect.y + 2 * (SmallIconSize + 2 * SmallIconMargin),
                armorRect.width, SmallIconSize);
            DrawOverallArmor(rectheat, StatDefOf.ArmorRating_Heat, "ArmorHeat".Translate(), "%",
                ContentFinder<Texture2D>.Get("UI/Icons/Sandy_ArmorHeat_Icon"));
            return armorRect.yMax;
        }

        private void DrawEquipments()
        {
            var current = SelPawnForGear.equipment.Primary;
            if (current == null) return;

            Rect itemRect = RectAtEquipmentArea(0, 0);
            GUI.DrawTexture(itemRect, itemBackground);
            DrawThingRow1(itemRect, current, false);
            if (SelPawnForGear.story.traits.HasTrait(TraitDefOf.Brawler) && current.def.IsRangedWeapon)
            {
                Rect rect6 = new Rect(itemRect.x, itemRect.yMax - SmallIconSize, SmallIconSize, SmallIconSize);
                GUI.DrawTexture(rect6, ContentFinder<Texture2D>.Get("UI/Icons/Sandy_Forced_Icon", true));
                TooltipHandler.TipRegion(rect6, "BrawlerHasRangedWeapon".Translate());
            }
        }

        // x and y to be 0-indexed (e.g. top left slot is x=0, y=0; the one right below it is x=0, y=1)
        private Rect RectAtMainItemArea(int x, int y)
        {
            return new Rect(MainItemAreaX + x * (MainItemSize + MainItemMargin), y * (MainItemSize + MainItemMargin), MainItemSize, MainItemSize);
        }
        
        private Rect RectAtMiscItemArea(int x, int y)
        {
            return new Rect(MiscItemAreaX + x * (3 * MainItemSize + MiscItemSize + 4 * MainItemMargin), y * (MiscItemSize + MiscItemMargin), MiscItemSize, MiscItemSize);
        }

        // y=0 for weapon, {x=1, y=1} for Vanilla Weapon Expended Exoframe, {x=1, y=1} for CE shield.
        private Rect RectAtEquipmentArea(int x, int y)
        {
            if (y == 0)
            {
                return new Rect(equipmentsAreaTopLeft.x + (statBoxWidth - MainEquipmentSize - EquipmentMargin) / 2, equipmentsAreaTopLeft.y, MainEquipmentSize, MainEquipmentSize);
            }
            return new Rect(equipmentsAreaTopLeft.x + SmallIconMargin + x * (MiscItemSize + MediumMargin), equipmentsAreaTopLeft.y + y * (MainEquipmentSize + MiscItemMargin), MiscItemSize, MiscItemSize);
        }

        private void DrawMainItemAreaBackground()
        {
            Rect bgRect;
            bgRect = RectAtMainItemArea(1, 0);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(MainItemMargin), "Sandy_Head".Translate());
            bgRect = RectAtMainItemArea(0, 2);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(MainItemMargin), "Sandy_TorsoMiddle".Translate());
            bgRect = RectAtMainItemArea(1, 2);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(MainItemMargin), "Sandy_TorsoOnSkin".Translate());
            bgRect = RectAtMainItemArea(2, 2);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(MainItemMargin), "Sandy_TorsoShell".Translate());
            bgRect = RectAtMainItemArea(1, 3);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(MainItemMargin), "Sandy_Belt".Translate());
            bgRect = RectAtMainItemArea(1, 4);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(MainItemMargin), "Sandy_Pants".Translate());
        }

        private void DrawEquipmentsAreaBackground()
        {
            Rect bgRect;
            bgRect = RectAtEquipmentArea(0, 0);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(EquipmentMargin), "Sandy_PrimaryEquipment".Translate());
            // CE doesn't support dual wield, so there is no secondary equipment per se.
            // bgRect = RectAtEquipmentArea(1, 0);
            // GUI.DrawTexture(bgRect, itemBackground);
            // TooltipHandler.TipRegion(bgRect.ContractedBy(EquipmentMargin), "Sandy_SecondaryEquipment".Translate());
            bgRect = RectAtEquipmentArea(0, 1);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(EquipmentMargin), "Sandy_VWE_Apparel_Exoframe".Translate());
            bgRect = RectAtEquipmentArea(1, 1);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(EquipmentMargin), "Sandy_ShieldLeft".Translate());
            /* Not until someone tells me there are right hand shield or dual wield shields lmfao.
            bgRect = RectAtEquipmentArea(-);
            GUI.DrawTexture(bgRect, itemBackground);
            TooltipHandler.TipRegion(bgRect.ContractedBy(EquipmentMargin), "Sandy_ShieldRight".Translate());
            */
        }

        // xShift: how much to right to adjust the two bars
        private void TryDrawCEloadout(float xShift, float y, float width) {
            CompInventory comp = SelPawn.TryGetComp<CompInventory>();
            if (comp == null)
            {
                return;
            }

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
        
        #region Duplicate_Base_Code
        /* ========================== Duplicate code ahead ==========================
         * Everything below is duplicated from the base class since they are private to it. Damn it, Tynan.
         */

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

        private void DrawThingRow(ref float y, float width, Thing thing, bool inventory = false)
        {
            Rect rect = new Rect(0f, y, width, 28f);
            Widgets.InfoCardButton(rect.width - SmallIconSize, y, thing);
            rect.width -= SmallIconSize;
            bool flag = false;
            if (this.CanControl && (inventory || this.CanControlColonist || (this.SelPawnForGear.Spawned && !this.SelPawnForGear.Map.IsPlayerHome)))
            {

                Rect rect2 = new Rect(rect.xMax - SmallIconSize, rect.y, SmallIconSize, SmallIconSize);
                bool flag2 = this.SelPawnForGear.IsQuestLodger() && !(thing is Apparel);
                Apparel apparel;
                bool flag3 = (apparel = (thing as Apparel)) != null && this.SelPawnForGear.apparel != null && this.SelPawnForGear.apparel.IsLocked(apparel);
                flag = (flag2 || flag3);
                if (Mouse.IsOver(rect2))
                {
                    if (flag3)
                    {
                        TooltipHandler.TipRegion(rect2, "DropThingLocked".Translate());
                    }
                    else if (flag2)
                    {
                        TooltipHandler.TipRegion(rect2, "DropThingLodger".Translate());
                    }
                    else
                    {
                        TooltipHandler.TipRegion(rect2, "DropThing".Translate());
                    }
                }
                Color color = flag ? Color.grey : Color.white;
                Color mouseoverColor = flag ? color : GenUI.MouseoverColor;
                if (Widgets.ButtonImage(rect2, ContentFinder<Texture2D>.Get("UI/Buttons/Drop", true), color, mouseoverColor, !flag) && !flag)
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
                GUI.color = HighlightColor;
                GUI.DrawTexture(rect, TexUI.HighlightTex);
            }
            if (thing.def.DrawMatSingle != null && thing.def.DrawMatSingle.mainTexture != null)
            {
                Widgets.ThingIcon(new Rect(4f, y, 28f, 28f), thing, 1f);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = ThingLabelColor;
            Rect rect5 = new Rect(36f, y, rect.width - 36f, rect.height);
            string text = thing.LabelCap;
            Apparel apparel2 = thing as Apparel;
            if (apparel2 != null && this.SelPawnForGear.outfits != null && this.SelPawnForGear.outfits.forcedHandler.IsForced(apparel2))
            {
                text += ", " + "ApparelForcedLower".Translate();
            }
            if (flag)
            {
                text += " (" + "ApparelLockedLower".Translate() + ")";
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
        #endregion Duplicate_Base_Code
    }
}
