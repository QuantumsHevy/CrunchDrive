using UnityEngine;

namespace CrunchDrive
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchDebugWindow : MonoBehaviour
    {
        const KeyCode ToggleKey = KeyCode.Backslash;
        bool show;
        Rect rect = new Rect(50, 50, 460, 0);
        GUIStyle wrapToggle;
        GUIStyle wrapLabel;
        bool showDuration;   // Fixed-time controls visible. Only changes during the layout pass (see Draw).
        bool showTwoLegHint;

        void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
            {
                show = !show;
                Debug.Log("[CrunchDrive] Debug window toggled, show=" + show);
            }
        }

        void OnGUI()
        {
            if (!show) return;

            // GUI.skin is only valid inside OnGUI, so build the styles lazily here.
            if (wrapToggle == null)
            {
                wrapToggle = new GUIStyle(GUI.skin.toggle);
                wrapToggle.wordWrap = true;
                wrapLabel = new GUIStyle(GUI.skin.label);
                wrapLabel.wordWrap = true;
            }

            rect.height = 0f;   // IMGUI windows only ever grow; reset the height so it re-fits its contents every frame.
            rect = GUILayout.Window(GetInstanceID(), rect, Draw, CrunchText.WindowTitle);
        }

        void Draw(int id)
        {
            GUILayout.Label(CrunchController.Status, wrapLabel);
            if (!string.IsNullOrEmpty(CrunchController.TargetLine))   // Empty while the status readout already shows the target.
                GUILayout.Label(CrunchController.TargetLine, wrapLabel);

            CrunchPhase phase = CrunchController.CurrentPhase;

            // Keep controls visible across phases; disable them rather than changing the layout.
            GUI.enabled = phase != CrunchPhase.Spooling;
            if (GUILayout.Button(CrunchText.PrimaryButton(phase, CrunchTuning.Backwards, CrunchController.IsTurning)))
                CrunchController.PrimaryAction();

            GUI.enabled = phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready;
            if (GUILayout.Button(CrunchText.ButtonCancel))
                CrunchController.Cancel();

            // IMGUI lays controls out in one pass and draws them in another, so a control may only appear or
            // disappear between frames. Latch what is visible during the layout pass, then use it for the whole frame.
            if (Event.current.type == EventType.Layout)
            {
                showDuration = CrunchTuning.Mode == TravelMode.FixedDuration;
                showTwoLegHint = CrunchTuning.TwoLegManeuver;
            }

            GUI.enabled = phase != CrunchPhase.Crunching;
            CrunchTuning.Backwards = GUILayout.Toggle(CrunchTuning.Backwards, CrunchText.ToggleBackwards, wrapToggle);
            CrunchTuning.CaptureAssist = GUILayout.Toggle(CrunchTuning.CaptureAssist, CrunchText.ToggleCaptureAssist, wrapToggle);
            CrunchTuning.SasRetrograde = GUILayout.Toggle(CrunchTuning.SasRetrograde, CrunchText.ToggleSasRetrograde, wrapToggle);
            CrunchTuning.TwoLegManeuver = GUILayout.Toggle(CrunchTuning.TwoLegManeuver, CrunchText.ToggleTwoLeg, wrapToggle);
            CrunchTuning.UseTurnController = GUILayout.Toggle(CrunchTuning.UseTurnController, CrunchText.ToggleTurnController, wrapToggle);
            CrunchTuning.AllowInterstellarWarp = GUILayout.Toggle(CrunchTuning.AllowInterstellarWarp, CrunchText.ToggleInterstellarWarp, wrapToggle);

            if (showTwoLegHint) GUILayout.Label(CrunchText.TwoLegHint, wrapLabel);
            CrunchTuning.Mode = (TravelMode)GUILayout.SelectionGrid((int)CrunchTuning.Mode, CrunchText.ModeNames, 3);

            double tier = CrunchController.PreviewTierC;
            string info;
            if (showDuration)
            {
                CrunchTuning.CrunchSeconds = Linear(CrunchText.SliderDuration, CrunchTuning.CrunchSeconds, 1, 120);
                info = CrunchText.DistanceInfo(CrunchTuning.CrunchSeconds, tier);
            }
            else
            {
                info = CrunchText.ModeHint(CrunchTuning.Mode);
            }
            GUILayout.Label(info, wrapLabel);
            GUI.enabled = true;

            GUILayout.Label(string.Format(CrunchText.LabelTier, tier), wrapLabel);

            GUI.DragWindow();
        }

        static double Linear(string label, double v, double min, double max)
        {
            GUILayout.Label(label + ": " + v.ToString("G4"));
            return GUILayout.HorizontalSlider((float)v, (float)min, (float)max);
        }
    }
}