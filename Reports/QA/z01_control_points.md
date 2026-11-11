# Puntos de control de Z01

- Fecha: 2026-11-11 17:45 · Unity 6000.3.6f1
- Origen local: E 726094, N 4372732, cota 440 m (EPSG:25829); X = este, Y = altura, Z = norte.
- Relieve: MDT02-ETRS89-HU29-0704-1-COB2.tif (DATA-001), 513×513 muestras cada 2 m. Superficie: MDS02_WGS84_PM-2_0704-1.tif (DATA-003).
- Tolerancias: conversión 2 mm; cota Unity frente a GDAL 0.25 m.

| Punto | Este / Norte (fuente) | Unity X / Y / Z | Error de conversión | Cota GDAL | Cota Unity | Error vertical | Confianza | Resultado |
|---|---|---|---|---|---|---|---|---|
| GCP-01 Torre de Bujaco | 726137.290 / 4372809.510 | 43.29 / -6.10 / 77.51 | 0.0000 m | 433.902 m | 433.899 m | -0.003 m | alta | en tolerancia |
| GCP-02 Concatedral de Santa Maria (nave) | 726218.500 / 4372741.500 | 124.50 / 1.52 / 9.50 | 0.0000 m | 441.524 m | 441.523 m | -0.001 m | alta | en tolerancia |
| GCP-03 Iglesia de San Francisco Javier | 726215.830 / 4372622.160 | 121.83 / 12.41 / -109.84 | 0.0000 m | 452.411 m | 452.410 m | -0.001 m | alta | en tolerancia |

**Error vertical:** mediana 0.001 m, máximo 0.003 m (3 puntos).

## Volúmenes

Cada volumen es la caja envolvente del cuerpo de 15 m o más medido en el LiDAR, con su altura máxima. Es un volumen simple de prueba: no es la huella real ni la forma del edificio.

| Punto | Caja (este-oeste × norte-sur) | Altura | Fuente |
|---|---|---|---|
| GCP-01 Torre de Bujaco | 17 × 17 m | 21.3 m | LiDAR PNOA 2018 clase 6 (DATA-002); candidato L29 |
| GCP-02 Concatedral de Santa Maria (nave) | 43 × 39 m | 29.0 m | LiDAR PNOA 2018 clase 6 (DATA-002); candidato L38 |
| GCP-03 Iglesia de San Francisco Javier | 74 × 46 m | 32.0 m | LiDAR PNOA 2018 clase 6 (DATA-002); candidato L52 |

## Puntos descartados

Se conservan solo como registro; no se colocan en la escena.

| Punto | Este / Norte | Fuente |
|---|---|---|
| WD-01 Torre de Bujaco (Wikidata) | 726126.8 / 4372814.9 | Wikidata Q6150350 (P625; CC0) |
| WD-02 Arco de la Estrella (Wikidata) | 726137.6 / 4372780.1 | Wikidata Q5703166 (P625; CC0) |
| WD-03 Concatedral de Santa Maria (Wikidata) | 726223.9 / 4372758.8 | Wikidata Q2942653 (P625; CC0) |

## Avisos

- **Qué garantizan estos puntos:** la posición sale del LiDAR PNOA 2018 (celdas de 1 m) y el nombre lo confirmó Jorge sobre el plano de candidatos. Cada punto es el centro del cuerpo de 15 m o más, no una esquina medida en campo.
- **Qué falta:** el error horizontal de la spec (§5.6) compara estos puntos con la geometría modelada, y aún no hay edificios modelados: se medirá en F2. Son 3 puntos; la spec pide de 15 a 25.
- El error de conversión y el vertical de la tabla validan la cadena de datos a Unity, no la posición de los monumentos.
- Licencia de DATA-001, DATA-002 y DATA-003 pendiente de confirmar en la ficha de cada producto del CNIG.
