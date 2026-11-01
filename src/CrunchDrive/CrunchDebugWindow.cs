using UnityEngine;

namespace CrunchDrive
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchDebugWindow : MonoBehaviour
    {
        const KeyCode ToggleKey = KeyCode.Backslash;
        bool show;
        Rect rect = new Rect(50, 50, 460, 0);
        GUIStyle wrapLabel;

        void Update()
        {
            if (Input.GetKeyDown(ToggleKey)) show = !show;
        }

        void OnGUI()
        {
            if (!show) return;

            // GUI.skin is only valid inside OnGUI, so build the styles lazily here.
            if (wrapLabel == null)
            {
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
            bool canEngage = CrunchController.CanEngageFromSpool;   // A spool can be engaged once it is long enough.
            GUI.enabled = phase != CrunchPhase.Spooling || canEngage;
            if (GUILayout.Button(CrunchText.PrimaryButton(phase, CrunchTuning.Backwards, CrunchController.IsTurning, canEngage)))
                CrunchController.PrimaryAction();

            GUI.enabled = phase == CrunchPhase.Spooling || phase == CrunchPhase.Ready;
            if (GUILayout.Button(CrunchText.ButtonCancel))
                CrunchController.Cancel();

            // The craft's settings (mode, duration, automatic approach, capture assistance, SAS) are set in the VAB/SPH, on the crunch
            // drive's menu. These two are only here. They stay on screen during a crunch and are locked then, so the layout never changes.
            GUI.enabled = phase != CrunchPhase.Crunching;
            CrunchTuning.UseTurnController = GUILayout.Toggle(CrunchTuning.UseTurnController, CrunchText.ToggleTurnController);
            CrunchTuning.AllowWarp = GUILayout.Toggle(CrunchTuning.AllowWarp, CrunchText.ToggleCrunchWarp);
            GUI.enabled = true;

            double tier = CrunchController.ActiveTierC;
            GUILayout.Label(string.Format(CrunchText.LabelTier, tier), wrapLabel);

            GUI.DragWindow();
        }
    }
}