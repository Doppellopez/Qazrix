## Incremento ART-04: textura de teja en los tejados

- Objetivo/gate: que los tejados de los edificios de Z01 tengan textura de teja (petición de Jorge, 2026-11-16). Aspecto provisional de F2; no cierra ningún gate.
- Cambios de código/escenas/assets:
  - `Assets/_Qazrix/Art/Environment/ClayRoofTiles/`: `clay_roof_tiles_02_diff_1k.jpg` y `clay_roof_tiles_02_nor_gl_1k.jpg` (CC0-009).
  - `CityWallMaterial`: `EnsureRoof` pone color y relieve en `Ciudad_Tejado.mat`; el menú pasa a llamarse Qazrix → Arte → Aplicar texturas a muros, peldaños y tejados. `Z01GeoBuilder.CreateCity` lo usa al regenerar la escena.
  - La escena no se ha regenerado: ya referenciaba `Ciudad_Tejado`. Las coordenadas de textura de los tejados (proyección en planta, 3 m por repetición) ya existían desde ART-02.
- Comandos ejecutados (Unity 6000.3.6f1, sin interfaz): `CityWallMaterial.ApplyBatch`, tests EditMode, `PrototypePreview.CaptureZ01Batch`, `VerifyProject.RunBatch`; todos con salida 0.
- Pruebas: aprobadas 79/79 EditMode; `VerifyProject` salida 0. No ejecutadas: PlayMode (solo cambian un script de Editor y un material; la última pasada, 28/28, es de ART-02); jugar a mano; builds con la textura.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/z01_tejados_teja_aerea.png` (casco antiguo desde el suroeste, en alto).
- Derechos/licencias de activos nuevos: CC0-009, Clay Roof Tiles 02, Poly Haven, CC0 1.0; descarga autorizada por Jorge el 2026-11-16; fila en `Research/ASSET_LICENSES.csv`.
- Decisiones tomadas: D49.
- Problemas conocidos:
  - Desde la altura de la captura los tejados se ven de color teja uniforme: el dibujo de las hileras no se distingue. Sin comprobar de cerca, subido a un tejado.
  - Los tejados son planos y todas las hileras van en la misma dirección, sin relación con la orientación de cada edificio.
  - El suelo de la ciudad sigue con el damero de prototipo.
- Próximo paso más pequeño: que Jorge trepe a un tejado y diga si la teja se ve bien de cerca; después, textura para el suelo de calles y plaza.
