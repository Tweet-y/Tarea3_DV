using UnityEngine;

/// <summary>
/// Sonidos generados por código para lo que el proyecto no trae grabado: voces de zombie,
/// pisadas de respaldo y el motor de la lancha. Si existen clips en Resources (ver cada método),
/// se usan esos en vez de los sintetizados.
/// </summary>
public static class SintesisAudio
{
    const int Frecuencia = 22050;

    static AudioClip[] gemidos, rugidos, pasos;
    static AudioClip motor, agua;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        gemidos = rugidos = pasos = null;
        motor = agua = null;
    }

    /// <summary>Gemidos lentos de zombie. Reemplazables con clips en Resources/Sonido/Zombies/Gemidos.</summary>
    public static AudioClip[] Gemidos
    {
        get
        {
            if (gemidos != null) return gemidos;
            gemidos = Cargar("Sonido/Zombies/Gemidos");
            if (gemidos.Length > 0) return gemidos;
            var azar = new System.Random(11);
            gemidos = new AudioClip[5];
            for (int i = 0; i < gemidos.Length; i++)
            {
                float f0 = Rango(azar, 68f, 92f);
                gemidos[i] = Voz("ZombieGemido" + i, azar, Rango(azar, 1.1f, 1.8f), f0, f0 * Rango(azar, .72f, .9f), .35f, Rango(azar, 480f, 620f));
            }
            return gemidos;
        }
    }

    /// <summary>Rugidos cortos al perseguir o atacar. Reemplazables en Resources/Sonido/Zombies/Rugidos.</summary>
    public static AudioClip[] Rugidos
    {
        get
        {
            if (rugidos != null) return rugidos;
            rugidos = Cargar("Sonido/Zombies/Rugidos");
            if (rugidos.Length > 0) return rugidos;
            // Sin rugidos grabados pero con gemidos grabados (el zombie de Minecraft): se usan esos;
            // SonidoZombie los sube de tono al perseguir y atacar. Mezclar con los sintéticos sonaría a dos bichos distintos.
            var grabados = Cargar("Sonido/Zombies/Gemidos");
            if (grabados.Length > 0) return rugidos = grabados;
            var azar = new System.Random(23);
            rugidos = new AudioClip[4];
            for (int i = 0; i < rugidos.Length; i++)
            {
                float f0 = Rango(azar, 115f, 160f);
                rugidos[i] = Voz("ZombieRugido" + i, azar, Rango(azar, .45f, .75f), f0 * 1.1f, f0 * .8f, .75f, Rango(azar, 650f, 820f));
            }
            return rugidos;
        }
    }

    /// <summary>Pisadas: usa Resources/Sonido/Pasos; si no hay, un golpe sordo sintetizado.</summary>
    public static AudioClip[] Pasos
    {
        get
        {
            if (pasos != null) return pasos;
            pasos = Cargar("Sonido/Pasos");
            if (pasos.Length > 0) return pasos;
            var azar = new System.Random(5);
            pasos = new AudioClip[3];
            for (int i = 0; i < pasos.Length; i++) pasos[i] = Paso("PasoSintetico" + i, azar);
            return pasos;
        }
    }

    /// <summary>Motor fuera de borda en bucle. Reemplazable con Resources/Sonido/Lancha/Motor.</summary>
    public static AudioClip Motor
    {
        get
        {
            if (motor != null) return motor;
            motor = Resources.Load<AudioClip>("Sonido/Lancha/Motor");
            if (motor != null) return motor;
            var azar = new System.Random(3);
            motor = Bucle("LanchaMotor", 2f, (t, ruido) =>
            {
                // Explosiones del cilindro a 28 Hz (entero: el bucle cierra sin salto) y sus armónicos.
                float fase = t * 28f % 1f;
                float pulso = Mathf.Exp(-fase * 9f) - .2f;
                return pulso * .55f
                    + Mathf.Sin(2f * Mathf.PI * 56f * t) * .25f
                    + Mathf.Sin(2f * Mathf.PI * 84f * t + 1.3f) * .12f
                    + ruido * .18f;
            }, azar, 900f);
            return motor;
        }
    }

    /// <summary>Agua cortada por el casco en bucle. Reemplazable con Resources/Sonido/Lancha/Agua.</summary>
    public static AudioClip Agua
    {
        get
        {
            if (agua != null) return agua;
            agua = Resources.Load<AudioClip>("Sonido/Lancha/Agua");
            if (agua != null) return agua;
            var azar = new System.Random(8);
            agua = Bucle("LanchaAgua", 2f, (t, ruido) =>
                ruido * (.7f + .3f * Mathf.Sin(2f * Mathf.PI * .5f * t)), azar, 1400f);
            return agua;
        }
    }

    static readonly System.Collections.Generic.Dictionary<(AudioClip, float), AudioClip> amplificados =
        new System.Collections.Generic.Dictionary<(AudioClip, float), AudioClip>();

    /// <summary>
    /// Copia del clip con más ganancia (un AudioSource no pasa de volumen 1). Un limitador suave evita
    /// que distorsione. Si el clip no deja leer sus muestras (streaming), devuelve el original.
    /// </summary>
    public static AudioClip Amplificado(AudioClip clip, float ganancia)
    {
        if (clip == null || ganancia <= 1.001f) return clip;
        if (amplificados.TryGetValue((clip, ganancia), out var hecho) && hecho != null) return hecho;
        if (clip.loadType != AudioClipLoadType.DecompressOnLoad) return clip;
        var datos = new float[clip.samples * clip.channels];
        if (!clip.GetData(datos, 0)) return clip;
        for (int i = 0; i < datos.Length; i++)
        {
            float x = datos[i] * ganancia;
            // Lineal hasta ±0,7; por encima se curva hacia ±1 en vez de recortarse.
            float a = Mathf.Abs(x);
            if (a > .7f) x = Mathf.Sign(x) * (.7f + .3f * (float)System.Math.Tanh((a - .7f) / .3f));
            datos[i] = x;
        }
        var copia = AudioClip.Create(clip.name + "_x" + ganancia.ToString("0.0"), clip.samples, clip.channels, clip.frequency, false);
        copia.SetData(datos, 0);
        amplificados[(clip, ganancia)] = copia;
        return copia;
    }

    // ------------------------------------------------------------------ Generadores

    static AudioClip[] Cargar(string ruta)
    {
        var clips = Resources.LoadAll<AudioClip>(ruta);
        return clips ?? new AudioClip[0];
    }

    static float Rango(System.Random azar, float a, float b) => a + (float)azar.NextDouble() * (b - a);

    /// <summary>
    /// Voz gutural: diente de sierra con vibrato irregular, áspera por ruido modulado,
    /// pasada por dos formantes (filtros resonantes) para que suene a garganta y no a pitido.
    /// </summary>
    static AudioClip Voz(string nombre, System.Random azar, float duracion, float f0Inicio, float f0Fin, float aspereza, float formante)
    {
        int n = Mathf.CeilToInt(duracion * Frecuencia);
        var datos = new float[n];
        var f1 = new Resonador(formante, .32f);
        var f2 = new Resonador(formante * 2.15f, .4f);
        double fase = 0;
        float ruidoLento = 0f, temblor = Rango(azar, 4.5f, 7f);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Frecuencia;
            float k = t / duracion;
            float f0 = Mathf.Lerp(f0Inicio, f0Fin, k) * (1f + .07f * Mathf.Sin(2f * Mathf.PI * temblor * t) + .04f * Mathf.Sin(2f * Mathf.PI * 1.7f * t));
            fase += f0 / Frecuencia;
            float sierra = (float)(2.0 * (fase - System.Math.Floor(fase)) - 1.0);
            float blanco = (float)(azar.NextDouble() * 2.0 - 1.0);
            ruidoLento += (blanco - ruidoLento) * .08f;
            float fuente = sierra * (1f + aspereza * ruidoLento * 3f) + blanco * aspereza * .25f;
            float salida = f1.Procesar(fuente) + .6f * f2.Procesar(fuente);
            // Entra suave, se sostiene y se apaga en el último tercio.
            float envolvente = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / .12f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - k) / .35f));
            datos[i] = salida * envolvente;
        }
        Normalizar(datos, .85f);
        return Crear(nombre, datos);
    }

    static AudioClip Paso(string nombre, System.Random azar)
    {
        int n = Mathf.CeilToInt(.16f * Frecuencia);
        var datos = new float[n];
        float bajo = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Frecuencia;
            float blanco = (float)(azar.NextDouble() * 2.0 - 1.0);
            bajo += (blanco - bajo) * .06f;
            datos[i] = (bajo * 3f + Mathf.Sin(2f * Mathf.PI * 70f * t) * .6f) * Mathf.Exp(-t * 28f);
        }
        Normalizar(datos, .8f);
        return Crear(nombre, datos);
    }

    /// <summary>Bucle sin chasquido: se genera de más y la cola se funde con el comienzo.</summary>
    static AudioClip Bucle(string nombre, float duracion, System.Func<float, float, float> muestra, System.Random azar, float corteRuido)
    {
        int n = Mathf.CeilToInt(duracion * Frecuencia);
        int fundido = Frecuencia / 8;
        var largo = new float[n + fundido];
        float alfa = 1f - Mathf.Exp(-2f * Mathf.PI * corteRuido / Frecuencia);
        float ruido = 0f;
        for (int i = 0; i < largo.Length; i++)
        {
            ruido += ((float)(azar.NextDouble() * 2.0 - 1.0) - ruido) * alfa;
            largo[i] = muestra((float)i / Frecuencia, ruido * 2.5f);
        }
        var datos = new float[n];
        for (int i = 0; i < n; i++) datos[i] = largo[i];
        for (int i = 0; i < fundido; i++)
        {
            float k = (float)i / fundido;
            datos[i] = largo[i] * k + largo[n + i] * (1f - k);
        }
        Normalizar(datos, .8f);
        return Crear(nombre, datos);
    }

    static void Normalizar(float[] datos, float pico)
    {
        float maximo = 0f;
        foreach (float d in datos) maximo = Mathf.Max(maximo, Mathf.Abs(d));
        if (maximo < 1e-5f) return;
        float escala = pico / maximo;
        for (int i = 0; i < datos.Length; i++) datos[i] *= escala;
    }

    static AudioClip Crear(string nombre, float[] datos)
    {
        var clip = AudioClip.Create(nombre, datos.Length, 1, Frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }

    /// <summary>Filtro de estado variable (Chamberlin) usado como pasabanda resonante.</summary>
    struct Resonador
    {
        readonly float f, q;
        float bajo, banda;

        public Resonador(float centro, float amortiguacion)
        {
            f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(centro, Frecuencia * .2f) / Frecuencia);
            q = amortiguacion;
            bajo = banda = 0f;
        }

        public float Procesar(float entrada)
        {
            bajo += f * banda;
            float alto = entrada - bajo - q * banda;
            banda += f * alto;
            return banda;
        }
    }
}
