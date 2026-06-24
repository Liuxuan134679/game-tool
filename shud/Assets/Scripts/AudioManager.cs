using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clickClip;

    public void PlayClickSound()
    {
        audioSource.PlayOneShot(clickClip);
    }
}