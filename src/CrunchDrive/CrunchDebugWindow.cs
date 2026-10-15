using System;
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

            rect = GUILayout.Window(GetInstanceID(), rect, Draw, CrunchText.WindowTitle);
        }

        void Draw(int id)
        {
            GUILayout.Label(CrunchController.Status, wrapLabel);

            CrunchPhase phase = CrunchController.CurrentPhase;

            // Keep controls visible across phases; disable them rather than changing the layout.
            GUI.enabled = phase != CrunchPhase.Spooling && phase != CrunchPhase.Engaging;
            if (GUILayout.Button(CrunchText.PrimaryButton(phase, CrunchTuning.Backwards)))
                CrunchController.PrimaryAction();

            GUI.enabled = phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready || phase == CrunchPhase.Engaging;
            if (GUILayout.Button(CrunchText.ButtonCancel))
                CrunchController.Cancel();

            GUI.enabled = phase != CrunchPhase.Crunching;
            CrunchTuning.Backwards = GUILayout.Toggle(CrunchTuning.Backwards, CrunchText.ToggleBackwards, wrapToggle);
            CrunchTuning.SpoolSeconds = Linear(CrunchText.SliderSpool, CrunchTuning.SpoolSeconds, 0, 30);
            CrunchTuning.EngageDelaySeconds = Linear(CrunchText.SliderEngageDelay, CrunchTuning.EngageDelaySeconds, 0, 5);
            CrunchTuning.CrunchSeconds = Linear(CrunchText.SliderDuration, CrunchTuning.CrunchSeconds, 1, 120);
            CrunchTuning.SpringStiffness = Linear(CrunchText.SliderSpring, CrunchTuning.SpringStiffness, 2, 20);
            CrunchTuning.SpringLaunch = Linear(CrunchText.SliderLaunch, CrunchTuning.SpringLaunch, 0, 1);
            CrunchTuning.EndSpeedFraction = Linear(CrunchText.SliderEndSpeed, CrunchTuning.EndSpeedFraction, 0, 0.5);
            CrunchTuning.AutoEnd = GUILayout.Toggle(CrunchTuning.AutoEnd, CrunchText.ToggleAutoEnd, wrapToggle);
            CrunchTuning.AutoEngage = GUILayout.Toggle(CrunchTuning.AutoEngage, CrunchText.ToggleAutoEngage, wrapToggle);
            CrunchTuning.UseTargetLock = GUILayout.Toggle(CrunchTuning.UseTargetLock, CrunchText.ToggleTargetLock, wrapToggle);
            CrunchTuning.TargetAlignToleranceDeg = Linear(CrunchText.SliderAlign, CrunchTuning.TargetAlignToleranceDeg, 1, 90);
            GUI.enabled = true;

            double tier = CrunchTuning.UseInterstellarTier ? CrunchTuning.InterstellarTierC : CrunchTuning.InterplanetaryTierC;
            GUILayout.Label(CrunchText.DistanceInfo(CrunchTuning.CrunchSeconds, tier), wrapLabel);

            GUILayout.Label(string.Format(CrunchText.LabelInterplanetaryFixed, CrunchTuning.InterplanetaryTierC), wrapLabel);
            CrunchTuning.InterstellarTierC = Log(CrunchText.SliderInterstellar, CrunchTuning.InterstellarTierC, 1e3, 1e7);
            CrunchTuning.DropoutFraction = Linear(CrunchText.SliderDropout, CrunchTuning.DropoutFraction, 0.02, 0.5);

            CrunchTuning.UseInterstellarTier = GUILayout.Toggle(CrunchTuning.UseInterstellarTier, CrunchText.ToggleInterstellar, wrapToggle);

            if (GUILayout.Button(CrunchText.ButtonExit)) show = false;
            GUI.DragWindow();
        }

        static double Linear(string label, double v, double min, double max)
        {
            GUILayout.Label(label + ": " + v.ToString("G4"));
            return GUILayout.HorizontalSlider((float)v, (float)min, (float)max);
        }

        static double Log(string label, double v, double min, double max)
        {
            GUILayout.Label(label + ": " + v.ToString("G4"));
            float s = GUILayout.HorizontalSlider((float)Math.Log10(v), (float)Math.Log10(min), (float)Math.Log10(max));
            return Math.Pow(10, s);
        }
    }
}