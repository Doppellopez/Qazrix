# Inventario de fuentes y datasets

Una referencia visual **no** es una licencia de importación. Nada pasa a `Assets/` sin licencia verificada y fila en `ASSET_LICENSES.csv`.

Estados de uso: `solo-consulta` · `pendiente-contacto` · `pendiente-licencia` · `verificado-apto` · `descartado`.

## Datos geográficos y 3D de Cáceres

| ID | Recurso | URL | Qué aporta | Licencia / condiciones | Estado | Próxima acción |
|---|---|---|---|---|---|---|
| R07 | Visor 3D SIG Cáceres | https://sig.caceres.es/servicios/visores/visor-3d/ | morfología y fachadas | desconocida; solo visualización | solo-consulta | incluir en la consulta escrita al SIG municipal |
| R08 | Recreación 3D intramuros (Ayto., 2023) | https://www.ayto-caceres.es/noticias/caceres-primera-ciudad-patrimonio-de-la-humanidad-contar-recreacion-3d-recinto-intramuros/ | posible modelo 3D | desconocida | pendiente-contacto | carta F0 (spec §9.1, puntos a–g) |
| R09 | Piloto CNIG escena 3D casco histórico (boletín IGN nov. 2024) | https://www.ign.es/web/resources/acercaDe/Boletines/Boletin_IGN_nov_2024.pdf | malla/LiDAR, mención a videojuegos | desconocida | pendiente-contacto | carta F0 al CNIG |
| R10 | Plan Director de la Muralla | https://sig.caceres.es/patrimonio/pdmuralla/ · https://sig.caceres.es/visores/PD_Muralla_Caceres/ | tramos, torres, fases | consulta pública; reutilización sin verificar | pendiente-licencia | preguntar derechos de planos y fotos |
| R12 | CNIG Centro de Descargas (PNOA ortofoto, MDT, LiDAR) | https://centrodedescargas.cnig.es/CentroDescargas/home | planimetría, relieve, alturas | muchos productos CC BY 4.0 compatible; **comprobar ficha de cada producto** | pendiente-licencia | elegir hojas que cubran el recinto (F0-B) |
| R13 | Catastro, descarga de datos | https://www.sedecatastro.gob.es/Accesos/SECAccDescargaDatos.aspx | huellas y volúmenes | según modalidad | pendiente-licencia | revisar condiciones y autenticación |
| R14 | OpenStreetMap | https://www.openstreetmap.org/copyright/es | red peatonal, toponimia | ODbL + atribución | pendiente-licencia | revisar obligaciones en derivados; no mezclar sin análisis |
| R20 | Visor SIG cartográfico municipal | https://visor.caceres.es/visor?map=18 | cartografía de apoyo | desconocida | solo-consulta | — |

## Historia y patrimonio

| ID | Recurso | URL | Uso | Estado |
|---|---|---|---|---|
| R01 | Extremadura Misteriosa, leyenda de la Mansaborá | https://www.extremaduramisteriosa.com/mansabora-la-doncella-encantada-de-caceres | folclore; versiones de fechas | solo-consulta |
| R02 | UNESCO, Old Town of Cáceres | https://whc.unesco.org/en/list/384/ | extensión, muralla, épocas | solo-consulta |
| R03 | Cáceres Histórica, Arco de la Estrella | https://cacereshistorica.caceres.es/patrimonio-historico/edificios-historicos/arquitectura-civil/arco-de-la-estrella-o-puerta-nueva/ | anacronismo (1726) | solo-consulta |
| R04 | Ministerio de Cultura, Casa de las Veletas | https://www.cultura.gob.es/cultura/areas/museos/mc/arquitectura-museos/museos-gestion-transferida/extremadura/70-caceres/70a-casa-veletas.html | aljibe, fases del alcázar | solo-consulta |
| R11 | Cáceres Histórica, Torre de Bujaco | https://cacereshistorica.caceres.es/patrimonio-historico/edificios-historicos/arquitectura-militar/torre-de-bujaco/ | ficha monumental | solo-consulta |
| R19 | Ministerio de Cultura, panorama histórico | https://www.cultura.gob.es/cultura/areas/patrimonio/mc/patrimoniomundial/bienes-declarados/por-ano-de-inscripcion/1986/caceres.html | contexto | solo-consulta |

## Herramientas y bibliotecas de recursos

| ID | Recurso | URL | Licencia | Nota |
|---|---|---|---|---|
| R15 | Blender · QGIS | https://www.blender.org/about/ · https://qgis.org/ | GPL (herramientas) | lo producido es nuestro |
| R16 | Poly Haven | https://polyhaven.com/license | CC0 | guardar URL, fecha y captura de la ficha de cada activo |
| R17 | ambientCG | https://docs.ambientcg.com/license/ | CC0 | ídem |
| R18 | Cesium for Unity | https://cesium.com/learn/unity | según producto | **no añadir por defecto** (spec §9.2) |

## Pendientes prioritarios (spec §18)
- [ ] Carta al SIG municipal y al CNIG (borrador por preparar).
- [ ] Derechos de reutilización de planos del Plan Director.
- [ ] Hojas PNOA concretas que cubren el recinto y su licencia exacta.
- [ ] Idoneidad de los datos del Catastro para un juego.
- [ ] Estatus histórico del pasadizo; consulta a especialista local.
