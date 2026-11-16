# Plataformas en tercera persona - Evaluación 2 (UCSC 2026-2)

Proyecto de videojuego desarrollado en **Unity**, correspondiente a la segunda evaluación de la asignatura de Desarrollo de Videojuegos. Es un juego de **plataformas en tercera persona**, de temática libre: menú inicial, un nivel jugable y una pantalla de resumen al terminar.

Al cerrar el resumen, el juego vuelve al menú con carga asíncrona. El nivel incluye obstáculos que reducen la salud y dos enemigos que persiguen al jugador.

---

## Integración de requerimientos

### Scripting: manejo de corrutinas
- La carga de escenas corre en una corrutina (`ControladorMenus.CargarEscena`): inicia `LoadSceneAsync`, actualiza la barra y el porcentaje frame a frame y activa la escena cuando la carga termina.

### UI: HUD y botón de inicio
- **Menú (`MenuInicio`):** botón de inicio que carga el nivel.
- **HUD:** salud, botellas objetivo y puntos actualizados por `ControladorGameplayHUD` desde `ControladorPartidaBotellas`.
- **Cierre:** pantalla de resumen con el resultado de la partida, y desde ahí el regreso al menú.
- **Estilo común:** `EstiloUI` define la paleta de atardecer (oro, rosa, magenta, noche) y las fuentes de toda la interfaz, con contorno negro grueso y sombra dura. Lo usan el HUD, los menús, la pantalla de carga, los diálogos (franjas de cine) y el HUD de la pelea del jefe. Las opciones de menú son texto con una franja inclinada (`FormaUIGraphic`, `RealceBotonMenu`).
- **Fuentes:** **Anton** (HUD y menús) y **Bangers** (títulos), ambas con licencia **SIL Open Font License 1.1**; los archivos y sus licencias están en `Assets/Resources/Fuentes/`. El texto largo usa LiberationSans, la fuente por defecto de TextMesh Pro.

### Sonido: efectos y música de fondo
- **Música de fondo** en loop durante el nivel.
- **Efectos del juego:** recolección, daño por obstáculos, victoria y derrota.
- **Efectos de interfaz:** el mismo efecto de moneda en los botones de inicio, pausa y resumen. Se escucha con la partida en pausa y no se corta al cambiar de escena.

### Input: teclado, ratón y gamepad
- El jugador se controla con el **Input System** y la base de `ThirdPersonController` (Starter Assets): movimiento, cámara y salto con teclado, ratón y gamepad.

### Personaje animado
- Jugador **curao_principal**, basado en el personaje Synty `Character_ToplessMale_01`, con `ThirdPersonController`, Input System y cápsula de movimiento. Prefab: `Assets/Prefabs/curao_principal.prefab`.
- Controlador `Assets/Animaciones/Personajes/CuraoPrincipalLocomotion.controller`: reposo, caminar, correr, sprint, salto, caída y aterrizaje de **Kevin Iglesias / Basic Motions**, con retargeting Humanoid y root motion desactivado.
- La cámara Cinemachine sigue `PlayerCameraRoot` del jugador activo. Se conservan el HUD, el mapa, las botellas, la pausa, el daño por caídas, los objetivos de enemigos y la pose procedural del rifle. `AnimacionCuraoPrincipal` conserva los sonidos de pasos y aterrizaje.

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
- **Victoria y derrota:** el nivel detecta ambas condiciones y abre la pantalla de resumen antes de volver al menú. Morir por los golpes de un enemigo muestra «Te atraparon».

### Armas de fuego

