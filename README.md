# La Isla del Curao — Evaluación 3 / proyecto final (UCSC 2026-2)

Juego de **rol y acción en tercera persona** hecho en **Unity 6000.3.21f1 (URP)** para la asignatura de Desarrollo de Videojuegos. Tema libre.

CJ deja al protagonista en una isla infestada de zombies con un solo encargo: juntar todo el alcohol que encuentre. El jugador recorre la isla, pelea con bate, rifle y escopeta, abre cajas, se cura y, al completar las botellas, escapa en lancha para enfrentar al jefe.

**Flujo:** `MenuInicio` → `MapaV2` (la isla) → pantalla de resumen → menú.

---

## Integración de requerimientos

### Gestión de escenas
- Dos escenas: `MenuInicio` y `MapaV2` (`ControladorMenus.EscenaMenu` / `EscenaJuego`).
- La carga es **asíncrona en corrutina** (`ControladorMenus.CargarEscena`): `LoadSceneAsync` con barra de progreso y porcentaje frame a frame, y activación al terminar.
- El resumen permite **volver a jugar** (recarga la isla) o **ir al menú principal**, por el mismo camino asíncrono. Si se entra a `MapaV2` directo desde el editor, el juego rebota al menú para no saltarse el flujo.

### Nivel, menú y entidades acompañantes
- Un nivel jugable completo (la isla), menú de inicio y pantalla de cierre.
- El jugador no está solo: **CJ** es un NPC que aparece en la intro y en el final, y la isla está poblada de **zombies** y del **jefe**.

### Daño en ambos sentidos, con animación y sonido
- El jugador golpea con el **bate** y dispara **rifle** y **escopeta**; los zombies y el jefe le pegan de vuelta.
- Cada impacto dispara el flujo completo: golpe conectado → daño → animación → sonido.
- `ControladorVida` es el componente de vida compartido por zombies, jefe y cajas. `ControladorPartidaBotellas.RecibirDanio` es la única puerta de entrada del daño al jugador.

### Animator: atacar, recibir golpe y morir
- Estados **Atacar**, **Golpe** y **Morir** para el jugador y para los enemigos (`EnemigoCombate.controller`, con overrides por tipo de enemigo).
- La ventana de daño del cuerpo a cuerpo se abre y cierra con **eventos de animación** (`ActivaArma` / `DesactivaArma`) escritos en la importación del FBX, no con temporizadores.
- Los enemigos mezclan locomoción con un blend tree sobre el parámetro `Speed`.

### NavMesh Surface + Bake, enemigos que persiguen
- `NavMeshSurface` horneada y guardada **como asset** en `Assets/Scenes/MapaV2/NavMesh-Isla.asset` (así la escena sigue en texto y es mezclable en git).
- Los zombies persiguen con `NavMeshAgent.SetDestination`, refrescando el destino en corrutina, y atacan al entrar en rango. `DirectorZombies` administra la horda.
- Los puntos de aparición se filtran por la altura del mar para que nadie salga bajo el agua.

### Rigidbody y colisiones por capas
- Siete capas: **Jugador, Aliados, Enemigos, ArmaJugador, ArmaEnemigo, Objetos, Recogibles**.
- `CapasJuego` reaplica la matriz de colisión en tiempo de ejecución: `ArmaEnemigo` solo toca al jugador, `ArmaJugador` toca enemigos y cajas, y `Recogibles` solo al jugador. Resultado: **los zombies no se dañan entre sí**, pero jugador ↔ enemigo sí funciona.

### Inventario básico
- **1 / 2 / 3:** bate, rifle y escopeta.
- **Q:** botiquín (+30 % de vida). **F:** bebida energética (buff de +50 % de daño y velocidad por 15 s).
- La munición vive en cada arma; el HUD muestra icono, color y nombre según el arma activa.

### Coleccionables con animación de recogida
- **20 botellas objetivo:** 19 repartidas por la isla y la última la suelta el jefe. El medidor de ebriedad es el conteo n/20.
- Al recogerse, el objeto **sube, se encoge y se desvanece** con un sonido suave, en vez de desaparecer de golpe.
- Las **cajas** se rompen a batazos o con el interactuador y sueltan munición, curación o buff.

### Audio Mixer
- `Assets/Resources/Audio/MezcladorPrincipal.mixer` con grupos **Master, Música, Efectos y Voces**.
- Todo el sonido pasa por `Sonido.Reproducir` / `Sonido.Asignar`, nunca por `AudioSource` suelto. Los clips del nivel viven en el `BancoSonidos` de la escena.

