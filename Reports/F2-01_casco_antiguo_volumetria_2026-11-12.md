## Incremento F2-01: volumetría del casco antiguo de Cáceres con calles transitables

- **Objetivo/gate:** primer corte de F2 («Cáceres reconocible en graybox georreferenciado», spec §10), capa B de spec §5.3: proporciones, alturas, anchuras y perfil de cubiertas con material neutro. Empezado por orden directa de Jorge con el gate F1 pendiente solo de sus pruebas manuales (D42).

- **Cambios de código/escenas/assets:**
  - `Tools/GIS/make_city_blocks.py`: rasteriza a 1 m los puntos LiDAR de clase 6 (edificios) en una ventana de 512 × 576 m que cubre el recinto intramuros, la Plaza Mayor y un margen; descarta lo que mide menos de 2,5 m sobre el terreno, rellena huecos de una celda, quita motas de menos de 12 m² y escribe la cota de tejado de cada celda en metros locales.
  - **Calles transitables** (petición de Jorge): el radio de rasterización baja de 0,75 m a 0,5 m para que los edificios no engorden y cierren las calles estrechas, y todo pasillo de una sola celda se ensancha a 3 m quitando la celda construida de cada lado (997 celdas). El script mide qué parte del suelo libre queda conectada a pie con la Plaza Mayor.
  - `Scripts/Simulation/World/BlockMesher.cs` (lógica pura): superficie continua de tejado sobre las celdas construidas y muro hasta el suelo en cada borde con celda vacía, en trozos de 48 m.
  - `Scripts/Runtime/World/CityBlocks.cs`: convierte esos datos en mallas y colisionadores al activarse, en el editor y en el juego. **Las mallas no se guardan**: el repositorio solo lleva el archivo de datos (1,1 MB). Ofrece consultas `HasBuildingAt`, `IsClear` y `FindOpen`.
  - `Z01GeoBuilder`: crea la ciudad, busca un claro en la Plaza Mayor para el punto de salida y coloca ahí la zona de juego (murete, plataforma, carta, checkpoint y guardia, cuya ruta se ajusta a puntos despejados). Se retiran los tres bloques de prueba de F0-B; los postes de los puntos de control siguen. La malla de navegación incluye ya calles y edificios.
  - Datos: `Assets/_Qazrix/Data/Zones/Z01/z01_tejados_1m.bytes` y `.json`. Materiales de color plano `Ciudad_Tejado` y `Ciudad_Muro`.

- **Comandos ejecutados:** `python-qgis-ltr.bat Tools\GIS\make_city_blocks.py` (PDAL 2.10, GDAL 3.13.3); Unity 6000.3.6f1 sobre `C:\Users\Propietario\Qazrix`: `Z01GeoBuilder.BuildBatch`, `PrototypePreview.CaptureZ01Batch`, `VerifyProject.RunBatch`, `Z01GeoReport.RunBatch`, tests EditMode y PlayMode.

- **Pruebas:**
  - Aprobadas: compilación; VerifyProject 0 errores, 1 aviso (pipeline provisional); EditMode 64/64 (8 nuevos del generador de volúmenes); PlayMode 22/22; puntos de control en tolerancia.
  - Datos medidos: 147 899 m² construidos en 94 manzanas; altura mediana 9,7 m, percentil 95 de 16,9 m, máxima 35,9 m. **Suelo libre alcanzable a pie desde la Plaza Mayor: 92,5 %** (el resto son patios cerrados).
  - Ciudad en Unity: 130 trozos, 364 578 triángulos, levantada en 0,3 s.
  - Pruebas en Play nuevas (escena `Z01_Relieve`): la ciudad se construye y el jugador nace en un claro; en la caja de cada punto de control hay tejado de la altura medida; el jugador anda 6 s por la plaza sin atascarse ni meterse en un edificio; el guardia patrulla entre edificios siguiendo la malla; Espacio salta el murete.
  - Falladas por el camino y corregidas: la comprobación de monumentos falló tres veces por la definición del punto GCP-03 (ver «Problemas conocidos»), no por la ciudad; un error del script de Python al leer el ráster.
  - No ejecutadas: **Play a mano**; ningún recorrido real por las calles (la prueba automática anda solo por la plaza); **nadie que conozca Cáceres ha comprobado que sea reconocible**, que es lo que exige el gate F2; sin medida de rendimiento en el equipo de Jorge.

- **Build y plataforma:** el build de Windows no se ha regenerado con la ciudad.

- **Evidencia:** `Reports/QA/z01_vista_aerea.png`, `z01_vista_plaza.png` (Plaza Mayor con el cuerpo de la Torre de Bujaco y su poste), `z01_vista_cenital.png`, `play_z01_ciudad.png`, `playmode-results.xml`, `editmode-results.xml`.

- **Derechos/licencias de activos nuevos:** los tejados derivan del LiDAR PNOA 2018 (DATA-002) y del MDT02 (DATA-001), ambos del CNIG con licencia CC BY 4.0 declarada y **pendiente de confirmar en la ficha**. Es el primer dato derivado del CNIG que entra en `Assets/` y en el juego: obliga a citar la fuente en los créditos y a no publicar builds hasta cerrar esa licencia.

- **Decisiones tomadas:** D42.

- **Problemas conocidos:**
  - **Es volumetría, no arquitectura.** Los tejados son una superficie suavizada (salen «derretidos» en torres y saltos de altura) y los muros son planos y sin huecos. No hay fachadas, puertas, ventanas ni muralla diferenciada.
  - **Aspecto actual, no de 1229** (spec §2.2): edificios posteriores incluidos, sin catálogo de anacronismos.
  - Las calles son el relieve MDT tal cual: sin pavimento, escaleras ni bordillos; en pendientes fuertes puede haber tramos no andables.
  - Una calle solo existe si el LiDAR vio suelo: pasajes cubiertos, arcos y soportales (el Arco de la Estrella, los portales de la plaza) salen como edificio macizo.
  - La ventana termina al oeste en E 726000 (borde del cuadro LiDAR): el lado oeste de la Plaza Mayor queda cortado. El cuadro 724-4374 está descargado y permite ampliarla.
  - GCP-03 (San Francisco Javier) es el centro de un cuerpo de 74 × 46 m y cae en un patio; sus torres quedan a un lado. Conviene redefinirlo como centro de una de las torres.
  - Los colisionadores son la malla completa (364 578 triángulos): correcto en las pruebas, sin medir en un equipo modesto.
  - Solo `Z01_Relieve` tiene ciudad; la malla de navegación cubre 160 m alrededor del punto de salida.

- **Próximo paso más pequeño:** que Jorge abra `Z01_Relieve`, recorra la Plaza Mayor y entre por alguna calle del recinto, y diga qué reconoce y qué no. Después: ampliar la ventana al oeste con el cuadro 724-4374 y diferenciar la muralla y las torres.
