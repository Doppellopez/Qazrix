## Incremento ART-08: capa flexible del hidalgo

- Objetivo/gate: que la capa se mueva al caminar y saltar (petición de Jorge, 2026-11-17).
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/Movement/CapeSim.cs` (nuevo, lógica pura): tres tramos con resorte amortiguado; reacciona a la velocidad hacia delante, vertical y lateral.
  - `Scripts/Runtime/Prototype/CapeAnimator.cs` (nuevo): mide el desplazamiento de la raíz y aplica los ángulos a los pivotes.
  - `HidalgoBuilder`: la capa pasa de una pieza a tres tramos articulados (`Capa_articulacion_1..3`), cada uno con sus pliegues; escenas Prototype y Z01 regeneradas.
  - Tests: `CapeSimTests` (7, EditMode) y dos pruebas en `CharacterModelTests` (correr y asentarse; subir y caer).
- Comandos ejecutados: RebuildBatch de Prototype, Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode (Unity 6000.3.6f1).
- Pruebas: aprobadas 103/103 EditMode y 34/34 PlayMode; VerifyProject 0 errores. En la primera pasada falló la prueba del salto: el hidalgo corría hacia un murete y la tecla de salto se convirtió en parkour. Se separó en una prueba propia que salta quieto y comprueba que el salto ocurre.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/play_hidalgo_capa_corriendo.png` (de espaldas: al levantarse hacia atrás la capa queda tapada por el propio cuerpo; no sirve para juzgar el movimiento).
- Derechos/licencias de activos nuevos: ninguno (la capa reutiliza la textura Hidalgo_Capa).
- Decisiones tomadas: D57.
- Problemas conocidos: sin jugar a mano; los números (viento, rigidez, amortiguación) son de prototipo y se ajustan a ojo; la capa atraviesa el cuerpo si se levanta mucho al correr de espaldas a la pared y no choca con nada.
- Próximo paso más pequeño: que Jorge la vea en Unity corriendo, saltando y cayendo, y decir si va demasiado suelta o rígida.
