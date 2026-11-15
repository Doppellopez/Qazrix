# CLAUDE.md — instrucciones para Claude Code en Qazrix

## Antes de tocar nada
1. Lee `QAZRIX_SPEC_v1.md` (al menos §0, §10, §13.2, §14), este archivo, `README.md` y `Design/DecisionLog.md`.
2. Comprueba la versión real del editor en `ProjectSettings/ProjectVersion.txt`. No crees un segundo proyecto.
3. Identifica la fase y el gate actuales (abajo). No empieces la campaña ni modeles Cáceres sin orden de fase.

## Estado
- **Fase:** F0, con F0-A y F0-B rehechas tras el **reinicio del 2026-11-11 (D31)**. F0-A: escena `Prototype`, `VerifyProject`, tests. F0-B (D33, D34): relieve real de Z01, tres volúmenes y tres puntos de control sacados del LiDAR e identificados por Jorge. Verificado en headless sobre el proyecto real: VerifyProject 0 errores, 23/23 tests EditMode, puntos de control en tolerancia. Play probado a mano por Jorge el 2026-11-11 (comprobación visual general, antes del cambio de puntos de control). Pendiente del gate F0: benchmark URP vs HDRP (D06), PC de referencia (D18), carta al CNIG/SIG, licencias de DATA-001..003. Git: historial en `main` con LFS (D35), subido a `origin` el 2026-11-11; no hacer push ni borrar la rama `respaldo-initial-checkin` sin que Jorge lo pida. F1 empezada: F1-01 Input Actions con reasignación de teclado (D36), 31/31 tests EditMode y 4/4 PlayMode con teclado simulado (W, Espacio, F1, F2); reasignar una tecla a mano y el mando siguen sin probar. F1-02 guardia con patrulla, vista y oído (D37): 42/42 EditMode y 10/10 PlayMode, **sin jugar a mano**; rodea obstáculos con AI Navigation 2.0.9 (D38, instalado con permiso de Jorge), malla solo en `Prototype`. F1-03 parkour contextual (D39): saltar el obstáculo bajo y trepar a la plataforma con la tecla de salto; 51/51 EditMode y 17/17 PlayMode, **sin jugar a mano**. Guardia, malla de navegación y dos obstáculos de parkour también en `Z01_Relieve` (zona de prueba, no geometría de Cáceres). F1-04 interacción y checkpoint (D40): tecla E, carta recogible y zonas de reaparición, 56/56 EditMode y 19/19 PlayMode. F1-05 build de Windows reproducible (`BuildWindows`, D41) y F3 para cambiar de escena; 20/20 PlayMode. Para cerrar F1 solo queda trabajo manual de Jorge: probar `Builds/Windows` en otro PC y las 20 repeticiones de QA. El guardado en disco no existe todavía. **F2 empezada por orden de Jorge (D42):** F2-01 volumetría del casco antiguo desde LiDAR (`make_city_blocks.py` → `CityBlocks`, mallas generadas al cargar, no versionadas) con calles transitables (92,5 % del suelo libre conectado); 64/64 EditMode, 22/22 PlayMode. Falta que alguien que conozca Cáceres diga si se reconoce (gate F2). Carpeta del proyecto: `C:\Users\Propietario\Qazrix`. Personajes (D43): jugador hidalgo con calzones anchos y guardia andalusí corpulento con sable curvo, hechos con primitivas y texturas generadas (`CharacterKit`); 69/69 EditMode, 25/25 PlayMode. F2-02 (D44, 2026-11-15): cada manzana partida en edificios con tejado plano propio (607 edificios) y escalinatas de la Plaza Mayor con peldaños de 0,2 m (`make_plaza_stairs.py`, objeto `Escalinata`); 71/71 EditMode, 27/27 PlayMode, **sin jugar a mano**. Últimos informes: `Reports/F2-02_edificios_y_escalinata_2026-11-15.md`, `Reports/F2-01_casco_antiguo_volumetria_2026-11-12.md` y `Reports/ART-01_modelos_hidalgo_guardia_2026-11-12.md`. Dirección de juego: acción-sigilo tipo AC (D27).
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
