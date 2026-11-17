## Incremento ART-09: la capa ya no atraviesa el cuerpo

- Objetivo/gate: que al subir (trepar) y saltar el cuerpo del hidalgo no asome por la capa (petición de Jorge, 2026-11-17).
- Causas: (a) en reposo la capa (z=-0,175) ya solapaba con la cadera (atrás en -0,19); (b) al subir, `CapeSim` la apretaba hacia delante contra la cadera; (c) la pierna trasera de las poses de salto y trepada atravesaba el borde de abajo.
- Cambios de código/escenas/assets:
  - `CapeSim`: tope duro `MinLift=1°` (sin presión hacia delante al subir), parámetro `legBackSwing` (0,25° de capa por grado de pierna hacia atrás) y `HemSetback()`.
  - `CapeAnimator`: recibe los pivotes de cadera y mide la pierna más echada hacia atrás.
  - `HidalgoBuilder`: pivote de la capa en z=-0,215 y piernas pasadas al animador de la capa; escenas Prototype y Z01 regeneradas.
  - Pruebas: `CapeSimTests` (11, EditMode) y `CapeClipping` (PlayMode): cuenta puntos de la capa dentro de cadera, calzones y botas; usada quieto, corriendo, saltando (`CharacterModelTests`) y trepando (`Z01SceneTests.Player_ClimbsAFacade_...`).
- Comandos ejecutados (Unity 6000.3.6f1): RebuildBatch de Prototype, Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode.
- Pruebas: VerifyProject 0 errores. Pasan las 11 de `CapeSimTests`, las 2 de la capa en PlayMode y la trepada de Z01 con 0 penetraciones. La pasada completa dio 115/116 EditMode y 34/36 PlayMode: los fallos son de otra tarea en curso (redondeo de esquinas `CornerRoundingTests` y salto hacia atrás en Z01), no de la capa.
- Build y plataforma: sin build nuevo.
- Evidencia: pruebas geométricas (0 puntos dentro del cuerpo en reposo, carrera, salto y trepada). No hay captura lateral nueva.
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D58.
- Problemas conocidos: sin jugar a mano; la medida es sobre primitivas (no hay colisión de ttela); no se comprueban la espada ni los brazos; la capa queda 4 cm más atrás.
- Próximo paso más pequeño: que Jorge vuelva a mirar la capa trepando y saltando; si aún ve cruces, sacar una captura lateral.
