## Incremento F1-03: parkour contextual mínimo

- **Objetivo/gate:** tercer corte de F1 (spec §10: «parkour contextual de un conjunto mínimo de obstáculos») según spec §6.1 y D09: contextual y predecible, solo en obstáculos marcados, sin escalada universal.

- **Cambios de código/escenas/assets:**
  - `Scripts/Simulation/Movement/ParkourRules.cs` (lógica pura): `ParkourRules.Decide` (saltar un obstáculo de 0,3 a 1,1 m de alto y hasta 1,2 m de grueso; trepar hasta 2,3 m; nunca sin sitio en la llegada ni más de lo que autoriza la marca), `ParkourRules.Duration` y `ParkourPath.Evaluate` (arco que supera el borde con 0,35 m de margen al saltar; subir y después avanzar al trepar).
  - `Scripts/Runtime/Prototype/ParkourMarker.cs`: marca un obstáculo y dice qué se puede hacer en él.
  - `Scripts/Runtime/Prototype/PlayerParkour.cs`: mide el obstáculo que hay delante con consultas físicas (cara, altura, grosor, sitio en la llegada), decide con las reglas puras y lleva el cuerpo por la trayectoria.
  - `PrototypePlayerController`: la tecla de salto es contextual. Ante un obstáculo marcado y practicable, de pie y en el suelo, lo salta o lo trepa; en cualquier otro caso es el salto de siempre. Durante la acción no hay control.
  - `PrototypeHud`: línea de ayuda «Espacio / A: saltar el obstáculo» o «Espacio / A: trepar» cuando la acción está disponible.
  - `PrototypeSceneBuilder`: marca `Obstaculo_bajo` como saltable y `Plataforma` como trepable; el jugador lleva `PlayerParkour`. Escenas `Prototype` y `Z01_Relieve` regeneradas.

- **Comandos ejecutados** (Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`): `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoBuilder.BuildBatch`, `VerifyProject.RunBatch`, `-runTests -testPlatform EditMode` (con `-batchmode`) y `-runTests -testPlatform PlayMode` (con ventana, dos veces).

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional); EditMode 51/51 (9 nuevos); PlayMode 14/14 (4 nuevos) en la segunda tanda.
  - Fallida en la primera tanda y corregida: la prueba de trepar colocaba al jugador encima del escalón que hay delante de la plataforma, así que llegaba en el aire y la acción no se ofrecía. Era un defecto de la prueba, no del parkour: ahora se acerca por la cara norte.
  - Pruebas en Play nuevas: la tecla de salto pasa el obstáculo bajo y deja al jugador de pie al otro lado; trepa a la plataforma y lo deja encima, a 1 m; en campo abierto es un salto normal; la pared alta, sin marca, no se puede trepar.
  - No ejecutadas: **Play a mano** y las 20 repeticiones del circuito que pide spec §6.1. Tampoco está probado con mando ni a distintos fotogramas por segundo.

- **Build y plataforma:** sin build.

- **Evidencia:** `Reports/QA/play_parkour_aviso.png` (ayuda «Espacio / A: saltar el obstáculo» delante del obstáculo), `Reports/QA/playmode-results.xml`, `Reports/QA/editmode-results.xml`, `Reports/QA/verify-project-latest.md`.

- **Derechos/licencias de activos nuevos:** ninguno.

- **Decisiones tomadas:** D39.

- **Problemas conocidos:**
  - Solo dos acciones (saltar por encima y trepar) y dos obstáculos marcados. No hay colgarse de bordes, bajar ni saltar entre plataformas.
  - Sin animación: el cuerpo se desliza por la trayectoria.
  - Hay que estar de frente, de pie y en el suelo; no se encadena desde un salto ni corriendo en diagonal.
  - El salto normal ya alcanza 1,2 m, así que a la plataforma también se sube saltando: trepar es más fiable, no imprescindible.
  - El guardia no hace parkour ni lo tiene en cuenta: no sube a la plataforma, y el jugador no hace ruido al saltar un obstáculo o trepar (solo al caer con golpe).
  - La altura se toma de la caja envolvente del colisionador: vale para cajas rectas, no para formas inclinadas o irregulares.
  - La captura muestra en azul la malla de navegación: es una ayuda de depuración del editor, no forma parte del juego.

- **Próximo paso más pequeño:** que Jorge recorra el circuito de `Prototype` usando Espacio delante del obstáculo bajo y de la plataforma, y diga si los tiempos se sienten bien. Lo que falta de F1 según la spec: sistema de interacción, checkpoint y build de Windows probado en otro PC.

## Añadido: guardia y parkour también en Z01_Relieve (2026-11-12)

- **Motivo:** Jorge probó en `Z01_Relieve` y no vio guardia ni parkour: solo estaban en `Prototype`.
- **Cambios:** `Z01GeoBuilder` añade una «zona de juego» de prueba alrededor del origen: murete de 0,5 m (saltar) a 6 m al norte, plataforma de 1 m (trepar) a su izquierda, ambos hundidos 2 m en el relieve; malla de navegación `Scenes/Z01/Z01_NavMesh.asset` en un cuadrado de 160 m; guardia que recorre un cuadrado de 40 m. `PrototypeSceneBuilder.BakeNavMesh` y `Box` pasan a ser reutilizables. Nuevo `Tests/PlayMode/Z01SceneTests` (3 pruebas).
- **Comandos (salida 0 todos):** `Z01GeoBuilder.BuildBatch`, `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoReport.RunBatch`, `VerifyProject.RunBatch`, tests EditMode y PlayMode, `CaptureZ01Batch`.
- **Pruebas:** VerifyProject 0 errores; EditMode 51/51; PlayMode 17/17. En Z01: el jugador queda apoyado en el relieve, el guardia patrulla siguiendo la malla sobre el terreno y Espacio salta el murete. Puntos de control siguen en tolerancia.
- **Límites:** son piezas de prueba, no geometría de Cáceres; la trepada a la plataforma y la detección del guardia en Z01 no tienen prueba propia; fuera del cuadrado de 160 m no hay malla y el guardia iría en línea recta; sin jugar a mano.
