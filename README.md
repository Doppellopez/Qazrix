# QAZRIX — La llave de la Mansaborá

Aventura de sigilo y acción en tercera persona ambientada en Cáceres (Qazrix), 1229, basada en la leyenda de la Mansaborá.
Especificación completa: [`QAZRIX_SPEC_v0.1.md`](QAZRIX_SPEC_v0.1.md).

**Versión:** 0.1 · **Fase actual:** F0-A (arranque técnico e inventario). Nada de modelado de Cáceres todavía.
**Estado del código:** generado fuera de Unity → **pendiente de verificación en Editor**.

---

## Montarlo en casa (primera vez)

### 1. Programas

| Programa | Para qué | ¿Imprescindible ahora? |
|---|---|---|
| Unity Hub + **Unity 6.3 LTS** (6000.3.x) con módulo *Windows Build Support (IL2CPP)* | motor | Sí |
| **Git** y **Git LFS** | control de versiones | Muy recomendable |
| Visual Studio 2022 Community o Rider | editar C# | Sí (lo ofrece el propio Hub) |
| Blender, QGIS | modelado y GIS | No (a partir de F0-B / F2) |

Anota la versión exacta de parche que instales (p. ej. `6000.3.4f1`) en `Design/DecisionLog.md`: a partir de ahí queda fijada.

### 2. Crear el proyecto Unity

1. Unity Hub → **New project** → editor 6000.3.x → plantilla **Universal 3D** (URP).
   URP es provisional: ver `Design/Decisions/D06_Pipeline_URP_vs_HDRP.md`.
2. Nombre: `Qazrix`. Ubicación: la carpeta donde quieras el repositorio.
3. Ábrelo una vez y ciérralo.

### 3. Copiar este paquete dentro

Descomprime el zip y copia **todo su contenido** en la carpeta del proyecto, de forma que queden juntas:

```text
Qazrix/
├── Assets/            ← aquí se añade Assets/_Qazrix
├── Packages/          (lo creó Unity)
├── ProjectSettings/   (lo creó Unity)
├── Design/  Research/  Reports/
├── README.md  CLAUDE.md  QAZRIX_SPEC_v0.1.md
└── .gitignore  .gitattributes
```

> Si Windows oculta `.gitignore` y `.gitattributes`, activa *Ver → Elementos ocultos* para comprobar que se han copiado.

### 4. Comprobar los ajustes del proyecto

- **Edit → Project Settings → Player → Active Input Handling:** `Input System Package (New)` o `Both`.
  Si Unity pide reiniciar, acepta. (Con la plantilla Universal 3D suele venir ya así).
- **Project Settings → Editor → Asset Serialization:** `Force Text`.
- **Project Settings → Version Control → Mode:** `Visible Meta Files`.

### 5. Crear la escena y verificar

1. Menú **Qazrix → Prototipo → Crear o abrir escena Prototype**.
2. Menú **Qazrix → Aplicar versión y nombre del producto** (deja el juego en v0.1; también se aplica al crear la escena).
3. Menú **Qazrix → Verificar proyecto**. Objetivo: **0 errores**. Los avisos de render pipeline son normales mientras D06 siga abierta.
4. **Window → General → Test Runner → EditMode → Run All.** Deben pasar 10 tests.
5. Pulsa **Play**. Haz clic en la ventana Game para capturar el ratón.

| Acción | Teclado / ratón | Mando |
|---|---|---|
| Mover | WASD / flechas | stick izquierdo |
| Correr | Mayús izq. | L3 o gatillo derecho |
| Saltar | Espacio | A / Cruz |
| Agacharse | C o Ctrl | B / Círculo |
| Cámara | ratón (Esc suelta el cursor) | stick derecho |
| Zoom | rueda | cruceta arriba/abajo |
| Ocultar panel | F1 | Select |

Qué probar: saltar el obstáculo bajo, subir el escalón y la plataforma, pasar agachado bajo el túnel (no debes poder levantarte dentro), subir la rampa y pegar la cámara a la pared alta (no debe atravesarla).

### 6. Git (opcional pero recomendado)

```bash
git init
git lfs install
git add .
git commit -m "F0-A: arranque técnico"
```

Para fusionar escenas sin romperlas, configura *UnityYAMLMerge* (viene con el editor) como herramienta de merge `unityyamlmerge` en tu `.gitconfig`.

---

## Ejecución sin interfaz (headless)

Sustituye la ruta del editor por la tuya (Unity Hub → Installs → icono de engranaje → Show in Explorer). En PowerShell:

```powershell
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.X\Editor\Unity.exe"
$Proj  = "C:\ruta\a\Qazrix"

# Crear la escena si falta (no toca una existente)
& $Unity -batchmode -projectPath $Proj -executeMethod Qazrix.EditorTools.PrototypeSceneBuilder.CreateBatch -logFile Reports\QA\scene.log

# Verificar (código de salida 0 = sin errores)
& $Unity -batchmode -projectPath $Proj -executeMethod Qazrix.EditorTools.VerifyProject.RunBatch -logFile Reports\QA\verify.log
echo $LASTEXITCODE

# Tests EditMode (resultados en XML NUnit)
& $Unity -batchmode -projectPath $Proj -runTests -testPlatform EditMode -testResults Reports\QA\editmode-results.xml -logFile Reports\QA\tests.log
```

No añadas `-quit` a estas tres órdenes: los métodos cierran Unity con su propio código de salida y `-runTests` también. Unity no debe estar abierto con el mismo proyecto mientras corren. Comprueba los argumentos en el manual de tu versión exacta (fuente R06 de la spec).

---

## Qué hay en el código

| Ruta | Contenido |
|---|---|
| `Scripts/Simulation/` | Lógica pura sin Unity (`noEngineReferences`): máquina de alerta de guardias y origen geográfico local |
| `Scripts/Runtime/Prototype/` | Controlador del jugador, cámara orbital con colisión, HUD de depuración, entrada temporal |
| `Scripts/Runtime/World/` | Puente entre coordenadas double y `Vector3` |
| `Scripts/Editor/` | `PrototypeSceneBuilder` y `VerifyProject` |
| `Tests/EditMode/` | 10 tests de la lógica pura |

Documentación de trabajo: `Design/DecisionLog.md`, `Research/Datasets.md`, `Research/ASSET_LICENSES.csv`, informes en `Reports/`.

Qazrix está inspirado en una leyenda cacereña y recrea libremente la ciudad de 1229 usando como referencia su fisonomía actual. Algunos edificios, acontecimientos y personajes se han adaptado por razones artísticas.
