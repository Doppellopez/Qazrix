# QAZRIX — La llave de la Mansaborá

Aventura de sigilo y acción en tercera persona ambientada en Cáceres (Qazrix), 1229, basada en la leyenda de la Mansaborá.
Especificación completa: [`QAZRIX_SPEC_v1.md`](QAZRIX_SPEC_v1.md).

**Versión:** 0.1 · **Fase actual:** F0-A rehecha tras el reinicio del 2026-11-11 (D31), con **URP provisional** como en el paquete original (D32). F0-B hecha: relieve real de Z01 con tres puntos de control (D33); aún sin edificios.
**Estado del código:** compila en Unity 6000.3.6f1, `VerifyProject` 0 errores y 56/56 tests EditMode, 20/20 pruebas en Play y build de Windows correcto. Play probado a mano por Jorge el 2026-11-11 (comprobación visual general).

---

## Abrir y probar

1. Unity Hub → abrir esta carpeta con **Unity 6000.3.6f1** (D05).
2. Menú **Qazrix → Prototipo → Crear o abrir escena Prototype**.
3. Menú **Qazrix → Verificar proyecto**. Objetivo: **0 errores**. El aviso de render pipeline es normal mientras D06 siga abierta.
4. **Window → General → Test Runner → EditMode → Run All.** Deben pasar 56 tests.
5. Pulsa **Play** y haz clic en la ventana Game para capturar el ratón.

| Acción | Teclado / ratón | Mando |
|---|---|---|
| Mover | WASD / flechas | stick izquierdo |
| Correr | Mayús izq. | L3 o gatillo derecho |
| Saltar | Espacio | A / Cruz |
| Agacharse | C o Ctrl | B / Círculo |
| Interactuar | E | X / Cuadrado |
| Cámara | ratón (clic captura, Esc suelta) | stick derecho |
| Zoom | rueda | cruceta arriba/abajo |
| Ocultar panel | F1 | Select |
| Cambiar teclas | F2 | Start |
| Cambiar de escena | F3 | — |

Qué probar en el circuito:

- delante del obstáculo bajo aparece «Espacio / A: saltar el obstáculo»: púlsalo y pasa por encima; el escalón se sube andando;
- delante de la plataforma naranja (1 m) aparece «Espacio / A: trepar»: púlsalo y sube;
- pasar agachado por el túnel: dentro no debes poder levantarte;
- subir la rampa hasta el mirador;
- pegar la cámara a la pared alta: no debe atravesarla;
- tirarte fuera del suelo: debes reaparecer en el punto de salida;
- el guardia (cápsula granate) rodea el circuito: déjate ver, escóndete tras la pared alta, pasa agachado por detrás y corre cerca de él; la bola sobre su cabeza y la línea «Guardia» del recuadro dicen qué está haciendo;
- sube al mirador por la rampa y recoge la carta con E; pasa por la salida del túnel y verás «Reaparición: salida del túnel» en el recuadro: desde entonces, si el guardia te alcanza vuelves ahí;
- pulsar F2, cambiar «Saltar» a otra tecla, cerrar y comprobar que salta con la nueva; parar, volver a Play y ver que se conserva.

## Mapa Z01 (F0-B)

Relieve real de 1024 × 1024 m alrededor de la Plaza Mayor (MDT02 del CNIG), con un poste y un volumen simple en cada punto de control.

1. Menú **Qazrix → Mapa → Crear escena Z01 (relieve CNIG)**. Se puede repetir: todo se regenera desde `Research/Derived/Z01`.
2. Menú **Qazrix → Mapa → Informe de puntos de control Z01** → `Reports/QA/z01_control_points.md`.
3. Play: el jugador del prototipo nace en el origen local y puede andar por el relieve. Hay una zona de juego de prueba: un murete para saltar y una plataforma para trepar a 6 m al norte, y un guardia que rodea el punto de salida.

Para regenerar los datos derivados desde las fuentes del CNIG (necesita el GDAL de QGIS 3.44; no descarga nada):

```powershell
powershell -ExecutionPolicy Bypass -File Tools\GIS\make_z01.ps1
```

En headless: `Qazrix.EditorTools.Z01GeoBuilder.BuildBatch`, `Qazrix.EditorTools.Z01GeoReport.RunBatch` y `Qazrix.EditorTools.PrototypePreview.CaptureZ01Batch`, igual que las órdenes de abajo.

Los tres puntos de control (Torre de Bujaco, Concatedral de Santa María y San Francisco Javier) salen del LiDAR PNOA 2018 y los identificó Jorge sobre el plano `Reports/QA/z01_candidatos_lidar.png` (D34). Para añadir más: `Tools/GIS/gcp_candidates_lidar.py` lista los candidatos y se apuntan en `Research/Survey/gcp_input.csv`. Ambos scripts de Python se ejecutan con `python-qgis-ltr.bat` de QGIS.

