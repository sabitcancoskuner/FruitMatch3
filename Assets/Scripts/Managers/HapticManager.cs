using UnityEngine;

public class HapticManager : MonoBehaviour
{
    public static HapticManager Instance;

    public bool hapsticsEnabled = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;

        Vibration.Init();
    }

    public void VibrateLight()
    {
        if (!hapsticsEnabled) return;

        Vibration.VibratePop();
    }

    public void VibrateMedium()
    {
        if (!hapsticsEnabled) return;

        Vibration.VibratePeek();
    }

    public void VibrateHeavy()
    {
        if (!hapsticsEnabled) return;

        Vibration.Vibrate();
    }
}
