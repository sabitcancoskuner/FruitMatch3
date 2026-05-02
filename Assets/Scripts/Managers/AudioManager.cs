using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;
    
    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private int sfxPoolSize = 10;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip backgroundMusic;
    public AudioClip matchPopSound;
    public AudioClip createPowerupSound;
    public AudioClip rocketPowerupSound;
    public AudioClip bombPowerupSound;
    public AudioClip propellerPowerupSound;
    public AudioClip discoballPowerupLaserSound;

    private List<AudioSource> sfxPool;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        InitializeSfxPool();
    }

    private void InitializeSfxPool()
    {
        sfxPool = new List<AudioSource>();

        GameObject poolContainer = new GameObject("SFX Pool");
        poolContainer.transform.SetParent(this.transform);

        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource source = poolContainer.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Add(source);
        }
    }
    
    public AudioSource PlaySFX(AudioClip clip, bool randomizePitch = true)
    {
        if (clip == null) return null;

        AudioSource availableSource = null;

        foreach (AudioSource source in sfxPool)
        {
            if (!source.isPlaying)
            {
                availableSource = source;
                break;
            }
        }

        if (availableSource == null)
            availableSource = sfxPool[0];

        if (randomizePitch)
            availableSource.pitch = Random.Range(.95f, 1.05f);
        else
            availableSource.pitch = 1f;

        availableSource.clip = clip;
        availableSource.Play();

        return availableSource;
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null) return;

        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.Play();
    }
}
