using UnityEngine;
using System.IO;

public class AudioManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clickClip;
    private AudioClip runtimeClip;

    private void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null) { audioSource.playOnAwake = false; audioSource.spatialBlend = 0; }
#if UNITY_WEBGL && !UNITY_EDITOR
        var pcm = Resources.Load<TextAsset>("AreaGridClickPcm");
        if (pcm != null)
        {
            using (var reader = new BinaryReader(new MemoryStream(pcm.bytes)))
            {
                int frequency = reader.ReadInt32(), channels = reader.ReadInt32(), frames = reader.ReadInt32();
                var samples = new float[channels * frames];
                for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32767f;
                runtimeClip = AudioClip.Create("AreaGridClickPCM", frames, channels, frequency, false);
                if (runtimeClip.SetData(samples, 0)) clickClip = runtimeClip;
                else Debug.LogError("Unable to initialize click sound samples.");
            }
        }
        else Debug.LogError("Missing AreaGridClickPcm resource. Rebuild using Area Grid > Build WebGL release.");
#endif
        if (clickClip != null) clickClip.LoadAudioData();
    }

    public void PlayClickSound()
    {
        if (AudioListener.volume > 0 && audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);
    }

    private void OnDestroy() { if (runtimeClip != null) Destroy(runtimeClip); }
}
