## Incremento F2-03: trepada libre por fachadas y tejados

- **Objetivo/gate:** primer corte de la dirección «acción-sigilo tipo AC» (D27) sobre la ciudad de F2, por indicación de Jorge del 2026-11-15 («quiero que el juego se parezca al Assassin's Creed»; eligió empezar por trepar y tejados). Se toman mecánicas de género, no personajes, nombres, interfaz ni arte de ese juego.

- **Cambios de código/escenas/assets:**
  - `Scripts/Simulation/Movement/ClimbRules.cs` (lógica pura): cuándo puede agarrarse a una pared, velocidad de subida y bajada con zona muerta, y trayectoria al encaramarse (primero sube, luego entra en el tejado).
  - `Scripts/Runtime/Prototype/PlayerWallClimb.cs`: corriendo contra una fachada el jugador se agarra a ella, también en el aire tras un salto. Adelante sube (3 m/s), atrás baja, izquierda y derecha lo desplazan por la pared, agacharse la suelta. Al llegar al borde se encarama al tejado. Bajando hasta el suelo queda de pie.
  - `PrototypePlayerController`: integra la trepada (propiedad `IsClimbing`) y la cancela al reaparecer.
  - `CityBlocks.Climbable`: los edificios se pueden trepar; los peldaños de la escalinata no.
  - `PrototypeHud`: línea de ayuda «Trepar: correr contra una fachada · agacharse suelta».
  - `Z01_Relieve` regenerada: el jugador lleva `PlayerWallClimb`. La escena `Prototype` no se ha regenerado (no tiene edificios).

- **Comandos ejecutados:** Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`: `Z01GeoBuilder.BuildBatch`, tests EditMode, tests PlayMode, `VerifyProject.RunBatch`, `BuildWindows.BuildBatch`.

- **Pruebas:**
  - Aprobadas: compilación; EditMode 74/74 (3 nuevas de `ClimbRules`); PlayMode 28/28; `VerifyProject` 0 errores, 1 aviso.
  - Prueba en Play nueva (`Z01_Relieve`): el jugador corre contra una fachada al norte de la Plaza Mayor, se agarra, sube 2 m, se suelta agachándose y cae al suelo; vuelve a trepar y queda de pie en el tejado, a 15,3 m sobre la calle.
  - Falladas por el camino y corregidas: un error de compilación (espacio de nombres de `Float3`) y dos versiones de la comprobación final de la prueba, que consultaba la celda equivocada cuando el jugador queda en el mismo borde del tejado; la trepada en sí no cambió.
  - No ejecutadas: **Play a mano**; saltar de un tejado a otro, agarrarse en el aire, moverse de lado y bajar trepando no tienen prueba automática; el mando no se ha probado.

- **Build y plataforma:** build de Windows regenerado en `Builds/Windows` (fuera de git); no se ha jugado.

- **Evidencia:** `Reports/QA/play_z01_trepada.png`, `play_z01_tejado.png`, `playmode-results.xml`, `editmode-results.xml`.

- **Derechos/licencias de activos nuevos:** ninguno.

- **Decisiones tomadas:** D45.

- **Problemas conocidos:**
  - No hay animación de trepar: el hidalgo sube en la pose de aire.
  - Se puede trepar cualquier fachada a cualquier altura, sin asideros, cansancio ni límite.
  - Caer desde un tejado no hace daño.
  - Los muros en diagonal son dientes de sierra de 1 m: al moverse de lado el cuerpo da saltos entre caras.
  - El guardia no ve ni sigue al jugador a los tejados de forma especial: su malla de navegación incluye los tejados, pero no sabe trepar.
  - Si no hay sitio para ponerse de pie arriba, el jugador se queda colgado en el borde.

- **Añadido el mismo día — animación de escalada** (petición de Jorge: «hacer animación de escalada»):
  - `Scripts/Simulation/Movement/ClimbCycle.cs` (lógica pura): brazos por encima de la cabeza que se alternan, piernas que empujan en diagonal, y la postura de encaramarse (los brazos pasan a empujar hacia abajo y una pierna sube al borde). La fase avanza con lo que el cuerpo recorre por la pared: colgado y quieto conserva la postura.
  - `CharacterWalkAnimator` la aplica cuando el jugador trepa o se encarama, con los dos brazos; `PlayerWallClimb` expone `IsMantling` y `MantleProgress`. No hizo falta regenerar la escena.
  - Pruebas: EditMode 78/78 (4 nuevas de `ClimbCycle`); PlayMode 28/28, y la prueba de trepada comprueba además que los dos brazos van levantados y se alternan. Captura `Reports/QA/play_z01_trepada.png`.
  - Límites: cada extremidad tiene un solo pivote (sin codos ni rodillas), así que es una pose esquemática; el cuerpo no se inclina ni se pega a la pared, y moverse de lado usa el mismo ciclo. Esto sustituye al primer problema conocido de arriba («no hay animación de trepar»).

- **Próximo paso más pequeño:** que Jorge trepe a un tejado, salte a otro y diga qué le falta para que se sienta como quiere. Después: animación de trepar y salto de fe o atalaya.
