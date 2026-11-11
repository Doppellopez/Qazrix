# CLAUDE.md — instrucciones para Claude Code en Qazrix

## Antes de tocar nada
1. Lee `QAZRIX_SPEC_v1.md` (al menos §0, §10, §13.2, §14), este archivo, `README.md` y `Design/DecisionLog.md`.
2. Comprueba la versión real del editor en `ProjectSettings/ProjectVersion.txt`. No crees un segundo proyecto.
3. Identifica la fase y el gate actuales (abajo). No empieces la campaña ni modeles Cáceres sin orden de fase.

## Estado
- **Fase:** F0, con F0-A y F0-B rehechas tras el **reinicio del 2026-11-11 (D31)**. F0-A: escena `Prototype`, `VerifyProject`, tests. F0-B (D33, D34): relieve real de Z01, tres volúmenes y tres puntos de control sacados del LiDAR e identificados por Jorge. Verificado en headless sobre el proyecto real: VerifyProject 0 errores, 23/23 tests EditMode, puntos de control en tolerancia. Play probado a mano por Jorge el 2026-11-11 (comprobación visual general, antes del cambio de puntos de control). Pendiente del gate F0: benchmark URP vs HDRP (D06), PC de referencia (D18), carta al CNIG/SIG, licencias de DATA-001..003. Git: historial en `main` con LFS (D35), subido a `origin` el 2026-11-11; no hacer push ni borrar la rama `respaldo-initial-checkin` sin que Jorge lo pida. F1 empezada: F1-01 Input Actions con reasignación de teclado (D36), 31/31 tests EditMode y 4/4 PlayMode con teclado simulado (W, Espacio, F1, F2); reasignar una tecla a mano y el mando siguen sin probar. Siguiente corte de F1: parkour contextual mínimo o guardia con patrulla. Carpeta del proyecto: `C:\Users\Propietario\Qazrix`. Último informe: `Reports/F1-01_input_actions_2026-11-11.md`. Dirección de juego: acción-sigilo tipo AC (D27).
- **Copia previa al reinicio:** `C:\Users\Propietario\Kazrix_backup_2026-11-11` (mapa Z01, ciudadano, trepada, informes). Es referencia, no fuente: no copiar código de ahí sin que Jorge lo pida.
- **Editor:** 6000.3.6f1 en `E:\Utilities\Unity\6000.3.6f1\Editor\Unity.exe`. Git 2.55 y Git LFS 3.7.1 presentes; Blender 5.0/5.2 presentes; QGIS 3.44.15 instalado (GDAL/PDAL en `C:\Program Files\QGIS 3.44.15\bin`).
- **Pipeline:** URP 17.3.0 provisional (D32, vuelve al paquete original y sustituye al HDRP de D20; menú Qazrix → Configurar URP provisional); D06 abierta hasta benchmark URP vs HDRP.
- **Hardware objetivo:** sin definir (pregunta abierta §17).

## Reglas
- Una tarea o corte vertical pequeño por iteración; el repositorio siempre compilable; sin refactors ajenos.
- Idioma: español en documentación y comentarios; inglés en clases, métodos, interfaces y rutas.
- Lógica pura en `Scripts/Simulation` (asmdef sin referencias a Unity) con tests EditMode. Vista Unity aparte.
- `ScriptableObject` para definiciones, nunca como estado de partida mutable.
- Sin `FindObjectOfType` disperso, sin mega-managers, sin service locator global sin motivo documentado.
- Escenas, prefabs y materiales: crearlos con herramientas de Editor. **Prohibido** escribir YAML de `.unity`/`.prefab` a mano o inventar GUIDs.
- Assets: ninguno sin fila en `Research/ASSET_LICENSES.csv`. Nada de scraping de visores SIG, Google Maps/Street View ni modelos comerciales.
- No instalar software, paquetes de pago ni dependencias externas sin permiso explícito.
- Decisiones estructurales o de alcance → una línea en `Design/DecisionLog.md`.
- Decisión irreversible (diseño, licencia, coste, recurso externo): explica alternativas y para **esa** decisión; sigue con lo demás.

## Verificación
- Con Unity disponible: ejecuta `VerifyProject.RunBatch` y los tests EditMode (comandos en README) y adjunta el resumen.
- Sin Unity: marca el trabajo como **«generado, pendiente de verificación en Editor»**. Nunca digas «funciona».

## Informe de cada entrega (spec §14.1)
```markdown
## Incremento <ID>: <título>
- Objetivo/gate:
- Cambios de código/escenas/assets:
- Comandos ejecutados:
- Pruebas: aprobadas / fallidas / no ejecutadas (por qué):
- Build y plataforma:
- Evidencia: capturas, logs y recorridos:
- Derechos/licencias de activos nuevos:
- Decisiones tomadas:
- Problemas conocidos:
- Próximo paso más pequeño:
```
Guarda cada informe en `Reports/`.
