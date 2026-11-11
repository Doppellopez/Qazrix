# Contraste de los puntos de control con el LiDAR (Z01)

- Fecha: 2026-11-11 11:02
- LiDAR: PNOA 2018, cuadro 726-4374, clase 6 (edificios) — DATA-002. Relieve: MDT02 — DATA-001.
- Ventana: E 726000–726400, N 4372600–4373000 (EPSG:25829), celdas de 1 m. Celdas con edificio: 103991; altura máxima sobre el terreno: 35.9 m.
- Umbrales de trabajo: construido ≥ 2.5 m; «edificio alto» ≥ 15.0 m sobre el terreno.

| Punto | Altura construida en el punto | Distancia a lo construido | Distancia al edificio alto más cercano | Ese edificio alto: superficie / altura máx. | Distancia a su centro | Rumbo |
|---|---|---|---|---|---|---|
| GCP-01 Torre de Bujaco | 0.0 m | 4.2 m | 5.1 m | 173 m² / 21.3 m | 11.8 m | 117° |
| GCP-02 Arco de la Estrella | 0.0 m | 5.0 m | 5.0 m | 88 m² / 17.2 m | 10.1 m | 83° |
| GCP-03 Concatedral de Santa Maria | 16.5 m | 0.0 m | 0.0 m | 838 m² / 29.0 m | 18.1 m | 197° |

## Cómo leerlo

- **Distancia al edificio alto más cercano** es una cota inferior del error horizontal *si* el monumento es ese edificio. 0 m significa que la coordenada cae sobre un edificio alto; no prueba que sea el correcto.
- **Distancia a su centro** sería el error si la coordenada pretendiera marcar el centro de ese edificio.
- El rumbo va del punto publicado al centro del edificio (0° = norte, 90° = este).
- Identificar el edificio alto con el monumento es una **hipótesis** [RESEARCH]: hace falta ortofoto, plano o visita para confirmarlo.
- El Arco de la Estrella es un vano en la muralla, no una torre: su punto puede caer legítimamente en hueco o en el lienzo contiguo.
