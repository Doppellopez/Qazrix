## Incremento F1-04: interacción y checkpoint

- **Objetivo/gate:** cuarto corte de F1 (spec §10: «sistema de interacción, HUD simple, checkpoint»), según spec §6.4 (interacciones con una base común) y §6.5 (reinicio por checkpoints).

- **Cambios de código/escenas/assets:**
  - `Scripts/Simulation/Gameplay/InteractionRules.cs` (lógica pura): elige el objeto más cercano dentro de 1,8 m y más o menos delante; pegado a él (0,6 m) la mirada no cuenta.
  - `Scripts/Runtime/Gameplay/Interactable.cs`: base común con texto de acción y `Interact`.
  - `Scripts/Runtime/Gameplay/PlayerInteractor.cs`: mide los candidatos alrededor del jugador y activa el elegido con la tecla de interactuar; cuenta los objetos recogidos.
  - `Scripts/Runtime/Gameplay/LetterPickup.cs`: carta que se recoge una sola vez.
  - `Scripts/Runtime/Gameplay/CheckpointZone.cs`: zona que, al entrar, pasa a ser el punto de reaparición.
  - `PrototypePlayerController`: `SetCheckpoint` y `CheckpointLabel`; caer fuera del mundo o ser alcanzado por el guardia devuelve al último checkpoint.
  - Entrada: acción nueva **Interact** (E en teclado, X / Cuadrado en mando), reasignable desde el panel F2. `InputActionsBuilder.Ensure` regenera el archivo de acciones cuando le falta alguna que el código lee.
  - `PrototypeHud`: líneas «Reaparición: … · cartas: N» y «E / X: recoger la carta» cuando hay algo a mano.
  - Escenas regeneradas. `Prototype`: carta sobre el mirador y checkpoint «salida del túnel». `Z01_Relieve`: carta sobre la plataforma y checkpoint «tras el murete».

- **Comandos ejecutados** (Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`): `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoBuilder.BuildBatch`, `VerifyProject.RunBatch`, `-runTests -testPlatform EditMode` (con `-batchmode`) y `-runTests -testPlatform PlayMode` (con ventana). Todos con salida 0 a la primera.

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional); EditMode 56/56 (5 nuevos); PlayMode 19/19 (2 nuevos).
  - Pruebas en Play nuevas (escena `Prototype`): junto a la carta se ofrece recogerla, E la recoge, deja de verse y no se puede recoger dos veces; al entrar en la zona se fija el checkpoint y `Respawn` devuelve allí y no al inicio.
  - No ejecutadas: **Play a mano**; la carta y el checkpoint de `Z01_Relieve` no tienen prueba propia; sin probar con mando; no se ha probado que ser alcanzado por el guardia devuelva al checkpoint (sí que `Respawn` lo hace, y la captura llama a `Respawn`).

- **Build y plataforma:** sin build.

- **Evidencia:** `Reports/QA/playmode-results.xml`, `Reports/QA/editmode-results.xml`, `Reports/QA/verify-project-latest.md`.

- **Derechos/licencias de activos nuevos:** ninguno.

- **Decisiones tomadas:** D40.

- **Problemas conocidos:**
  - El checkpoint y la carta recogida **no se guardan**: al salir de Play o cerrar el juego se pierden. El guardado versionado en disco (§6.5) es un corte aparte.
  - El checkpoint es invisible: solo se nota en la línea «Reaparición» del recuadro.
  - Solo hay un tipo de objeto interactuable (la carta). Puertas, palancas, escondites y conversaciones usarán la misma base, pero no existen.
  - Recoger la carta no tiene consecuencia todavía: no hay misión ni objetivo que la use.
  - Al regenerarse el archivo de acciones cambian sus identificadores, así que las teclas que hubieras reasignado con F2 vuelven a las de fábrica.
  - La zona de checkpoint no se activa si se atraviesa durante un salto de obstáculo o una trepada (el cuerpo no usa la física en ese momento).

- **Próximo paso más pequeño:** que Jorge pruebe en `Z01_Relieve`: saltar el murete (cambia «Reaparición» a «tras el murete»), trepar a la plataforma y recoger la carta con E. Para cerrar F1 quedan el build de Windows probado en otro PC y las 20 repeticiones de QA del circuito, que son trabajo manual.
