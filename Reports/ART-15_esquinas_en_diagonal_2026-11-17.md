## Incremento ART-15: esquinas de los edificios cortadas en diagonal

- Objetivo/gate: que las esquinas de los edificios se corten con líneas diagonales (petición de Jorge, 2026-11-17), en vez del recorte en escalera de ART-10.
- Cambios de código/escenas/assets:
  - `Scripts/Simulation/World/CornerRounding.cs`: `Apply` pasa a `Chamfer` (devuelve `ChamferResult`: rejilla sin las celdas quitadas y una marca por celda partida en diagonal). Un corte de tamaño n quita las celdas i + j ≤ n − 2 desde la esquina y parte por la mitad las de i + j = n − 1. Mismo criterio de esquina que antes (ángulo recto con lados que siguen el borde y bloque macizo detrás).
  - `Scripts/Simulation/World/BlockMesher.cs`: parámetro opcional `cuts`; una celda partida lleva tejado triangular, no tiene los dos lados que tocan la esquina quitada y añade un muro diagonal con la cara hacia ella.
  - `CityBlocks`: usa `Chamfer`, pasa los cortes al mesher y a las ventanas una rejilla sin las celdas partidas; nuevos `CutCells` y `TryFindCutCorner`. Z01 regenerada.
  - Tests: `CornerRoundingTests` (10) y `BlockMesherTests` (+3: área del tejado, muro diagonal hacia fuera, cuatro esquinas) en EditMode; `City_CornersAreRounded_...` en PlayMode con foto del chaflán. Las pruebas de trepada piden ahora una fachada de al menos 9 m (`FindTallFacade`): al cambiar la geometría la primera fachada encontrada era un edificio de 4 m y el jugador llegaba al tejado antes de que la prueba lo soltara.
- Comandos ejecutados: Z01GeoBuilder.BuildBatch, VerifyProject.RunBatch, tests EditMode y PlayMode (Unity 6000.3.6f1).
- Ampliación («quitar todas las esquinas»): el corte se adapta al tamaño del edificio (2 celdas; 1 en torres y edificios estrechos) y los rincones entrantes rectos se rellenan con un triángulo (`ChamferResult.FilledCells`). Se midió sobre la ciudad real: unas 772 esquinas salientes cortables y 1.671 rincones entrantes candidatos.
- Pruebas: aprobadas 155/155 EditMode y 40/40 PlayMode; VerifyProject 0 errores. La atalaya de Bujaco se vuelve a colocar sin problema. 1.258 celdas partidas por una diagonal.
- Build y plataforma: sin build nuevo.
- Evidencia: `Reports/QA/play_z01_chaflan.png` (esquina de una torre con la diagonal, desde fuera) y `Reports/QA/play_z01_chaflanes_cenital.png` (vista cenital: los contornos sacados del LiDAR son tan irregulares que el chaflán de 2 m apenas se distingue de los escalones del borde).
- Derechos/licencias de activos nuevos: ninguno.
- Decisiones tomadas: D64 (sustituye el recorte en escalera de D59).
- Problemas conocidos: sin jugar a mano; el chaflán es de 45° y 2 m (no hay otros ángulos ni curvas); la textura del muro diagonal va estirada un 41 % a lo largo; sin ventanas sobre los muros diagonales; en la fachada del hidalgo trepando, el muro diagonal es trepable como los demás.
- Próximo paso más pequeño: que Jorge lo vea en Unity; si las esquinas siguen viéndose dentadas, regularizar los contornos de los edificios (limpiar la rejilla antes de cortar) o subir el chaflán a 3.
