## Incremento ART-10: esquinas redondeadas y salto hacia atrás al trepar

- Objetivo/gate: dos peticiones de Jorge (2026-11-17): que los edificios puedan tener las esquinas redondeadas, y poder saltar hacia atrás estando agarrado a una fachada.
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/World/CornerRounding.cs` (nuevo, lógica pura): quita un triángulo de celdas en cada esquina saliente recta; solo cuenta como esquina el ángulo recto con un bloque macizo de cut × cut detrás (así no se tocan escalones sueltos, esquinas entrantes ni muros finos).
  - `CityBlocks`: campo/propiedad `CornerRoundingPasses` y contador `CellsRemovedByRounding`; la rejilla se recorta antes de levantar muros, colisiones, ventanas y navegación. `Z01GeoBuilder.CityCornerRounding = 2`; escalinata sin recorte. Z01 regenerada.
  - Salto hacia atrás: `ClimbLimits.BackJumpSpeed/BackJumpUp`, `ClimbRules.CanJumpBack/BackJumpVelocity`, `PlayerWallClimb.Tick(..., jumpPressed)` (gira al jugador de cara a la salida, lo separa 0,1 m, bloquea re-agarre 0,4 s), `PrototypePlayerController.FinishClimb` aplica la velocidad; ayuda en pantalla «Espacio / A: saltar hacia atrás».
  - Tests: `CornerRoundingTests` (9) y `ClimbRulesTests` (+2) en EditMode; `Z01SceneTests.City_CornersAreRounded_...` y `Player_ClimbingJumpsBackwards_OffTheWall` en PlayMode.
- Comandos ejecutados: Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode (Unity 6000.3.6f1).
- Pruebas: aprobadas 117/117 EditMode y 36/36 PlayMode; VerifyProject 0 errores. Dos iteraciones de diseño: el primer recorte (quitar toda celda con dos lados vacíos) comía el 11,5 % de la ciudad (erosionaba los muros diagonales) y el segundo (lados rectos de ≥3 celdas) solo 344 m²; el criterio final recorta 2.127 m² (1,4 %). El test del salto falló al principio por un fallo del test (W y Mayús soltadas en el mismo fotograma: el segundo evento pisaba al primero), no del juego.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/play_z01_salto_atras.png` y `play_z01_esquinas.png` (la cámara del juego no encuadra una esquina: no sirve para juzgar el redondeo; hay que verlo en Unity).
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D59.
- Problemas conocidos: el redondeo es en escalera de 1 m y de efecto discreto; en el salto hacia atrás sin pulsar nada el jugador se separa solo ~1,6 m y sube ~0,6 m (el control aéreo frena la velocidad horizontal); sin jugar a mano.
- Próximo paso más pequeño: que Jorge lo pruebe y diga si quiere esquinas más grandes (cut 3) o un salto más largo.
