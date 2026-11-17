## Incremento ART-12: brazo y cimitarra en alto al perseguir

- Objetivo/gate: que el guardia que persigue lo haga con el brazo y la cimitarra en alto (petición de Jorge, 2026-11-17).
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/Movement/WeaponRaise.cs` (nuevo, lógica pura): peso de 0 a 1 (`Blend`), ángulo del brazo con balanceo (`ArmAngle`) y giro del puño del arma (`GripAngle`).
  - `CharacterWalkAnimator`: `WeaponRaised`, `WeaponRaiseWeight`, `SetWeapon`; si tiene puño de arma, sube el brazo derecho y orienta la hoja.
  - `GuardController`: pone `WeaponRaised` cuando su estado es `Alert`.
  - `GuardModelBuilder`: registra el nodo «Sable» como puño del arma. Escenas Prototype y Z01 regeneradas.
  - Tests: `WeaponRaiseTests` (5, EditMode) y `GuardTests.ChasingGuard_HoldsTheArmAndScimitarHigh_AndLowersThemAfterwards` (PlayMode): patrullando lleva el arma abajo; persiguiendo el peso sube a 1, el brazo pasa de 110° y la punta queda ≥30 cm más arriba que con el brazo caído; al acabar la persecución baja.
- Comandos ejecutados (Unity 6000.3.6f1): RebuildBatch de Prototype, Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode (tres pasadas de PlayMode).
- Pruebas: VerifyProject 0 errores; aprobadas 136/136 EditMode y 38/38 PlayMode. Las dos primeras pasadas fallaron solo por mi test del guardia (la punta de la hoja curva ya queda unos 10 cm sobre el puño con el brazo caído, y la persecución tarda más de 5 s en acabar): corregido.
- Build y plataforma: sin build nuevo.
- Evidencia: sin captura del guardia persiguiendo (las pruebas miden ángulos y alturas, no se ha visto en pantalla).
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D61.
- Problemas conocidos: sin jugar a mano; la mano no se cierra sobre la empuñadura (esfera suelta); sin cabeceo del cuerpo al blandir; los números (145°, 25°, balanceo) son de prototipo; también levanta el arma durante el tiempo que dure `Alert` aunque no esté cerca del jugador.
- Próximo paso más pequeño: que Jorge lo vea persiguiéndolo y diga si el ángulo y el balanceo convencen; después, captura lateral del guardia persiguiendo.
