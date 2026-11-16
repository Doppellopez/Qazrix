## Incremento ART-03: textura de granito en los peldaños

- Objetivo/gate: que los peldaños de la escalinata de la Plaza Mayor tengan textura de piedra (petición de Jorge, 2026-11-16). Aspecto provisional de F2; no cierra ningún gate.
- Cambios de código/escenas/assets:
  - `Assets/_Qazrix/Art/Environment/RockTileFloor/`: `rock_tile_floor_diff_1k.jpg` y `rock_tile_floor_nor_gl_1k.jpg` (CC0-008).
  - `CityWallMaterial`: `EnsureSteps` pone color y relieve en `Ciudad_Peldano.mat`; el menú pasa a llamarse Qazrix → Arte → Aplicar piedra a muros y peldaños. `Z01GeoBuilder.CreateStairs` lo usa al regenerar la escena.
  - La escena no se ha regenerado: ya referenciaba `Ciudad_Peldano`.
  - Primera textura probada, Granite Tile 04 (CC0-007): quedaba marrón oscura en el juego; Jorge pidió una más clara y se retiró del repositorio (la fila de licencia se conserva, marcada como retirada).
- Comandos ejecutados (Unity 6000.3.6f1, sin interfaz): `CityWallMaterial.ApplyBatch`, tests EditMode, `PrototypePreview.CaptureZ01Batch`, `VerifyProject.RunBatch`; todos con salida 0.
- Pruebas: aprobadas 79/79 EditMode; `VerifyProject` salida 0. No ejecutadas: PlayMode (solo cambian un script de Editor y un material; la última pasada, 28/28, es de ART-02); jugar a mano; builds con la textura.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/z01_peldanos_granito_plaza.png` (con Rock Tile Floor).
- Derechos/licencias de activos nuevos: CC0-008, Rock Tile Floor, Poly Haven, CC0 1.0; descarga autorizada por Jorge el 2026-11-16; fila en `Research/ASSET_LICENSES.csv`. CC0-007 retirada.
- Decisiones tomadas: D48.
- Problemas conocidos: la escalinata queda gris y se distingue de los muros, pero en la captura está en sombra y las juntas de las losas apenas se aprecian a esa distancia; sin comprobar de cerca. El suelo de la plaza sigue con el damero de prototipo.
- Próximo paso más pequeño: que Jorge suba la escalinata jugando y diga si le vale; después, teja en los tejados.
