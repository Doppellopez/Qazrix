# CLAUDE.md — instrucciones para Claude Code en Qazrix

## Antes de tocar nada
1. Lee `QAZRIX_SPEC_v0.1.md` (al menos §0, §10, §13.2, §14), este archivo, `README.md` y `Design/DecisionLog.md`.
2. Comprueba la versión real del editor en `ProjectSettings/ProjectVersion.txt`. No crees un segundo proyecto.
3. Identifica la fase y el gate actuales (abajo). No empieces la campaña ni modeles Cáceres sin orden de fase.

## Estado
- **Fase:** F0 (verificada en headless sobre copia el 2026-11-10: VerifyProject 0 errores, 20/20 tests EditMode; **falta probar Play a mano** en la escena Z01). F0-B en curso: mapa Z01 jugable (relieve, edificios LiDAR 1 m, calles transitables, ciudadano en 3.ª persona con trepada). Último informe: `Reports/F0B_Z01_incremento_2026-11-10.md`. Dirección de juego: acción-sigilo tipo AC (D27).
- **Editor:** 6000.3.6f1 en `E:\Utilities\Unity\6000.3.6f1\Editor\Unity.exe`. Git 2.55 y Git LFS 3.7.1 presentes; Blender 5.0/5.2 presentes; QGIS 3.44.15 instalado (GDAL/PDAL en `C:\Program Files\QGIS 3.44.15\bin`).
- **Pipeline:** HDRP 17.3.0 provisional (D20, espacio de color Linear; menú Qazrix → Configurar HDRP provisional); D06 abierta hasta benchmark URP vs HDRP.
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
