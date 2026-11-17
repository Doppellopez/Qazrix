## Incremento ART-14: la cámara sigue al personaje y da media vuelta al pulsar atrás

- Objetivo/gate: (a) que la cámara se oriente hacia donde mira el personaje; (b) que al pulsar atrás gire 180° despacio y se quede ahí, y el personaje gire también (peticiones de Jorge, 2026-11-17).
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/Movement/CameraFollowRules.cs` (nuevo, puro): `ShouldFollow`, `ShouldStartTurnAround`, `MovementYaw`, `TurnAroundStep`.
  - `PrototypeOrbitCamera`: seguimiento suavizado (0,35 s) de la orientación del personaje; media vuelta de 180° a 90°/s al pulsar atrás, con `IsTurningAround`, `IsFollowingFacing` y `MovementYaw`; una cámara movida a mano se respeta 1,5 s y cancela la vuelta en curso.
  - `PrototypePlayerController.Wished`: con la cámara orbital, WASD usa `MovementYaw` (la orientación de antes de la media vuelta mientras se mantenga atrás).
  - Escenas Prototype y Z01 regeneradas.
  - Tests: `CameraFollowRulesTests` (EditMode, 9) y dos en `PrototypeSceneTests` (`Camera_FollowsWhereTheCharacterFaces_WhenTurningWithArrows`, `Camera_TurnsAround180_WhenBackIsPressed_AndTheCharacterTurnsToo`).
- Comandos ejecutados (Unity 6000.3.6f1): RebuildBatch de Prototype, Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode.
- Pruebas: VerifyProject 0 errores; aprobadas 147/147 EditMode y 40/40 PlayMode. El test del seguimiento falló al principio por un fallo del propio test: el teclado simulado conserva pulsada la tecla soltada si se pulsa otra en el mismo fotograma; se añadió una espera de 3 fotogramas.
- Build y plataforma: sin build nuevo.
- Evidencia: sin captura (la prueba mide ángulos de la cámara y del personaje).
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D63.
- Problemas conocidos: sin jugar a mano; la flecha abajo no da media vuelta; la media vuelta solo ocurre al pulsar (soltar y pulsar otra vez para repetirla); con WASD y atrás mantenido, el personaje sigue yendo al mismo sitio tras la vuelta (comportamiento pensado, no probado a mano); 90°/s y 0,35 s son valores de prototipo.
- Próximo paso más pequeño: que Jorge lo pruebe con WASD y con las flechas y diga si la velocidad de la media vuelta y el seguimiento convencen.