- Rifle **Synty Battle Royale `SM_Wep_Rifle_Assault_03`**, recogible junto al punto de inicio. Los cargadores de munición aparecen junto al inicio y en varios sectores del recorrido.
- **E:** recoger rifle o munición; **clic izquierdo mantenido:** disparo automático; **clic derecho:** apuntar; **R:** recargar; **G:** soltar el rifle.
- **Mando:** X para recoger, RT para disparar, LB para apuntar, Y para recargar y cruceta abajo para soltar. LB evita interferir con el sprint del gatillo izquierdo.
- **Movimiento al apuntar:** WASD o stick permiten avanzar, retroceder y desplazarse lateralmente manteniendo el torso hacia la mira. Paso de 1,45 m/s, sin sprint, con animaciones de caminar en ocho direcciones y sensibilidad de cámara reducida.
- **Cámara al hombro:** al mantener clic derecho o LB, Cinemachine acerca la cámara a 1,8 m, desplaza el encuadre al hombro derecho y reduce el campo de visión a 32°. La transición dura 0,22 s; al soltar, recargar o dejar el arma recupera el encuadre normal. Se mantiene la detección de obstáculos de la cámara.
- Cargador de **30**, reserva inicial de **120** y maxima de **240**; cajas de **60** balas. Cadencia de **8 disparos/s**, recarga de **2,2 s** y alcance de **150 m**. La municion se conserva al soltar y volver a recoger; una recarga interrumpida no transfiere balas. Con el cargador vacio, disparar inicia la recarga si queda reserva.
- Mira, confirmacion de impacto, contador de municion, progreso de recarga, retroceso visual, fogonazo y sonido. Pose de brazos y extraccion del cargador mediante animacion procedural sobre el rig Humanoid del jugador.
- Disparos hitscan: comprueban tanto la vista de la cámara como la trayectoria desde el cuerpo y el cañón, respetando paredes y cobertura. Daño de **25**, doble en la cabeza. Nemesis tiene **175** de vida y el Titán **1200**; al morir dejan de perseguir y hacer daño. Las botellas siguen siendo el objetivo de victoria.
- Pausar congela la recarga y bloquea disparos y recogidas. Al terminar la partida se oculta el HUD de armas. El sistema se reinstala al cargar una partida nueva.
- Ajustes y referencia al prefab original: `Assets/Resources/Armas/RifleAsalto03.asset`. La instalación se hace al cargar la escena de juego (`ControladorMenus.EscenaJuego`).
- **Prueba integrada:** `Tools > Armas > Verificar sistema completo`, con las escenas guardadas y Play detenido. Comprueba recogida, impacto, cadencia, paredes, pausa, recarga, municion, soltar y recoger, y dano letal; genera `Logs/armas-fuego-verificadas.txt`. `Tools/PruebasMunicion.cs` prueba la logica de municion sin Unity.

### Introducción de la isla

- Al cargar `MapaV2` desde el menú aparece CJ en la lancha de la orilla y dice: «Hoy nos tenemos que curar, ve y consigue todo el alcohol que haya en esta isla, te espero al otro lado».
- El texto aparece progresivamente. **Espacio / A** permite mostrarlo completo y continuar; también avanza automáticamente. CJ permanece sobre la lancha mientras esta acelera y se aleja al mar.
- La cámara regresa a `curao_principal` y devuelve los controles al terminar el plano de salida. Durante la presentación se oculta el HUD y se bloquean disparos, recogidas, daño y ebriedad temporal. **Esc** conserva la pausa, incluyendo el diálogo y el movimiento de la lancha.
- El objeto `IntroduccionIsla` de `MapaV2` permite ajustar las referencias de CJ y la lancha, el tiempo de lectura, la duración del plano de salida y la velocidad. CJ se configura como NPC, sin tag Player ni controles del jugador.

### Enemigos
- **Nemesis** aparece a unos 40 m del jugador, espera 5 s y lo persigue por el NavMesh. Camina a 2,2 m/s y corre a 4,6 m/s si está a más de 8 m (el sprint del jugador es 5,335, así que se le puede escapar). El golpe por contacto quita un 20 % de vida cada 1,5 s y empuja con fuerza 12.
- **Titán** es el personaje Synty `Character_Biker_01` a escala 20 (unos 36 m). Usa NavMesh con su propio tipo de agente `Titan` (radio 3 m, altura 8 m) y su propia `NavMeshSurface` (`NavMesh-Titan.asset`), igual que el jefe grande de la demo del curso: recorre las avenidas anchas y, si el jugador se mete en una calle angosta, lo espera en el punto de avenida más cercano. Camina a 1,5 m/s y corre a 3,2 m/s si está a más de 40 m: a paso normal (2,0) el jugador apenas se le escapa, y con sprint sí. Por debajo del clip de caminar la animación reproduce Walk más lento, para que los pies no patinen ni quede en idle. El golpe quita un 35 % cada 2 s y empuja con fuerza 25.
- Ambos persiguen con `NavMeshAgent.SetDestination` (como `MovimientoSimpleNPC`). El campo `objetivo` de `EnemigoAcechador` se asigna en la escena; si queda vacío, se busca el objeto con tag `Player`.
- Ambos reutilizan `StarterAssetsThirdPerson` (caminar, correr, idle). El daño es `TrampaDanio` con `repetirMientrasContacto`. Pausa y fin de partida (`Time.timeScale = 0`) los congelan. El jugador puede defenderse con el rifle recogible de Battle Royale.
- **Configurar:** `Tools > Enemigos > Configurar` (o `Unity -batchmode -projectPath . -executeMethod ConfigurarEnemigos.Ejecutar -quit` con el editor cerrado). Antes, si hace falta regenerar el FBX: `blender --background --python Tools/convertir_nemesis.py`.
- **Verificar:** `Tools > Enemigos > Verificar` escribe `Logs/enemigos-validados.txt`.
- **Créditos:** modelo de Nemesis por **toheed_zahid** en Sketchfab, licencia **CC-BY-4.0**. Hay que mantener esta atribución. El titán sale del pack Synty Polygon City Characters del material del curso. Los clips de combate, cuando están en `Assets/Animaciones/Combate/`, son de **Mixamo** (Adobe). Los efectos de partículas salen de Polygon Particle FX y los sonidos de ataque, impacto, buff y explosión del Ultimate Sound FX Bundle del material de clase.

