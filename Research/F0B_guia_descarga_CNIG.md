# F0-B: guía de descarga de datos CNIG/PNOA para el casco antiguo de Cáceres

Estado: **pendiente de que Jorge descargue los datos**. Nada de esto está descargado ni verificado.

## Zona de trabajo (Z01)
Plaza Mayor, Torre de Bujaco y frente occidental de la muralla (spec §5, Z01).
- Centro aproximado: 39,474° N, 6,372° O (**aproximado, comprobar en el visor del CNIG**).
- Caja de trabajo sugerida: unos 600 × 600 m alrededor de ese punto. Para el relieve sirve cualquier hoja que la cubra.
- Sistema de coordenadas: ETRS89 / UTM 29N (EPSG:25829). Es el que usa `GeoOrigin` (D12).

## Qué descargar (Centro de Descargas del CNIG, R12)
https://centrodedescargas.cnig.es/CentroDescargas/home

| Producto | Para qué | Prioridad |
|---|---|---|
| Modelo Digital del Terreno (MDT02, o MDT05 si no hay) | relieve y pendientes | imprescindible |
| Ortofoto PNOA máxima actualidad | referencia visual y puntos de control | imprescindible |
| LiDAR PNOA (nube de puntos) | alturas de muralla y edificios | opcional, pesado |

## Antes de usar cada archivo
1. Abre la **ficha del producto** y copia la licencia exacta (normalmente CC BY 4.0) y el texto de atribución.
2. Anota una fila en `Research/ASSET_LICENSES.csv`: nombre de hoja, URL, fecha, licencia, atribución, estado `verificado-apto` solo si la ficha lo confirma.
3. Cambia el estado de R12 en `Research/Datasets.md`.

## Dónde dejar los archivos
`Research/Source/CNIG/` (fuera de `Assets/`). No se versionan sin Git LFS. Cuando estén ahí, el siguiente paso es el importador a metros locales (relieve + 3 volúmenes + 3 puntos de control, spec §14.3).

## Fuera de alcance
Nada de capturas de visores SIG, Google Maps ni Street View. El visor 3D del Ayuntamiento (R07) es solo consulta.
