## Incremento F1-01: Input Actions con reasignación de teclas

- **Objetivo/gate:** primer corte de F1 (spec §10). Sustituir la lectura directa de dispositivos (D13, temporal) por Input Actions con remapeo (spec §6.7 y §8.1), sin cambiar cómo se juega.

- **Cambios de código/escenas/assets:**
  - `Scripts/Runtime/Input/QazrixControls.cs`: definición de fábrica de las 11 acciones (mover, cámara con ratón y con stick, zoom, correr, agacharse, saltar, panel, controles, capturar y soltar cursor) en dos esquemas, teclado/ratón y mando.
  - `Scripts/Runtime/Input/QazrixInput.cs`: único lector de entrada. Carga y guarda las reasignaciones en PlayerPrefs (`qazrix.input.overrides.v1`) y bloquea el juego mientras hay un panel abierto.
  - `Scripts/Runtime/Input/ControlsPanel.cs`: panel de controles (F2 o Start). Lista mover, correr, agacharse y saltar; clic en una tecla y se pulsa la nueva; «Restablecer» vuelve a las de fábrica. Solo teclado en este corte.
  - `Scripts/Editor/InputActionsBuilder.cs`: genera `Settings/Input/QazrixControls.inputactions` desde la definición de fábrica; no pisa un archivo existente salvo desde el menú Qazrix → Entrada.
  - `PrototypePlayerController`, `PrototypeOrbitCamera` y `PrototypeHud` reciben el lector por referencia; se elimina `PrototypeInput.cs`.
  - `PrototypeSceneBuilder` y `Z01GeoBuilder` crean el objeto «Entrada»; ambas escenas regeneradas.
  - `VerifyProject` comprueba que existe el archivo de acciones y que la escena tiene el lector con las acciones asignadas.
  - `Qazrix.Editor` y `Qazrix.Tests.EditMode` referencian `Unity.InputSystem`.

- **Comandos ejecutados** (Unity 6000.3.6f1 `-batchmode` sobre `C:\Users\Propietario\Qazrix`): `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoBuilder.BuildBatch`, `Z01GeoReport.RunBatch`, `VerifyProject.RunBatch`, `-runTests -testPlatform EditMode`. Todos con salida 0 en la tanda final.

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional); EditMode 31/31 (8 nuevos de entrada); puntos de control de Z01 siguen en tolerancia.
  - Fallidas por el camino y corregidas: la primera tanda no compiló (faltaba la referencia a `Unity.InputSystem` en `Qazrix.Editor`); un test de reasignación fallaba porque comparaba dos definiciones de fábrica distintas (ver «Problemas conocidos»).
  - No ejecutadas: **Play a mano**. Nada de lo que depende de pulsar teclas está probado: movimiento con las acciones nuevas, panel F2, captura de una tecla nueva, guardado entre sesiones, mando.

- **Build y plataforma:** sin build.

- **Evidencia:** `Reports/QA/verify-project-latest.md`, `Reports/QA/editmode-results.xml`, `Reports/QA/z01_control_points.md`, logs en `Reports/QA/`.

- **Derechos/licencias de activos nuevos:** ninguno.

- **Decisiones tomadas:** D36 (cierra D13).

- **Problemas conocidos:**
  - Las reasignaciones se guardan por identificador de asignación, y ese identificador vive en el archivo `.inputactions`. Si se regenera el archivo desde el menú, los identificadores cambian y las teclas guardadas dejan de aplicarse (vuelven las de fábrica sin error).
  - El panel solo reasigna teclado; el mando usa siempre las asignaciones de fábrica.
  - El panel de depuración (F1) muestra los controles de fábrica, no los reasignados.
  - No se impide asignar la misma tecla a dos acciones.
  - Sin ajustes de sensibilidad ni inversión de ejes en pantalla todavía (§6.7); existen como campos de la cámara.
  - PlayerPrefs es provisional: cuando exista el guardado versionado (§6.5), las opciones deben pasar a él.
  - Unity reescribe `projectName: Kazrix` en `ProjectSettings.asset` porque el proyecto está enlazado a Unity Cloud con ese nombre; hay que renombrarlo allí.

- **Próximo paso más pequeño:** que Jorge pruebe en Play: mover, saltar y agacharse como antes; F2, cambiar «Saltar» a otra tecla, cerrar, comprobar que salta con la nueva; parar y volver a Play para ver que se conserva. Después, siguiente corte de F1: parkour contextual mínimo (vault en el obstáculo bajo) o el guardia con patrulla y percepción.

## Añadido: pruebas en Play con teclado simulado (2026-11-11)

- **Motivo:** Jorge informó de que F2 no funcionaba y de que tampoco veía el recuadro de depuración ni respondía F1.
- **Qué se hizo:** nuevo ensamblado `Tests/PlayMode` con `PrototypeSceneTests` (4 pruebas sobre la escena `Prototype` con `InputTestFixture`): W avanza, Espacio salta, F2 abre y cierra el panel y bloquea el movimiento, F1 oculta y muestra el recuadro. Guardan capturas reales del juego. `PrototypeHud` expone `IsVisible`.
- **Comando:** Unity 6000.3.6f1 con ventana (sin `-batchmode`, las capturas la necesitan) `-runTests -testPlatform PlayMode` → salida 0.
- **Resultado:** PlayMode 4/4; EditMode 31/31; VerifyProject 0 errores. Capturas `Reports/QA/play_prototype_hud.png` y `play_prototype_controles.png`: el recuadro y el panel de controles se dibujan y responden.
- **Conclusión:** el fallo no se reproduce con el código y la escena del proyecto. Causa en el equipo de Jorge **sin determinar**; hipótesis principal: estaba mirando la pestaña Scene (que no dibuja estos paneles ni recibe el teclado del juego) en lugar de la pestaña Game.
- **Sigue sin probar a mano:** capturar una tecla nueva con el ratón, guardado entre sesiones y mando. La escena `Z01_Relieve` no tiene pruebas en Play.

## Añadido: comprobación de Jorge en Play (2026-11-11)

Jorge envió una captura de `Z01_Relieve` en la pestaña Game con el recuadro de depuración visible y, tras repetir los pasos (clic en el juego, F1, F2), respondió «correcto». Se toma como que F1 y F2 responden en su equipo; no precisó si necesitó la tecla Fn ni cuál era la causa del fallo inicial, que queda sin determinar. Reasignar una tecla, el guardado entre sesiones y el mando siguen sin confirmar a mano.
