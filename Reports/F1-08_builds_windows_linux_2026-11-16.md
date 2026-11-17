## Incremento F1-08: builds de Windows y Linux al día

- Objetivo/gate: regenerar los dos builds con lo hecho hoy (petición de Jorge, 2026-11-16): juego que empieza en Z01, teja, empedrado, ventanas y saeteras, animación de salto y caída. Amplía F1-05 y F1-06.
- Cambios de código/escenas/assets: ninguno. Los builds salen del commit `5257224` más `Z01_Relieve.unity` tal como estaba guardada en disco (modificada desde el editor por Jorge, sin commit).
- Comandos ejecutados (Unity 6000.3.6f1, sin interfaz): `BuildWindows.BuildBatch` y `BuildLinux.BuildBatch`, ambos con salida 0.
- Pruebas: `Builds/Windows/Qazrix.exe` arrancado 25 s en ventana: sigue vivo y su registro no tiene líneas de error. No ejecutadas: ver el juego en pantalla ni jugarlo desde el build; **ejecutar el build de Linux** (no hay Linux en esta máquina); tests (sin cambios de código desde 89/89 EditMode y 29/29 PlayMode).
- Build y plataforma:
  - Windows: Succeeded, 0 errores, 168,2 MB, 30 s, `Builds/Windows/Qazrix.exe`.
  - Linux x86_64: Succeeded, 0 errores, 180,3 MB, 38 s, `Builds/Linux/Qazrix.x86_64`.
  - Los dos con `Z01_Relieve` como escena 0 y `Prototype` como escena 1; modo desarrollo. Fuera de git.
- Evidencia: `Reports/QA/build-windows-latest.md`, `build-linux-latest.md`, `build-windows.log`, `build-linux.log`, `player-windows.log`.
- Derechos/licencias de activos nuevos: ninguno. Los builds no se distribuyen públicamente mientras la licencia de DATA-001/002 siga pendiente (D41).
- Decisiones tomadas: ninguna.
- Problemas conocidos: los informes de build listan «Il2CPP» entre las opciones aunque el de Linux es Mono (D46); es la etiqueta de las opciones de compilación de scripts, no el backend. Sin comprobar.
- Próximo paso más pequeño: que Jorge copie `Builds/Windows` (o `Builds/Linux`) entera al otro ordenador y diga si arranca en la ciudad.