### Cinemáticas
- Tres momentos en corrutina: la **intro** de CJ en la lancha, el **plano del jefe** al entrar en su zona y la **salida final** con fuegos artificiales.
- `SuspensionCinematica` congela al jugador y a los enemigos durante los planos; los diálogos se adelantan con **Espacio / A**.

### UI, input y sonido
- HUD con vida, botellas, munición, inventario y mapa circular estilo radar con exploración progresiva (**M** lo amplía sin pausar).
- **Input System** sobre `ThirdPersonController` (Starter Assets): teclado, ratón y gamepad.
- Música en loop y efectos de recolección, impacto, daño, victoria y derrota. **Esc** pausa sin cortar el audio de interfaz.

---

## Reutilizado del repositorio de clases

Del repo del profesor (`ClaseUCSC2026`), adaptado a nombres y necesidades del proyecto:

| De la clase | Dónde se usa |
| :--- | :--- |
| `MobController` / `MovimientoSimpleNPC` | Patrón de persecución con `NavMeshAgent` en `EnemigoAcechador` |
| `MobAnimController` | Animator de enemigos: blend tree sobre `Speed` + Atacar/Golpe/Morir |
| `ControladorJugador` | Vida como propiedad que emite `UnityEvent<float>` |
| `ControladorBarraSalud` | Barras de vida por parámetro de Animator |
| `Ataque01` y sus eventos | Eventos `ActivaArma` / `DesactivaArma` en los clips de ataque |
| `DemoControlInteraccion` | `ControladorInteraccion`: zona de interacción para romper cajas con **E** |
| Demo del jefe grande | Tipo de agente de NavMesh propio para el titán de `MapaV1` |
| Material del curso | Starter Assets, personajes Synty, Polygon Particle FX y Ultimate Sound FX Bundle |

También se siguieron sus convenciones: nombres en español (`Controlador*`), campos públicos o `[SerializeField]` cableados en el Inspector, `UnityEvent` para desacoplar y `GetComponent` de respaldo en `Start` / `Reset`.

## Nuevo, hecho por el equipo

- **`ControladorPartidaBotellas`:** centro de estado de la partida (vida, ebriedad, botellas, resultado y causa) con eventos a los que se suscribe todo el HUD.
- **Sistema de armas de fuego:** `ControladorArmaFuego` con disparo hitscan, munición y recarga (`EstadoMunicion`, lógica pura y testeable), cámara de apuntado al hombro, casquillos y efectos de impacto.
- **Poses procedurales:** `PoseRifleProcedural` y `PoseBateProcedural` colocan brazos y arma sobre el rig Humanoid por código.
- **Combate cuerpo a cuerpo:** `GolpeMelee` como hitbox de trigger con un impacto por objetivo y por golpe.
- **Pelea del jefe en la lancha** con sus propias fases y dificultad.
- **Audio propio:** `SintesisAudio`, `SonidoZombie`, `SonidoLancha` y las voces grabadas de CJ en la intro y la pelea.
- **HUD de armas** con icono, color y animación por arma, y mapa con niebla de exploración.
- **Herramientas de editor:** `ConfigurarIsla` genera la isla completa (capas, mixer, animators, prefabs, cajas, botellas, zombies, jefe, NavMesh) desde un script, y `ValidarIsla` entra a Play y corre **24 comprobaciones** automáticas que se escriben en `Logs/isla-validada.txt`.

---

## Principales dificultades