### Isla (evaluación 3)

El nivel que se juega desde el menú es **MapaV2**. Hay **30 botellas**: 29 en la isla y la última la lleva Nemesis. El medidor de ebriedad pasa a ser el conteo **n/30**; la vida baja solo con el daño. Al juntar las 30 aparecen fuegos artificiales y la lancha de CJ en el extremo opuesto. Subir a la lancha cierra con diálogo y victoria.

- **Bate** en la mano (tecla **1**). **2** cambia al rifle si ya se recogió. Clic izquierdo o RT golpea o dispara según el arma activa.
- **Q** usa un botiquín (+30 % de vida). **F** toma la bebida energética (+50 % de daño y de velocidad durante 15 s). La munición sigue en el rifle.
- **Zombies** de personajes Synty persiguen por el NavMesh y pegan con una hitbox. No se dañan entre sí. Nemesis es el jefe (variante `NemesisJefe`, sin tocar el prefab de MapaV1 ni al titán).
- **Cajas** se rompen y sueltan munición, curación o buff. El objeto sube y se encoge al recogerlo.
- **Capas:** Jugador, Aliados, Enemigos, ArmaJugador, ArmaEnemigo, Objetos, Recogibles. El arma enemiga solo toca al jugador; la del jugador toca enemigos y cajas.
- **NavMesh** horneado en `Assets/Scenes/MapaV2/NavMesh-Isla.asset`. La escena queda en texto.
- **Audio Mixer** `Assets/Resources/Audio/MezcladorPrincipal.mixer` con grupos Master, Musica, Efectos y Voces.
- **Cinemáticas** con corrutina: la intro de CJ, el plano del jefe al entrar en su zona y la salida en la lancha. Espacio o A adelantan el diálogo.
- **Configurar:** `Tools > Isla > Configurar`. **Verificar:** `Tools > Isla > Verificar` escribe `Logs/isla-validada.txt`.
- Los clips de atacar, recibir golpe y morir se leen de `Assets/Animaciones/Combate/` (Mixamo, FBX for Unity, Without Skin). `Tools > Isla > Configurar` los asigna también si la capa Combate ya existe. Sin esos archivos el Animator entra a los estados, pero con los clips de locomoción de reemplazo.

---

## Controles

- **1 / 2** o cruceta izquierda/derecha: bate o rifle.
- **Clic izquierdo / RT:** golpear con el bate, o disparar si el rifle está seleccionado.
- **Q:** botiquín. **F:** bebida energética.
- **H:** ocultar o mostrar las indicaciones. También en **Esc**. La vida, las botellas, el mapa y la munición siguen visibles.
- **WASD:** mover; **ratón:** cámara; **Espacio:** saltar; **Shift:** correr.
- **Esc:** pausar y continuar; **M:** ampliar o reducir mapa sin pausar.
- **Gamepad:** sticks para movimiento/cámara, botón sur para saltar y gatillo izquierdo para correr.
- Objetivo en la isla: juntar las 30 botellas y volver a la lancha de CJ.

---

## Estructura del proyecto

```
Assets/
├── Animaciones/           # Controladores y clips del personaje
├── Inputs/                # Acciones de input
├── Materiales/            # Materiales del nivel y del personaje
├── Prefabs/               # Personaje, enemigos, coleccionables y obstáculos
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
