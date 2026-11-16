## Incremento F1-07: el juego empieza en Z01

- Objetivo/gate: que el juego compilado arranque en la ciudad (`Z01_Relieve`) y no en `Prototype` (petición de Jorge, 2026-11-16). Amplía F1-05.
- Cambios de código/escenas/assets:
  - `Assets/_Qazrix/Scripts/Editor/BuildSceneOrder.cs`: fija el orden del build, `Z01_Relieve` primero y `Prototype` después (menú Qazrix → Build → Empezar el juego en Z01; batch `BuildSceneOrder.ApplyBatch`).
  - `PrototypeSceneBuilder` y `Z01GeoBuilder` usan ese orden al regenerar sus escenas (antes `Prototype` volvía a ponerse la primera).
  - `ProjectSettings/EditorBuildSettings.asset`: orden aplicado con la herramienta.
  - `PrototypeSceneTests.F3_SwitchesToTheNextScene`: ahora parte de Z01 y espera `Prototype`.
- Comandos ejecutados (Unity 6000.3.6f1, sin interfaz): `BuildSceneOrder.ApplyBatch` (salida 0), tests EditMode, tests PlayMode, `VerifyProject.RunBatch` (salida 0).
- Pruebas: aprobadas 79/79 EditMode y 28/28 PlayMode; `VerifyProject` salida 0. El build de Windows regenerado arranca y se mantiene 25 s sin errores en su registro. No ejecutadas: **ver en pantalla** que abre en Z01 (el registro del juego no nombra la escena; consta solo por la lista de escenas del build); F3 desde `Prototype` de vuelta a Z01 (ver problemas conocidos).
- Build y plataforma: `Builds/Windows` regenerado (`BuildWindows.BuildBatch`, Succeeded, 0 errores, 164,7 MB, 36 s) con `Z01_Relieve` como escena 0 y `Prototype` como escena 1; lleva también las texturas de muros, peldaños y tejados. `Builds/Linux` sigue siendo el anterior: arranca en `Prototype` y sin texturas.
- Evidencia: `Reports/QA/scene-order.log`, `Reports/QA/playmode-results.xml`, `Reports/QA/build-windows-latest.md`, `Reports/QA/player-windows.log`.
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D50.
- Problemas conocidos:
  - Con el orden nuevo, las dos primeras tandas PlayMode cerraron Unity con una violación de acceso (0xC0000005) en `Space_MakesThePlayerJump`. Causa: en las pruebas, el primer puesto del build lo ocupa la escena del lanzador de tests; el test de F3 partía de `Prototype`, que ahora es la última, y al dar la vuelta recargaba esa escena. Comprobado devolviendo el orden antiguo (28/28) y, tras cambiar el test, con el orden nuevo (28/28). No afecta al juego compilado, donde ese puesto es Z01, pero esa vuelta queda sin prueba automática.
  - Al pulsar Play en el editor se sigue jugando la escena abierta, no la primera del build.
- Próximo paso más pequeño: que Jorge abra `Builds/Windows/Qazrix.exe`, confirme que aparece en la ciudad y pruebe F3 de ida y vuelta; después, regenerar el build de Linux.
