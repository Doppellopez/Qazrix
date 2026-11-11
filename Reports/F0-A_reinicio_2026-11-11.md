## Incremento F0-A (reinicio): arranque técnico rehecho desde cero

- **Objetivo/gate:** Jorge pidió hacer el proyecto desde el principio y eligió reiniciar `Kazrix` en la misma carpeta (D31). Este incremento rehace F0-A (spec §14.2) y avanza el gate F0: proyecto limpio que abre y compila, con escena reproducible.

- **Cambios de código/escenas/assets:**
  - *Retirado* (tras copia verificada): `Assets/_Qazrix/{Art,Scenes,Scripts,Tests}`, `Research/Derived`, `Tools/`, `EXE/`, `Logs/`, `lfs/`, informes antiguos y copias sueltas en `Assets/` (`CLAUDE.md`, `README.md`, `QAZRIX_SPEC_v1.md`, MTN25 2004 duplicado).
  - *Movido:* `MTN25-0704c1-2019-Caceres.{ecw,prj,zip}` de `Assets/` a `Research/Source/CNIG/` (datos fuente, no activos del juego).
  - *Conservado:* spec, `Design/`, `Packages/`, `ProjectSettings/`, `Assets/_Qazrix/Settings/HDRP`, `Assets/HDRPDefaultResources`, `Research/Source`, `Research/*.md`, `ASSET_LICENSES.csv`, `Library/`.
  - *Nuevo, escrito de cero:*
    - `Scripts/Simulation`: `AlertStateMachine` (Patrol → Suspicious → Investigate → Alert → Search → Return, determinista), `GeoOrigin` (traslación en double, D12).
    - `Scripts/Runtime/Prototype`: `PrototypePlayerController` (andar, correr, saltar con margen de borde, agacharse con comprobación de techo, reaparición al caer), `PrototypeOrbitCamera` (órbita, zoom, colisión), `PrototypeHud`, `PrototypeInput`.
    - `Scripts/Editor`: `PrototypeSceneBuilder`, `VerifyProject` (ahora también revisa escenas de build rotas y activos sin fila de licencia), `PrototypePreview`, `ProjectInfo`, `Hdrp/HdrpSetup`.
    - `Tests/EditMode`: 17 tests (11 de alerta, 6 de georreferencia).
    - Escena `Assets/_Qazrix/Scenes/Prototype/Prototype.unity` y 4 materiales grises en `Art/Prototype/`, creados por herramienta de Editor.

- **Comandos ejecutados** (Unity 6000.3.6f1, `-batchmode`, sobre `C:\Users\Propietario\Qazrix`, no sobre una copia):
  - `robocopy` de todo el proyecto salvo `Library` → `C:\Users\Propietario\Kazrix_backup_2026-11-11`
  - `-executeMethod Qazrix.EditorTools.PrototypeSceneBuilder.CreateBatch` → salida 0
  - `-executeMethod Qazrix.EditorTools.VerifyProject.RunBatch` → salida 0
  - `-runTests -testPlatform EditMode` → salida 0
  - `-executeMethod Qazrix.EditorTools.PrototypePreview.CaptureBatch` → salida 0

- **Pruebas:**
  - Aprobadas: compilación sin errores; VerifyProject 0 errores, 1 aviso (pipeline provisional, D06); EditMode 17/17.
  - No ejecutadas: **Play con teclado, ratón y mando** (no se puede en headless). Los valores de velocidad, salto y cámara son de partida y están sin jugar. Sin tests PlayMode todavía.

- **Build y plataforma:** no se ha hecho build. El `EXE/` anterior se retiró (está en la copia).

- **Evidencia:** `Reports/QA/prototype_vista.png` (captura HDRP de la escena), `Reports/QA/verify-project-latest.md`, `Reports/QA/editmode-results.xml`, logs en `Reports/QA/*.log`.

- **Derechos/licencias de activos nuevos:** ninguno externo; los materiales son colores planos propios. En `ASSET_LICENSES.csv` las filas GEN-001 y CC0-001..005 quedan marcadas como retiradas del repositorio; COM-001..003 apuntan solo a las fotos fuente.

- **Decisiones tomadas:** D31 (reinicio). D21–D26 y D28–D30 quedan como referencia a revalidar; D01–D20 y D27 siguen vigentes.

- **Problemas conocidos:**
  - Play sin probar a mano (ver arriba).
  - El cielo de la captura sale oscuro: la escena usa el volumen por defecto de HDRP, sin cielo ni exposición propios. Es graybox; la prueba de material/luz del gate F0 sigue pendiente junto al benchmark D06.
  - El túnel mide 1,3 m de alto y el jugador agachado 1,0 m: holgura por confirmar jugando.
  - La cámara ignora por completo el obstáculo si el primer impacto es el propio jugador (solo posible agachado); revisar al jugar.
  - `MTN25` 2004/2019 y otros ficheros de `Research/Source/CNIG` no tienen fila propia en `ASSET_LICENSES.csv` (no entran en el juego, pero conviene registrarlos).
  - `.gitattributes` conserva reglas LFS para las mallas `Z01_*` que ya no existen; inofensivas, se revisan en F0-B.
  - Repositorio aún sin commits; `Research/Source` pesa ≈1,4 GB (D30 sin decidir).
  - Pendientes heredados de F0 sin tocar: carta al CNIG/SIG sin enviar, texto de licencia de DATA-001/002, PC de referencia (D18), benchmark URP vs HDRP (D06).

- **Próximo paso más pequeño:** que Jorge abra la escena `Prototype`, pulse Play y recorra la lista «Qué probar» del README. Con eso confirmado, empezar F0-B: recorte del MDT02 alrededor de la Plaza Mayor → relieve en Unity con tres puntos de control medidos.

## Añadido el mismo día: ajuste al paquete original (D32)

- **Cambios:** HDRP → **URP 17.3.0** (`Packages/manifest.json`; retirados `Assets/HDRPDefaultResources`, `Settings/HDRP` y `Editor/Hdrp`; nuevos `Editor/Urp/UrpSetup` y `Settings/URP`); spec sustituida por `QAZRIX_SPEC_v1.md` (idéntica salvo el número de versión); carpeta renombrada a `C:\Users\Propietario\Qazrix`; `projectName`, `metroPackageName` y `metroApplicationDescription` de `ProjectSettings.asset` pasan de Kazrix a Qazrix; escena `Prototype` y materiales regenerados con el shader de URP (`RebuildBatch`).
- **Pruebas tras el cambio (headless, proyecto real):** UrpSetup, RebuildBatch, VerifyProject (0 errores, 1 aviso de pipeline provisional), EditMode 17/17 y captura, todos con salida 0. Play sigue sin probar a mano.
- **Sustituye en este informe:** donde arriba dice HDRP, cielo oscuro de HDRP o `QAZRIX_SPEC_v0.1.md`, vale ya URP y `QAZRIX_SPEC_v1.md`.
- **Problemas conocidos nuevos:** quedan `ProjectSettings/HDRPProjectSettings.asset` (inofensivo) y la caché de HDRP en `Library`; Unity Hub no tiene el proyecto en su lista: hay que añadirlo desde `C:\Users\Propietario\Qazrix`.
