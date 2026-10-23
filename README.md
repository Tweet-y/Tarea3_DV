# Plataformas en tercera persona - Evaluación 2 (UCSC 2026-2)

Proyecto de videojuego desarrollado en **Unity**, correspondiente a la segunda evaluación de la asignatura de Desarrollo de Videojuegos. Es un juego de **plataformas en tercera persona**, de temática libre: menú inicial, un nivel jugable y una pantalla de resumen al terminar.

Al cerrar el resumen, el juego vuelve al menú con carga asíncrona. El nivel incluye obstáculos que reducen la salud. No se requieren enemigos.

---

## Integración de requerimientos

### Scripting: manejo de corrutinas
- La carga de escenas corre en una corrutina (`ControladorMenus.CargarEscena`): inicia `LoadSceneAsync`, actualiza la barra y el porcentaje frame a frame y activa la escena cuando la carga termina.

### UI: HUD y botón de inicio
- **Menú (`MenuInicio`):** botón de inicio que carga el nivel.
- **HUD:** salud, botellas objetivo y puntos actualizados por `ControladorGameplayHUD` desde `ControladorPartidaBotellas`.
- **Cierre:** pantalla de resumen con el resultado de la partida, y desde ahí el regreso al menú.

### Sonido: efectos y música de fondo
- **Música de fondo** en loop durante el nivel.
- **Efectos del juego:** recolección, daño por obstáculos, victoria y derrota.
- **Efectos de interfaz:** el mismo efecto de moneda en los botones de inicio, pausa y resumen. Se escucha con la partida en pausa y no se corta al cambiar de escena.

### Input: teclado, ratón y gamepad
- El jugador se controla con el **Input System** y la base de `ThirdPersonController` (Starter Assets): movimiento, cámara y salto con teclado, ratón y gamepad.

### Personaje animado
- Personaje en tercera persona a partir de `ThirdPersonController` y los prefabs en `Assets/Prefabs/Personaje`.
- Animaciones de locomoción e idle en `Assets/Animaciones/Personajes` (`CJLocomotionAnimation`).

### Gestión de escenas
- Una escena de menú con botones que cargan el nivel de forma **asíncrona**, con pantalla de progreso.
- Al terminar el nivel y la pantalla de resumen, la misma carga asíncrona devuelve al menú inicial.

### Mecánicas
- **Mapa de la ciudad:** radar circular inspirado en San Andreas, con borde negro grueso, edificios claros, calles negras y terrenos verdes. Tiene zoom de 3×, está centrado en el jugador y se desplaza con él. La ciudad se revela al explorar un radio de 22 metros y conserva las zonas descubiertas durante la partida. Los círculos amarillos de posibles botellas aparecen sólo en sectores descubiertos y desaparecen al recoger sus botellas. **M** amplía el panel con zoom de 1,5×, manteniendo la exploración y sin pausar la partida.
- **Botellas azules:** cinco curativas distribuidas junto a ubicaciones accesibles de las botellas del nivel; recuperan un **8 %** de vida, hasta el máximo, sin sumar al objetivo ni a los puntos. La curación reduce la intoxicación según el sistema de vida existente.
- **Caídas:** hasta **4 metros** sin daño; por encima se pierde **4,5 % de vida por metro adicional** al aterrizar. Los valores son configurables en `DanioCaida`.
- **Victoria:** al completar las botellas objetivo, aparece `mission passed!` en dorado y `RESPECT + 99` en blanco, con contorno negro y opciones de volver a jugar o regresar al menú.
- **Verificación integrada:** `Tools > Gameplay > Verificar mapa curacion caida y victoria` ejecuta una partida de prueba y escribe el resultado en `Logs/ciudad-final-validada.txt`.
- **Coleccionables:** `ObjetoEspecialColeccionable` recoge botellas una sola vez. El trigger rectangular mide el tamaño original del mesh × **1,30** en cada eje, también para curativas; el modelo conserva su tamaño.
- **Salud y ebriedad:** recoger botellas objetivo aumenta la ebriedad y reduce la salud; las azules recuperan salud. La ebriedad también aumenta con el tiempo a **0,001 / 1,20 = 0,00083333 por segundo**, dando un 20 % más de margen temporal con los mismos daños y curaciones. Los obstáculos aplican daño mediante `TrampaDanio`.
- **Victoria y derrota:** el nivel detecta ambas condiciones y abre la pantalla de resumen antes de volver al menú.

---

## Controles

- **WASD:** mover; **ratón:** cámara; **Espacio:** saltar; **Shift:** correr.
- **Esc:** pausar y continuar; **M:** ampliar o reducir mapa sin pausar.
- **Gamepad:** sticks para movimiento/cámara, botón sur para saltar y gatillo izquierdo para correr.
- Objetivo: reunir todas las botellas objetivo antes de quedarse sin salud. Aprovechar las cinco azules para curarse.

---

## Estructura del proyecto

```
Assets/
├── Animaciones/           # Controladores y clips del personaje
├── Inputs/                # Acciones de input
├── Materiales/            # Materiales del nivel y del personaje
├── Prefabs/               # Personaje, coleccionables y obstáculos
├── Scenes/                # MenuInicio y escenas de nivel
├── Scripts/               # Lógica de juego en C#
│   ├── ObjetoEspecialColeccionable.cs
│   ├── CJLocomotionAnimation.cs
│   ├── ControladorMusica.cs
│   ├── Gameplay/           # Partida, caída, trampas y botellas curativas
│   └── UI/                 # Menús, HUD y mapa
├── Starter Assets/        # ThirdPersonController e input de ejemplo
├── Synty/                 # Modelos y animaciones de apoyo
└── TextMesh Pro/          # Fuentes y recursos de UI
```

---

## Autores

| Nombre | GitHub |
| :--- | :--- |
| Vicente Alarcón | [@vicente-ai](https://github.com/vicente-ai) |
| Benjamín Bizama | [@Tweet-y](https://github.com/Tweet-y) |
| Nicolás Valdebenito | [@NicoValdebenito](https://github.com/NicoValdebenito) |
