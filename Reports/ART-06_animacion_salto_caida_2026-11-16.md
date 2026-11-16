## Incremento ART-06: animación de salto y caída

- Objetivo/gate: que los personajes tengan postura propia al saltar, caer y aterrizar (petición de Jorge, 2026-11-16). Antes había una única pose fija «en el aire». No cierra ningún gate.
- Cambios de código/escenas/assets:
  - `Assets/_Qazrix/Scripts/Simulation/Movement/AirCycle.cs` (nuevo, lógica pura): posturas según la velocidad vertical — subida (brazos arriba, una rodilla delante), punto alto (recogido), caída (brazos abiertos arriba que bracean y piernas que pedalean) — y pose breve de amortiguación al aterrizar (0,22 s).
  - `CharacterWalkAnimator`: mide la velocidad vertical de la raíz, el tiempo en el aire y el tiempo desde el aterrizaje, y aplica `AirCycle`. No cambia de pose por perder el suelo menos de 0,06 s, y solo amortigua si venía de al menos 0,25 s en el aire. Expone `IsAirPose`, `IsLandingPose` y `VerticalSpeed`. Se retira el campo `airLegAngle`.
  - Tests: `Tests/EditMode/AirCycleTests.cs` (5) y `PrototypeSceneTests.Jump_ShowsRise_Fall_AndLandingPoses` (salta, comprueba las tres poses y guarda la captura con una cámara lateral temporal).
  - Sin cambios en escenas ni activos nuevos: vale para el hidalgo y para el guardia en las dos escenas.
- Comandos ejecutados (Unity 6000.3.6f1): tests EditMode (sin interfaz) y tests PlayMode (dos pasadas: la segunda solo cambia la cámara de la captura).
- Pruebas: aprobadas 89/89 EditMode y 29/29 PlayMode. No ejecutadas: `VerifyProject` (no cambian activos, escenas ni ajustes); jugar a mano; caída larga desde un tejado (el test solo cubre un salto en llano); salto de obstáculo de parkour y guardia en el aire, que usan el mismo código pero no tienen prueba propia; builds.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/play_salto.png` (hidalgo de lado en lo alto del salto), `editmode-results.xml`, `playmode-results.xml`.
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D54.
- Problemas conocidos:
  - Un solo pivote por extremidad: no hay flexión de rodillas, codos ni tronco, así que la amortiguación es una zancada, no una sentadilla.
  - El guardia no levanta el brazo derecho (lleva el sable).
  - Ángulos y tiempos puestos a ojo, sin ajustar jugando.
- Próximo paso más pequeño: que Jorge salte y se tire de un tejado en `Z01_Relieve` y diga si las poses convencen.
