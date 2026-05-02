using UnityEngine;
using TMPro;

public class FPSCounter : MonoBehaviour
{
    public TextMeshProUGUI fpsText; // Assign your UI text here
    public float updateInterval = 0.5f;
    
    private float accum = 0;
    private int frames = 0;
    private float timeLeft;

    void Start()
    {
        timeLeft = updateInterval;
        Application.targetFrameRate = 120;
    }

    void Update()
    {
        timeLeft -= Time.unscaledDeltaTime;
        accum += Time.timeScale / Time.unscaledDeltaTime;
        frames++;

        if (timeLeft <= 0.0)
        {
            float fps = accum / frames;
            fpsText.text = string.Format("{0:F0} FPS", fps);
            
            timeLeft = updateInterval;
            accum = 0.0f;
            frames = 0;
        }
    }
}
