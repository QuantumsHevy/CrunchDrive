using UnityEngine;

namespace CrunchDrive
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class CrunchBootstrap : MonoBehaviour
    {
        void Start() { Debug.Log(CrunchText.LogBootstrapPassed); }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class CrunchFlightProbe : MonoBehaviour
    {
        void Start()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            Debug.Log(CrunchText.LogFlightSceneEntered(v != null ? v.vesselName : "Unknown Vessel"));
        }
    }
}