## Build de Windows

Menú **Qazrix → Build → Windows (desarrollo)**. Genera `Builds/Windows/Qazrix.exe` con las dos escenas y deja el informe en `Reports/QA/build-windows-latest.md`. Para probarlo en otro PC, copia la carpeta `Builds/Windows` entera. Dentro del juego, F3 cambia de escena y Alt+F4 cierra. En headless: `Qazrix.EditorTools.BuildWindows.BuildBatch`.

## Ejecución sin interfaz (headless)

Unity no debe tener abierto el proyecto mientras corren. En PowerShell:

```powershell
$Unity = "E:\Utilities\Unity\6000.3.6f1\Editor\Unity.exe"
$Proj  = "C:\Users\Propietario\Qazrix"

# Crear la escena si falta (no toca una existente)
& $Unity -batchmode -projectPath $Proj -executeMethod Qazrix.EditorTools.PrototypeSceneBuilder.CreateBatch -logFile "$Proj\Reports\QA\scene.log" | Out-Null

# Verificar (código de salida 0 = sin errores); informe en Reports/QA/verify-project-latest.md
& $Unity -batchmode -projectPath $Proj -executeMethod Qazrix.EditorTools.VerifyProject.RunBatch -logFile "$Proj\Reports\QA\verify.log" | Out-Null
echo $LASTEXITCODE

# Tests EditMode (resultados en XML NUnit)
& $Unity -batchmode -projectPath $Proj -runTests -testPlatform EditMode -testResults "$Proj\Reports\QA\editmode-results.xml" -logFile "$Proj\Reports\QA\tests.log" | Out-Null

# Captura de la escena a Reports/QA/prototype_vista.png (necesita GPU: sin -nographics)
& $Unity -batchmode -projectPath $Proj -executeMethod Qazrix.EditorTools.PrototypePreview.CaptureBatch -logFile "$Proj\Reports\QA\preview.log" | Out-Null
```

Pruebas en Play (abre una ventana de Unity durante medio minuto; sin `-batchmode` porque las capturas la necesitan):

```powershell
& $Unity -projectPath $Proj -runTests -testPlatform PlayMode -testResults "$Proj\Reports\QA\playmode-results.xml" -logFile "$Proj\Reports\QA\playmode.log" | Out-Null
```

No añadas `-quit`: los métodos cierran Unity con su propio código de salida y `-runTests` también.

## Qué hay en el código (`Assets/_Qazrix`)

| Ruta | Contenido |
|---|---|
| `Scripts/Simulation/` | Lógica pura sin Unity (`noEngineReferences`): máquina de alerta de guardias, cono de visión y reglas de ruido, origen geográfico local y rejilla de cotas |
| `Scripts/Runtime/Gameplay/` | Interacción (base `Interactable`, carta) y zonas de checkpoint (D40) |
| `Scripts/Runtime/AI/` | Guardia con patrulla, vista y oído (D37) que rodea obstáculos con la malla de navegación (D38), y contrato `StealthTarget` |
| `Scripts/Runtime/Prototype/` | Controlador del jugador, cámara orbital con colisión, panel de depuración |
| `Scripts/Runtime/Input/` | Input Actions de fábrica, lector único de entrada y panel de reasignación de teclas (D36) |
| `Scripts/Runtime/World/` | Puente entre coordenadas `double` y `Vector3` |
| `Scripts/Editor/` | `PrototypeSceneBuilder`, `VerifyProject`, `PrototypePreview`, `ProjectInfo`, `Z01GeoBuilder`, `Z01GeoReport`; `Urp/UrpSetup` (solo compila con URP instalado) |
| `Tests/EditMode/` | 56 tests: lógica pura (alerta, visión, ruido, parkour, interacción, georreferencia) y acciones de entrada |
| `Tests/PlayMode/` | 20 pruebas en Play con teclado simulado: 17 en Prototype (movimiento, paneles, guardia, parkour, interacción, checkpoint y cambio de escena) y 3 en Z01_Relieve; dejan capturas en `Reports/QA` |
| `Settings/URP/` | Assets del pipeline URP provisional (D06, D32) |

Fuera de `Assets`: `Design/DecisionLog.md`, `Research/Datasets.md`, `Research/ASSET_LICENSES.csv`, datos fuente en `Research/Source/` e informes en `Reports/`.

## Git

Repositorio con Git LFS (`git lfs install` una vez por máquina). Los datos fuente del CNIG de `Research/Source` (≈1,4 GB), las imágenes y los derivados binarios van por LFS (D35). El remoto `origin` apunta a GitHub; `main` se subió el 2026-11-11.

---

Qazrix está inspirado en una leyenda cacereña y recrea libremente la ciudad de 1229 usando como referencia su fisonomía actual. Algunos edificios, acontecimientos y personajes se han adaptado por razones artísticas.
