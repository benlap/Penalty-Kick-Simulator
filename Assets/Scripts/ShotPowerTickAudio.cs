
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class ShotPowerTickAudio : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip tickingClip;

    [Header("Pitch")]
    [SerializeField, Min(0.01f)] private float minimumPitch = 0.7f;
    [SerializeField, Min(0.01f)] private float maximumPitch = 2.0f;
    [SerializeField, Min(0f)] private float pitchSmoothSpeed = 8f;

    [Header("Volume")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private float targetPitch;
    private bool isTicking;

    private void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        audioSource.clip = tickingClip;
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.pitch = minimumPitch;
        audioSource.volume = volume;
        audioSource.spatialBlend = 0f;

        targetPitch = minimumPitch;
    }

    private void Update()
    {
        if (audioSource == null)
            return;

        audioSource.pitch = Mathf.MoveTowards(
            audioSource.pitch,
            targetPitch,
            pitchSmoothSpeed * Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Start or update the ticking sound using normalized power (0 to 1).
    /// </summary>
    public void UpdatePower(float power01)
    {
        power01 = Mathf.Clamp01(power01);

        targetPitch = Mathf.Lerp(
            minimumPitch,
            maximumPitch,
            power01);

        if (!isTicking && tickingClip != null)
        {
            audioSource.volume = volume;
            audioSource.Play();
            isTicking = true;
        }
    }

    /// <summary>
    /// Stop ticking when charging ends.
    /// </summary>
    public void StopTicking()
    {
        if (audioSource == null)
            return;

        audioSource.Stop();
        audioSource.pitch = minimumPitch;
        targetPitch = minimumPitch;
        isTicking = false;
    }
}
