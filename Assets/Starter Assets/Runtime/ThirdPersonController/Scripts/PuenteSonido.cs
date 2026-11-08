using UnityEngine;

namespace StarterAssets
{
    /// <summary>El controlador de Starter Assets no puede llamar a Assembly-CSharp. El juego engancha esto.</summary>
    public static class PuenteSonido
    {
        public static System.Action<AudioClip, Vector3, float> Reproducir;

        public static void Emitir(AudioClip clip, Vector3 posicion, float volumen)
        {
            if (clip == null) return;
            if (Reproducir != null) Reproducir(clip, posicion, volumen);
            else AudioSource.PlayClipAtPoint(clip, posicion, volumen);
        }
    }
}
