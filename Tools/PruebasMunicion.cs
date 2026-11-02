using System;

// Ejecutable sin Unity: prueba transacciones de municion, no depende de frames ni del editor.
public static class PruebasMunicion
{
    private static int comprobaciones;
    private static void Exigir(bool condicion, string descripcion)
    {
        if (!condicion) throw new Exception(descripcion);
        comprobaciones++;
        Console.WriteLine("OK: " + descripcion);
    }

    public static void Main()
    {
        var arma = new EstadoMunicion(30, 240, 30, 120);
        Exigir(!arma.IniciarRecarga(), "Cargador lleno: no inicia recarga.");
        for (int i = 0; i < 7; i++) Exigir(arma.Disparar(), "Cada disparo consume una bala.");
        Exigir(arma.Cargador == 23 && arma.Reserva == 120, "El disparo no consume reserva.");
        Exigir(arma.IniciarRecarga() && !arma.IniciarRecarga(), "Solo una recarga simultanea.");
        Exigir(!arma.Disparar() && arma.Cargador == 23, "No dispara durante la recarga.");
        arma.CancelarRecarga();
        Exigir(arma.Cargador == 23 && arma.Reserva == 120, "Soltar o desactivar cancela sin crear ni perder balas.");
        arma.TerminarRecarga();
        Exigir(arma.Cargador == 23 && arma.Reserva == 120, "Una recarga cancelada no puede completarse despues.");
        arma.IniciarRecarga();
        arma.TerminarRecarga();
        Exigir(arma.Cargador == 30 && arma.Reserva == 113, "Recarga parcial transfiere exactamente siete balas.");
        arma.TerminarRecarga();
        Exigir(arma.Reserva == 113, "Completar dos veces no duplica municion.");
        for (int i = 0; i < 30; i++) arma.Disparar();
        Exigir(!arma.Disparar() && arma.Cargador == 0, "Cargador vacio: no dispara ni queda negativo.");
        var escasa = new EstadoMunicion(30, 240, 2, 3);
        escasa.IniciarRecarga();
        escasa.TerminarRecarga();
        Exigir(escasa.Cargador == 5 && escasa.Reserva == 0, "Reserva insuficiente: recarga solo lo disponible.");
        Exigir(!escasa.IniciarRecarga(), "Sin reserva: no inicia recarga.");
        Exigir(escasa.AgregarReserva(300) == 240 && escasa.Reserva == 240, "La recogida respeta el limite de reserva.");
        Exigir(escasa.AgregarReserva(60) == 0 && escasa.AgregarReserva(-10) == 0, "Reserva llena o cantidad negativa no altera las balas.");
        var corrupta = new EstadoMunicion(0, -1, 999, -4);
        Exigir(corrupta.Capacidad == 1 && corrupta.Cargador == 1 && corrupta.Reserva == 0, "Configuraciones invalidas se acotan.");
        Console.WriteLine("PASS: " + comprobaciones + " comprobaciones.");
    }
}
