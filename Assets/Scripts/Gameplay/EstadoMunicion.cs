using System;

/// <summary>La recarga transfiere balas solo al terminar; cancelarla no crea ni pierde municion.</summary>
public sealed class EstadoMunicion
{
    public int Capacidad { get; }
    public int ReservaMaxima { get; }
    public int Cargador { get; private set; }
    public int Reserva { get; private set; }
    public bool Recargando { get; private set; }

    public EstadoMunicion(int capacidad, int reservaMaxima, int cargador, int reserva)
    {
        Capacidad = Math.Max(1, capacidad);
        ReservaMaxima = Math.Max(0, reservaMaxima);
        Cargador = Math.Clamp(cargador, 0, Capacidad);
        Reserva = Math.Clamp(reserva, 0, ReservaMaxima);
    }

    public bool Disparar()
    {
        if (Recargando || Cargador == 0) return false;
        Cargador--;
        return true;
    }

    public bool IniciarRecarga()
    {
        if (Recargando || Cargador == Capacidad || Reserva == 0) return false;
        Recargando = true;
        return true;
    }

    public void TerminarRecarga()
    {
        if (!Recargando) return;
        int cantidad = Math.Min(Capacidad - Cargador, Reserva);
        Cargador += cantidad;
        Reserva -= cantidad;
        Recargando = false;
    }

    /// <summary>Recarga de a un cartucho: mete uno y sigue recargando mientras quepa y quede reserva.</summary>
    public bool CargarUno()
    {
        if (!Recargando) return false;
        if (Cargador < Capacidad && Reserva > 0)
        {
            Cargador++;
            Reserva--;
        }
        if (Cargador >= Capacidad || Reserva == 0) Recargando = false;
        return true;
    }

    public void CancelarRecarga() => Recargando = false;

    public int AgregarReserva(int cantidad)
    {
        int recibida = Math.Min(Math.Max(0, cantidad), ReservaMaxima - Reserva);
        Reserva += recibida;
        return recibida;
    }
}
