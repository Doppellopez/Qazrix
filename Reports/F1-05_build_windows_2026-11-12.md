## Incremento F1-05: build de Windows reproducible y cambio de escena

- **Objetivo/gate:** F1 pide «build Windows» y su gate exige «build en PC ajeno al del autor» (spec §10); spec §8.6 pide una herramienta `BuildWindows` reproducible. Este corte deja el build listo para esa prueba.

- **Cambios de código/escenas/assets:**
  - `Scripts/Editor/BuildWindows.cs`: menú **Qazrix → Build → Windows (desarrollo)** y método `BuildBatch` para ejecutar sin interfaz. Usa las escenas activas de Build Settings, limpia `Builds/Windows`, genera `Qazrix.exe` para Windows de 64 bits en modo desarrollo y escribe `Reports/QA/build-windows-latest.md`.
  - `Scripts/Runtime/Prototype/SceneSwitcher.cs` y acción de entrada **SwitchScene** (F3): pasa a la siguiente escena del build. Sin esto, en un build solo se podría jugar la primera.
  - `PrototypeHud`: línea «Cambiar de escena: F3». Escenas regeneradas (el archivo de acciones se regeneró al añadir la acción).
  - Prueba nueva en Play: F3 carga `Z01_Relieve` desde `Prototype` y en ella hay jugador.

- **Comandos ejecutados** (Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`): `PrototypeSceneBuilder.RebuildBatch`, `Z01GeoBuilder.BuildBatch`, `VerifyProject.RunBatch`, tests EditMode y PlayMode, `BuildWindows.BuildBatch`; después `Builds\Windows\Qazrix.exe -batchmode -nographics` durante 15 s.

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional); EditMode 56/56; PlayMode 20/20 (1 nueva).
  - Build: **Succeeded**, 0 errores, 0 avisos, 155,9 MB, 46 s, dos escenas (`Prototype` y `Z01_Relieve`).
  - Arranque del ejecutable: sigue en marcha a los 15 s y su registro no contiene errores ni excepciones.
  - No ejecutadas: **el build no se ha jugado ni visto en pantalla** (el arranque de prueba fue sin gráficos) y **no se ha probado en otro PC**, que es lo que exige el gate.

- **Build y plataforma:** Windows de 64 bits, modo desarrollo. Salida en `Builds/Windows`, fuera de git.

- **Evidencia:** `Reports/QA/build-windows-latest.md`, `Reports/QA/build.log`, `Reports/QA/player-smoke.log`, `Reports/QA/playmode-results.xml`.

- **Derechos/licencias de activos nuevos:** ninguno. El build contiene solo código propio, materiales de color plano y el relieve derivado del MDT02 (DATA-001, licencia pendiente de ficha): **no distribuirlo públicamente** hasta cerrar esa licencia.

- **Decisiones tomadas:** D41.

- **Problemas conocidos:**
  - Sin menú, sin pantalla de salida y sin opciones: se cierra con Alt+F4.
  - Es un build de desarrollo: más grande y lento que uno final, y con consola de errores.
  - El PC de referencia sigue sin definir (D18): no hay medida de rendimiento.
  - Queda otra carpeta de build, `EXE/`, hecha a mano desde Unity el mismo día; es anterior a este corte y también está fuera de git.

- **Próximo paso más pequeño:** copiar la carpeta `Builds/Windows` entera a otro PC, ejecutar `Qazrix.exe` y recorrer las dos escenas (F3 cambia entre ellas). Con eso y las 20 repeticiones del circuito, el gate F1 queda en condiciones de revisarse.
