using UnityEngine;

// Esto asegura que Unity añada un AudioSource automáticamente al usar este script
[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    [Tooltip("Arrastra aquí tus 3 pistas de audio desde el Inspector")]
    public AudioClip[] musicTracks;

    private AudioSource audioSource;
    private int lastPlayedIndex = -1;

    void Start()
    {
        // Obtenemos la referencia al AudioSource
        audioSource = GetComponent<AudioSource>();

        // Verificamos que se hayan asignado pistas antes de intentar reproducir
        if (musicTracks.Length > 0)
        {
            PlayRandomTrack();
        }
        else
        {
            Debug.LogWarning("No hay pistas de música asignadas en el MusicManager.");
        }
    }

    void Update()
    {
        // Si el AudioSource terminó de sonar, cargamos la siguiente pista
        if (!audioSource.isPlaying && musicTracks.Length > 0)
        {
            PlayRandomTrack();
        }
    }

    private void PlayRandomTrack()
    {
        int randomIndex = Random.Range(0, musicTracks.Length);

        // Evitamos repetir la pista que acaba de sonar
        if (musicTracks.Length > 1)
        {
            while (randomIndex == lastPlayedIndex)
            {
                randomIndex = Random.Range(0, musicTracks.Length);
            }
        }

        // Asignamos la pista elegida y la reproducimos
        audioSource.clip = musicTracks[randomIndex];
        audioSource.Play();

        // Guardamos el índice actual para la próxima iteración
        lastPlayedIndex = randomIndex;
    }
}