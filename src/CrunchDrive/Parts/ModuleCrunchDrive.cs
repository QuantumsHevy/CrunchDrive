using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrunchDrive
{
    // The crunch drive as a part. It holds no crunch logic of its own: its presence on the craft is what permits a
    // crunch (see CrunchController.HasDrive), its right-click menu is the player's control panel, and it plays the
    // sound cues. The first drive on the active craft is the "primary" one; it owns the settings and the cues, and
    // any further drive just shows standby.
    public class ModuleCrunchDrive : PartModule
    {
        const string MainGroup = "CrunchDrive";
        const string TargetGroup = "CrunchDriveTarget";
        const string SettingsGroup = "CrunchDriveSettings";

        // Effect names. They must match the entries in the part's EFFECTS block.
        const string FxSpool = "crunchSpool";        // Loop while the Casimir blades spin up.
        const string FxReady = "crunchReady";        // One-shot when enough negative energy has accumulated.
        const string FxEngage = "crunchEngage";      // One-shot when a crunch field forms.
        const string FxField = "crunchField";        // Loop while a crunch field is up.
        const string FxCollapse = "crunchCollapse";  // One-shot when a crunch field collapses.

        // ---- Readouts: crunch field ----

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = CrunchText.FieldStatus, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string statusLine = CrunchText.PhaseWord(CrunchPhase.Idle, false);

        [KSPField(guiActive = true, guiActiveEditor = false, guiName = CrunchText.FieldCoefficient, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string coefficientLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldSpool, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string spoolLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldLeg, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string legLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldProgress, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string progressLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldSpeed, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string speedLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldTime, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string timeLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldTraveled, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string traveledLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldOffAxis, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string offAxisLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldOrbital, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string orbitalLine = "";

        // ---- Buttons ----

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Crunch", active = true, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void PrimaryEvent()
        {
            if (IsPrimary()) PushSettings();
            CrunchController.PrimaryAction();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = CrunchText.ButtonCancel, active = false, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void CancelEvent()
        {
            CrunchController.Cancel();
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false, guiName = "Direction", active = true, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public void DirectionEvent()
        {
            backwards = !backwards;
            if (IsPrimary()) PushSettings();
        }

        [KSPAction(CrunchText.ActionPrimary)]
        public void PrimaryAction(KSPActionParam param)
        {
            if (IsPrimary()) PushSettings();
            CrunchController.PrimaryAction();
        }

        [KSPAction(CrunchText.ButtonCancel)]
        public void CancelAction(KSPActionParam param)
        {
            CrunchController.Cancel();
        }

        // ---- Readouts: target and dropout zones ----

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldTarget, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string targetLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDistance, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string distanceLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldRange, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string rangeLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDraw, groupName = MainGroup, groupDisplayName = CrunchText.GroupTitle)]
        public string drawLine = "";

        // Electric charge per second this part's spool draws. Set it in the part's cfg (spoolEcPerSecond = ...); 0 uses CrunchTuning.SpoolEcPerSecond.
        [KSPField(isPersistant = false)]
        public double spoolEcPerSecond = 0.0;

        public double SpoolEcPerSecond { get { return spoolEcPerSecond > 0.0 ? spoolEcPerSecond : CrunchTuning.SpoolEcPerSecond; } }

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDropoutAltitude, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string dropoutAltitudeLine = "";

        [KSPField(guiActive = false, guiActiveEditor = false, guiName = CrunchText.FieldDropoutNearest, groupName = TargetGroup, groupDisplayName = CrunchText.GroupTargetTitle)]
        public string dropoutNearestLine = "";

        // ---- Settings (saved with the craft; set in the VAB/SPH only, never in flight) ----

        [KSPField(isPersistant = true)]
        public bool backwards = false;   // Orientation of the first leg. Changed with the direction button.

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.FieldMode, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_ChooseOption(scene = UI_Scene.Editor)]
        public string travelMode = CrunchText.ModeNames[0];

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.SliderDuration, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_FloatRange(minValue = 1f, maxValue = 120f, stepIncrement = 1f, scene = UI_Scene.Editor)]
        public float crunchSeconds = 10f;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.ToggleTwoLeg, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.Editor)]
        public bool twoLeg = true;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.ToggleCaptureAssist, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.Editor)]
        public bool captureAssist = true;

        [KSPField(isPersistant = true, guiActive = false, guiActiveEditor = true, guiName = CrunchText.ToggleSasRetrograde, groupName = SettingsGroup, groupDisplayName = CrunchText.GroupSettingsTitle, groupStartCollapsed = true),
         UI_Toggle(disabledText = CrunchText.ToggleOff, enabledText = CrunchText.ToggleOn, scene = UI_Scene.Editor)]
        public bool sasRetrograde = true;

        // Every readout field, so a drive that is not in control can hide them all.
        static readonly string[] ReadoutFields =
        {
            nameof(coefficientLine), nameof(spoolLine), nameof(legLine), nameof(progressLine), nameof(speedLine),
            nameof(timeLine), nameof(traveledLine), nameof(offAxisLine), nameof(orbitalLine),
            nameof(targetLine), nameof(distanceLine), nameof(rangeLine), nameof(drawLine),
            nameof(dropoutAltitudeLine), nameof(dropoutNearestLine)
        };

        static readonly string[] ButtonEvents = { nameof(PrimaryEvent), nameof(CancelEvent), nameof(DirectionEvent) };

        // The tube of light, parented to this part. Only the primary drive shows one.
        CrunchTube tube;
        bool tubeWasSpooling;

        // Emissive glow: the part's own materials that have an emissive colour are set directly, from 0 (grey) to 1 (full glow).
        // No ModuleColorChanger (or any other glow module) is involved, so nothing else can fight over the value.
        static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        readonly List<Material> glowMaterials = new List<Material>();   // This part's own copies of its emissive materials.
        bool glowCollected;
        float glow;
        float glowSent = -1f;

        CrunchPhase lastPhase = CrunchPhase.Idle;
        const float SpoolPitchFloorPower = 0.05f;   // Power that gives the lowest pitch; the cue's pitch curve in the part's config starts here.
        float spoolPitch = 1f;      // The pitch the spool loop had while the spool ran.
        float spoolFallStart = -1f; // When the loop began to fall (unscaled time); -1 = not falling.
        float spoolFallFrom;
        float spoolFallSeconds;
        float spoolPower = -1f;   // The power last sent to the spool cue (it drives the cue's pitch); -1 = none yet.
        bool wasField;
        bool menuDirty;                                                          // A field was shown or hidden; the open menu must be rebuilt.
        static float lastMenuRefresh = -10f;
        const float MenuRefreshGapSeconds = 0.25f;                               // Never rebuild the menu more often than this, whatever changes.

        // The first drive (in part order) on a craft, cached for a moment: scanning every part is wasteful at frame rate.
        static ModuleCrunchDrive cachedPrimary;
        static float cachedAt = -10f;
        const float CacheSeconds = 0.5f;

        public static ModuleCrunchDrive PrimaryFor(Vessel v)
        {
            if (v == null) return null;
            if (cachedPrimary != null && cachedPrimary.vessel == v && Time.unscaledTime - cachedAt < CacheSeconds) return cachedPrimary;

            cachedPrimary = null;
            for (int i = 0; i < v.parts.Count; i++)
            {
                ModuleCrunchDrive m = v.parts[i].Modules.GetModule<ModuleCrunchDrive>();
                if (m != null) { cachedPrimary = m; break; }
            }
            cachedAt = Time.unscaledTime;
            return cachedPrimary;
        }

        public static bool HasDrive(Vessel v) { return PrimaryFor(v) != null; }
        readonly Dictionary<string, double> heldCues = new Dictionary<string, double>();   // One-shot effect -> time to switch it off again.
        readonly List<string> releasedCues = new List<string>();

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            // The mode names live in CrunchText, so they are handed to the selector here rather than in the attribute.
            SetOptions(Fields[nameof(travelMode)].uiControlEditor as UI_ChooseOption);

            glowCollected = false;   // The glow materials are gathered on first use, once the part's meshes are set up.

            if (HighLogic.LoadedSceneIsFlight)
            {
                tube = new CrunchTube();
                tube.Attach(part);
            }
        }

        void OnDestroy()
        {
            if (tube != null) tube.Dispose();
            ReleaseGlowMaterials();
        }

        static void SetOptions(UI_ChooseOption option)
        {
            if (option != null) option.options = CrunchText.ModeNames;
        }

        public override void OnUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            CrunchPhase phase = CrunchController.CurrentPhase;

            UpdateGlow(phase);   // Every drive on the active craft glows, not just the primary one.

            if (!IsPrimary())
            {
                statusLine = CrunchText.StatusStandby;
                SetFieldsVisible(ReadoutFields, false);
                SetEventsActive(ButtonEvents, false);
                StopLoops();
                ReleaseCues();
                if (tube != null) tube.Tick(Time.deltaTime, false);
                tubeWasSpooling = false;
                lastPhase = phase;
                RefreshMenuIfNeeded();
                return;
            }

            PushSettings();

            bool turning = CrunchController.IsTurning;
            bool spooling = CrunchController.IsSpooling;
            bool crunching = phase == CrunchPhase.Crunching;
            bool field = crunching && !turning;
            bool hasTarget = CrunchController.TargetName.Length > 0;
            bool fixedTime = ModeIndex() == (int)TravelMode.FixedDuration;
            bool dropoutKnown = CrunchController.DropoutBodyName.Length > 0;

            // ---- Readouts: crunch field ----
            statusLine = CrunchText.PhaseWord(phase, turning);

            coefficientLine = CrunchText.FieldCoefficientValue(CrunchController.ActiveTierC);
            SetVisible(nameof(coefficientLine), phase != CrunchPhase.Idle);

            spoolLine = CrunchText.FieldSpoolValue(CrunchController.SpoolSecondsPaid, CrunchController.ActiveTierC);
            SetVisible(nameof(spoolLine), spooling);

            drawLine = CrunchText.FieldDrawValue(SpoolEcPerSecond);
            SetVisible(nameof(drawLine), !crunching);

            legLine = CrunchText.FieldLegValue(CrunchController.CurrentLeg, CrunchController.ActiveBackwards);
            SetVisible(nameof(legLine), CrunchController.IsTwoLeg);

            progressLine = CrunchText.FieldProgressValue(CrunchController.LegElapsed, CrunchController.LegDuration);
            SetVisible(nameof(progressLine), field);

            speedLine = CrunchText.FieldSpeedValue(CrunchController.LiveSpeedC);
            SetVisible(nameof(speedLine), field);

            timeLine = CrunchText.FieldTimeValue(CrunchController.LegElapsed, CrunchController.LegDuration);
            SetVisible(nameof(timeLine), field);

            traveledLine = CrunchText.FormatDistance(CrunchController.TraveledMeters);
            SetVisible(nameof(traveledLine), field);

            offAxisLine = CrunchText.FieldOffAxisValue(CrunchController.OffAxisDeg);
            SetVisible(nameof(offAxisLine), field);

            orbitalLine = CrunchText.FieldOrbitalValue(vessel.obt_velocity.magnitude, vessel.mainBody.name);
            SetVisible(nameof(orbitalLine), field);

            // ---- Readouts: target and dropout zones ----
            targetLine = CrunchController.TargetName;
            SetVisible(nameof(targetLine), hasTarget);

            distanceLine = CrunchText.DistanceToStop(CrunchController.TargetStopMeters, CrunchController.TargetIsBody);
            SetVisible(nameof(distanceLine), hasTarget);

            rangeLine = CrunchText.FieldRangeValue(crunchSeconds, CrunchController.ActiveTierC);
            SetVisible(nameof(rangeLine), fixedTime && (phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready));

            dropoutAltitudeLine = CrunchText.FieldDropoutAltitudeValue(CrunchController.DropoutAltitudeMeters, vessel.mainBody.name);
            SetVisible(nameof(dropoutAltitudeLine), dropoutKnown);

            dropoutNearestLine = CrunchText.FieldDropoutNearestValue(CrunchController.DropoutBodyName, CrunchController.DropoutEdgeMeters);
            SetVisible(nameof(dropoutNearestLine), dropoutKnown);

            // ---- Buttons ----
            BaseEvent primary = Events[nameof(PrimaryEvent)];
            bool canEngage = CrunchController.CanEngageFromSpool;
            primary.guiName = CrunchText.PrimaryButton(phase, backwards, turning, canEngage);
            primary.active = phase != CrunchPhase.Spooling || canEngage;

            Events[nameof(CancelEvent)].active = phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready;

            BaseEvent dir = Events[nameof(DirectionEvent)];
            dir.guiName = CrunchText.ButtonDirection(backwards);
            bool showDirection = !crunching && !(twoLeg && hasTarget);   // The automatic approach with a target picks the directions itself.
            if (dir.active != showDirection) { dir.active = showDirection; menuDirty = true; }

            UpdateSounds(phase, spooling, field);
            UpdateTube(spooling, field);
            lastPhase = phase;
            RefreshMenuIfNeeded();
        }

        // ---- Emissive glow ----
        // Rises with the charge the blades have drawn, holds at full while ready and while the field is up, then fades back to grey. The
        // mid-course turn counts as leaving the crunch: the glow fades while the craft turns and rises again with the second spool.

        // Finds every material on this part that has an emissive colour and takes a private copy of it, so only this part glows
        // (the part's materials are shared with every other copy of it). The copies are made once and kept.
        void CollectGlowMaterials()
        {
            ReleaseGlowMaterials();
            glowCollected = true;
            glowSent = -1f;

            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            int found = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Material shared = r.sharedMaterial;
                if (shared == null || !shared.HasProperty(EmissiveColorId)) continue;   // The tube of light, for one, has none.

                Material own = r.material;   // Makes this renderer's own copy.
                glowMaterials.Add(own);
                found++;
            }
            if (found == 0) Debug.Log(CrunchText.LogNoGlowMaterial);
            else Debug.Log(CrunchText.LogGlowMaterials(found, renderers.Length));
        }

        void ReleaseGlowMaterials()
        {
            for (int i = 0; i < glowMaterials.Count; i++)
                if (glowMaterials[i] != null) Destroy(glowMaterials[i]);
            glowMaterials.Clear();
        }

        void UpdateGlow(CrunchPhase phase)
        {
            if (!glowCollected) CollectGlowMaterials();
            if (glowMaterials.Count == 0) return;

            float target = 0f;
            if (vessel == FlightGlobals.ActiveVessel)
            {
                if (CrunchController.IsSpooling) target = Mathf.Clamp01((float)CrunchController.SpoolFraction);
                else if (phase == CrunchPhase.Ready || (phase == CrunchPhase.Crunching && !CrunchController.IsTurning)) target = 1f;
            }

            if (target >= glow) glow = target;
            else glow = Mathf.MoveTowards(glow, target, Time.deltaTime / (float)CrunchTuning.GlowFadeSeconds);

            // Only touch the materials when the value has actually moved.
            if (Mathf.Abs(glow - glowSent) < 0.002f && !(glow == 0f && glowSent != 0f)) return;
            glowSent = glow;

            Color emissive = new Color(glow, glow, glow, glow);   // The shader multiplies this with the part's emissive texture.
            for (int i = 0; i < glowMaterials.Count; i++)
            {
                glowMaterials[i].SetColor(EmissiveColorId, emissive);
            }
        }

        // ---- Tube of light ----
        // Shown for exactly as long as the crunch field is up: from engage until dropout, and not during the mid-course turn.
        // The craft is measured at the start of every spool, so the tube is ready when the field forms.

        void UpdateTube(bool spooling, bool field)
        {
            if (tube == null) return;

            // Measured at the start of every spool except the one between the legs: the craft is the same then, and measuring again
            // right after the turn gave a different (longer) tube for the second leg. Measured anyway if there is no measurement yet.
            if (spooling && !tubeWasSpooling && (!CrunchController.IsTurning || !tube.IsBuilt)) tube.Measure(part, vessel);
            tubeWasSpooling = spooling;

            tube.Tick(Time.deltaTime, field);
        }

        // ---- Settings sync ----

        // The controller reads CrunchTuning, so the primary drive copies its saved settings there.
        // While a crunch is running the controller keeps the values it planned with, so nothing is copied.
        void PushSettings()
        {
            if (CrunchController.CurrentPhase == CrunchPhase.Crunching) return;

            CrunchTuning.Backwards = backwards;
            CrunchTuning.Mode = (TravelMode)ModeIndex();
            CrunchTuning.CrunchSeconds = crunchSeconds;
            CrunchTuning.TwoLegManeuver = twoLeg;
            CrunchTuning.CaptureAssist = captureAssist;
            CrunchTuning.SasRetrograde = sasRetrograde;
        }

        int ModeIndex()
        {
            int i = Array.IndexOf(CrunchText.ModeNames, travelMode);
            return i < 0 ? 0 : i;
        }

        // Only the first drive on the active craft is in control.
        bool IsPrimary()
        {
            return vessel != null && vessel == FlightGlobals.ActiveVessel && PrimaryFor(vessel) == this;
        }

        // ---- Menu helpers ----

        void SetVisible(string field, bool on)
        {
            BaseField f = Fields[field];
            if (f == null || f.guiActive == on) return;
            f.guiActive = on;
            menuDirty = true;
        }

        void SetFieldsVisible(string[] names, bool on)
        {
            for (int i = 0; i < names.Length; i++) SetVisible(names[i], on);
        }

        void SetEventsActive(string[] names, bool on)
        {
            for (int i = 0; i < names.Length; i++) Events[names[i]].active = on;
        }

        // Showing or hiding a field only takes effect in an already-open menu once the window is told to redraw.
        void RefreshMenuIfNeeded()
        {
            if (!menuDirty || Time.unscaledTime - lastMenuRefresh < MenuRefreshGapSeconds) return;
            menuDirty = false;
            lastMenuRefresh = Time.unscaledTime;
            foreach (UIPartActionWindow w in FindObjectsOfType<UIPartActionWindow>())
                if (w.part == part) w.displayDirty = true;
        }

        // ---- Sound cues ----
        // Each cue is an entry in the part's EFFECTS block, started with Part.Effect(name, 1) and stopped with 0.
        // Loops stay on until switched off. One-shots are switched off again after CrunchTuning.CueHoldSeconds, so they can play again.

        void UpdateSounds(CrunchPhase phase, bool spooling, bool field)
        {
            ReleaseCues();

            UpdateSpoolCue(phase, spooling, field);

            if (field != wasField)
            {
                part.Effect(FxField, field ? 1f : 0f);
                OneShot(field ? FxEngage : FxCollapse);
                wasField = field;
            }

            if (lastPhase == CrunchPhase.Spooling && phase == CrunchPhase.Ready) OneShot(FxReady);
        }

        // The spool loop plays from the start of a spool: its pitch rises over the first moments and holds at normal while the
        // spool runs. When the spool ends (the ready cue plays, or the player engages straight from the spool, or it is cancelled)
        // the pitch falls to the floor and the loop stops. After an engage the fall takes as long as the tube takes to grow, so the
        // loop ends as the craft starts to move; otherwise it takes CrunchTuning.SpoolPitchFallSeconds. The pitch is carried by the
        // power sent to the cue (0.05 to 1; 0 stops it), and the cue's pitch curve in the part's config maps that power to the pitch.
        void UpdateSpoolCue(CrunchPhase phase, bool spooling, bool field)
        {
            float start = (float)CrunchTuning.SpoolPitchStart;
            float floor = (float)CrunchTuning.SpoolPitchFloor;
            float pitch = -1f;   // Negative = the loop is off.

            if (spooling)
            {
                float rise = CrunchTuning.SpoolPitchRiseSeconds > 0.0
                    ? Mathf.Clamp01((float)(CrunchController.SpoolElapsed / CrunchTuning.SpoolPitchRiseSeconds))
                    : 1f;
                pitch = Mathf.Lerp(start, 1f, rise);
                spoolPitch = pitch;
                spoolFallStart = -1f;
            }
            else if (spoolPower >= 0f)
            {
                // The spool has just ended and the loop is still on: let it fall.
                if (spoolFallStart < 0f)
                {
                    spoolFallStart = Time.unscaledTime;
                    spoolFallFrom = spoolPitch;
                    spoolFallSeconds = (float)(field ? CrunchTuning.TubeFadeInSeconds : CrunchTuning.SpoolPitchFallSeconds);
                }
                float done = spoolFallSeconds > 0f ? Mathf.Clamp01((Time.unscaledTime - spoolFallStart) / spoolFallSeconds) : 1f;
                if (done < 1f) pitch = Mathf.Lerp(spoolFallFrom, floor, done);
            }

            if (pitch < 0f)
            {
                if (spoolPower >= 0f) { part.Effect(FxSpool, 0f); spoolPower = -1f; }
                spoolFallStart = -1f;
                return;
            }

            float power = SpoolPitchFloorPower + (1f - SpoolPitchFloorPower) * Mathf.Clamp01((pitch - floor) / Mathf.Max(0.0001f, 1f - floor));
            if (power != spoolPower && (spoolPower < 0f || Mathf.Abs(power - spoolPower) > 0.004f || pitch >= 1f))
            {
                part.Effect(FxSpool, power);
                spoolPower = power;
            }
        }

        void OneShot(string effect)
        {
            part.Effect(effect, 1f);
            heldCues[effect] = Time.time + CrunchTuning.CueHoldSeconds;
        }

        void ReleaseCues()
        {
            if (heldCues.Count == 0) return;
            releasedCues.Clear();
            foreach (KeyValuePair<string, double> held in heldCues)
                if (Time.time >= held.Value) releasedCues.Add(held.Key);
            for (int i = 0; i < releasedCues.Count; i++)
            {
                part.Effect(releasedCues[i], 0f);
                heldCues.Remove(releasedCues[i]);
            }
        }

        void StopLoops()
        {
            if (spoolPower >= 0f) { part.Effect(FxSpool, 0f); spoolPower = -1f; }
            spoolFallStart = -1f;
            if (wasField) { part.Effect(FxField, 0f); wasField = false; }
        }

        // ---- Editor info ----

        public override string GetModuleDisplayName()
        {
            return CrunchText.ModuleTitle;
        }

        public override string GetInfo()
        {
            return CrunchText.ModuleInfo;
        }
    }
}