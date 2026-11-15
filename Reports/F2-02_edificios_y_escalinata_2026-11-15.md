## Incremento F2-02: edificios separados y escalinatas de la Plaza Mayor

- **Objetivo/gate:** segundo corte de F2 (spec §5.3, capa B), por dos indicaciones de Jorge del 2026-11-15: «crear la ciudad con los edificios separados por calles» y «hacer que se puedan subir las escaleras de la plaza».

- **Cambios de código/escenas/assets:**
  - `Tools/GIS/make_city_blocks.py`: cada manzana se parte en edificios allí donde el tejado salta 0,9 m o más entre celdas vecinas; los trozos de menos de 30 m² se unen al vecino de altura más parecida, salvo que sobresalgan 4 m (torres). Cada edificio recibe un tejado plano a la cota mediana del suyo. Escribe el plano de comprobación `Research/Derived/Z01/z01_edificios.png`.
  - `Scripts/Simulation/World/BlockMesher.cs`: parámetro nuevo `stepHeight`. Dos celdas vecinas cuya cota difiere más que ese valor son volúmenes distintos: cada una conserva su tejado y entre ambas se levanta un muro del tejado alto al bajo. Sin el parámetro se comporta como antes.
  - `Tools/GIS/make_plaza_stairs.py` (nuevo): en la ventana de la Plaza Mayor localiza el suelo libre con pendiente de 14° o más y lo convierte en peldaños de 0,2 m de contrahuella sobre una rejilla de 0,2 m; ningún peldaño sube más de 0,2 m respecto a su vecino. Datos en `Assets/_Qazrix/Data/Zones/Z01/z01_escalinata.bytes` y `.json`.
  - `CityBlocks`: campo `roofStep` (0,5 m para los edificios, 0,05 m para los peldaños). `Z01GeoBuilder` crea el objeto `Escalinata` con el mismo componente y el material `Ciudad_Peldano`; entra en la malla de navegación.
  - Pruebas: 2 nuevas en `BlockMesherTests`, 2 nuevas en `Z01SceneTests`; la de monumentos exige ahora el 70 % de la altura máxima medida, porque el tejado plano rebaja agujas y remates.

- **Comandos ejecutados:** `python-qgis-ltr.bat Tools\GIS\make_city_blocks.py` y `make_plaza_stairs.py`; Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`: `Z01GeoBuilder.BuildBatch`, tests EditMode, tests PlayMode, `PrototypePreview.CaptureZ01Batch`, `VerifyProject.RunBatch`.

- **Pruebas:**
  - Aprobadas: compilación; EditMode 71/71; PlayMode 27/27; `VerifyProject` terminó con código 0.
  - Datos medidos: 607 edificios en 94 manzanas (superficie mediana 95 m², el mayor 3 663 m²); altura máxima 32,1 m; suelo libre alcanzable desde la Plaza Mayor 92,5 % (sin cambios). Ciudad: 130 trozos, 385 104 triángulos, 0,45 s. Escalinata: 696 m² de peldaños en 5 tramos, 56 208 triángulos; el mayor (402 m², 2,8 m de desnivel) es el borde de la plataforma alta de la plaza y el segundo (210 m², 4,8 m) sube hacia el Arco de la Estrella.
  - Pruebas en Play nuevas: el tejado sobre el punto de control GCP-01 es plano (variación menor de 5 cm en 2 m); el jugador, colocado al pie del tramo grande, sube andando más de 2,3 m pasando por los peldaños.
  - No ejecutadas: **Play a mano**; la subida del tramo del Arco de la Estrella y de los tres tramos pequeños no tiene prueba automática; el guardia no se ha probado sobre los peldaños; `Z01GeoReport` no se ha vuelto a pasar; sin medida de rendimiento.

- **Build y plataforma:** el build de Windows no se ha regenerado.

- **Evidencia:** `Reports/QA/z01_vista_aerea.png`, `z01_vista_plaza.png` (se ven los peldaños hacia el Arco de la Estrella), `z01_vista_cenital.png`, `play_z01_edificios.png`, `play_z01_escalinata.png`, `playmode-results.xml`, `editmode-results.xml`.

- **Derechos/licencias de activos nuevos:** ninguno nuevo. Los peldaños derivan del MDT02 (DATA-001) y los edificios del LiDAR PNOA 2018 (DATA-002), con la licencia CC BY 4.0 aún pendiente de confirmar en la ficha.

- **Decisiones tomadas:** D44.

- **Problemas conocidos:**
  - Los «edificios» son una segmentación por altura de tejado, no parcelas reales: dos casas pegadas de la misma altura salen como una (la mayor mide 3 663 m²) y una casa con dos cuerpos sale como dos.
  - Se pierden las cubiertas inclinadas: todos los tejados son planos. Las agujas quedan rebajadas (San Francisco Javier: 24,7 m frente a 32 m medidos).
  - No se han abierto calles nuevas: las que el LiDAR no ve (arcos, soportales, pasajes cubiertos) siguen cerradas.
  - Los peldaños están donde el relieve tiene pendiente, no donde hay escalera de verdad; su número y trazado no son los reales y el borde sigue la rejilla, con dientes de 0,2 m. En la parte alta, donde la pendiente baja de 14°, asoman franjas de terreno entre peldaños.
  - La ventana de peldaños solo cubre la Plaza Mayor (90 × 72 m).

- **Próximo paso más pequeño:** que Jorge abra `Z01_Relieve`, suba las dos escalinatas y diga si los edificios se distinguen como espera.
