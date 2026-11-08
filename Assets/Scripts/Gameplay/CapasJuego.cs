using UnityEngine;

/// <summary>La matriz del proyecto ya limita las armas; esto la vuelve a aplicar al entrar en Play.</summary>
public static class CapasJuego
{
    public const string Jugador = "Jugador";
    public const string Aliados = "Aliados";
    public const string Enemigos = "Enemigos";
    public const string ArmaJugador = "ArmaJugador";
    public const string ArmaEnemigo = "ArmaEnemigo";
    public const string Objetos = "Objetos";
    public const string Recogibles = "Recogibles";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Aplicar()
    {
        int jugador = LayerMask.NameToLayer(Jugador);
        int enemigos = LayerMask.NameToLayer(Enemigos);
        int armaJugador = LayerMask.NameToLayer(ArmaJugador);
        int armaEnemigo = LayerMask.NameToLayer(ArmaEnemigo);
        int objetos = LayerMask.NameToLayer(Objetos);
        int recogibles = LayerMask.NameToLayer(Recogibles);
        if (jugador < 0 || armaEnemigo < 0) return;
        Solo(armaEnemigo, jugador);
        Solo(armaJugador, enemigos, objetos);
        Solo(recogibles, jugador);
    }

    static void Solo(int capa, params int[] otras)
    {
        for (int i = 0; i < 32; i++)
        {
            bool choca = System.Array.IndexOf(otras, i) >= 0;
            Physics.IgnoreLayerCollision(capa, i, !choca);
        }
    }
}