1. **Las armas no se veían bien al moverse.** Los modelos no calzaban con las animaciones del personaje: el rifle y el bate quedaban flotando o atravesando el cuerpo. Se resolvió con **poses procedurales** (IK sobre el rig Humanoid) en vez de buscar clips nuevos para cada arma.
2. **Rendimiento por el alcance de las balas.** Con mucho alcance, los proyectiles y sus efectos **se acumulaban** en escena y la tasa de cuadros caía. Se pasó a **disparo hitscan** con alcance limitado y reciclaje de efectos e impactos.
3. **Sonidos superpuestos.** Con varios zombies, disparos y la lancha a la vez, el audio saturaba y se pisaba. Se centralizó todo en el **Audio Mixer** por buses y se limitaron las voces simultáneas por tipo de sonido.
4. **Zombies bajo el mar y la lancha "hundida".** Los puntos del NavMesh caían bajo el nivel del agua, así que ahora se **filtran por altura del mar**. La lancha, en cambio, no estaba hundida: era el **shader de agua transparente** dibujándose sobre el casco. Diagnosticarlo evitó "arreglar" algo que funcionaba.
5. **Generar la isla a mano era insostenible.** Cada ajuste tocaba decenas de objetos. Se movió todo a `ConfigurarIsla`, de modo que la escena es **salida regenerable** de un script y no se edita a mano.
6. **Verificar sin romper el flujo.** Unity no recompila sin foco y solo admite una instancia por proyecto, así que las pruebas se automatizaron con **disparadores por archivo y logs** (`Tools > Isla > Configurar` / `Verificar`).
7. **Clips de combate pendientes.** `Assets/Animaciones/Combate/` está vacío, así que los enemigos entran correctamente a los estados Atacar, Golpe y Morir, pero con **clips de reemplazo** de locomoción. Al dejar los FBX de Mixamo en esa carpeta, `Configurar` los asigna sin cambiar código.

---

## Controles

- **1 / 2 / 3** o cruceta: bate, rifle, escopeta.
- **Clic izquierdo / RT:** golpear o disparar según el arma activa. **Clic derecho / LB:** apuntar. **R / Y:** recargar.
- **E / X:** recoger arma o munición, y romper cajas por interacción. **G:** soltar el arma.
- **Q:** botiquín. **F:** bebida energética.
- **WASD:** mover. **Ratón:** cámara. **Espacio:** saltar. **Shift:** correr.
- **Esc:** pausar. **M:** ampliar o reducir el mapa sin pausar. **H:** ocultar o mostrar las indicaciones.
- **Gamepad:** sticks para movimiento y cámara, botón sur para saltar, gatillo izquierdo para correr.
- **Objetivo:** juntar las 20 botellas y volver a la lancha de CJ.

---

## Herramientas del proyecto

- `Tools > Isla > Configurar` regenera la isla completa. `Tools > Isla > Verificar` corre la validación en Play y escribe `Logs/isla-validada.txt`.
- `Tools > Enemigos > Configurar` / `Verificar` mantienen los enemigos de `MapaV1`.
- Con el editor cerrado: `Unity -batchmode -projectPath . -executeMethod ConfigurarIsla.Ejecutar -quit`.
- Los assets generados (prefabs, animators, NavMesh, escena) **se cambian editando el generador y regenerando**, nunca a mano.

## Estructura del proyecto

```
Assets/
├── Animaciones/           # Controladores, overrides y clips (Combate/ para Mixamo)
├── Editor/                # ConfigurarIsla, ValidarIsla y otras herramientas
├── Inputs/                # Acciones de Input System
├── Prefabs/               # Jugador, enemigos, coleccionables y props
├── Resources/             # Armas, catálogo de objetos, mixer y fuentes
├── Scenes/                # MenuInicio, MapaV2 (isla) y MapaV1 (ciudad, etapa anterior)
├── Scripts/
│   ├── Gameplay/          # Partida, combate, inventario, enemigos, audio y cinemáticas
│   └── UI/                # Menús, HUD, mapa y diálogos
├── Starter Assets/        # ThirdPersonController e input base
└── Synty/                 # Modelos y animaciones de apoyo
```

`MapaV1` (la ciudad de la evaluación anterior, con Nemesis y el titán) sigue en el repositorio como referencia, pero no se alcanza desde el menú.

---

## Créditos

- Modelo de **Nemesis** por **toheed_zahid** en Sketchfab, licencia **CC-BY-4.0**. Esta atribución debe mantenerse.
- Personajes, props y partículas de los packs **Synty Polygon** del material del curso. Animaciones de **Kevin Iglesias** (Basic Motions, Melee Warrior) y de **Mixamo** (Adobe) para los clips de combate.
- Efectos de sonido del **Ultimate Sound FX Bundle** del material de clase.
- Fuentes **Anton** y **Bangers**, licencia **SIL Open Font License 1.1**; los archivos y licencias están en `Assets/Resources/Fuentes/`.

## Autores

| Nombre | GitHub |
| :--- | :--- |
| Vicente Alarcón | [@vicente-ai](https://github.com/vicente-ai) |
| Benjamín Bizama | [@Tweet-y](https://github.com/Tweet-y) |
| Nicolás Valdebenito | [@NicoValdebenito](https://github.com/NicoValdebenito) |
