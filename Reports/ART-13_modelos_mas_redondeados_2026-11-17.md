## Incremento ART-13: modelos más redondeados

- Objetivo/gate: que los personajes se vean más redondeados (petición de Jorge, 2026-11-17). Las esferas y cápsulas ya lo eran; lo duro eran cubos y cilindros.
- Cambios de código/escenas/assets:
  - `PrimitiveGeometry.Box` y `PrimitiveGeometry.Cylinder`: superelipsoide con exponente `Roundness = 0,3`; mismas medidas que las primitivas de Unity (extremos en ±0,5 y ±1); normales analíticas; textura de las cajas proyectada por la cara dominante y de los cilindros alrededor/planas en las tapas.
  - Mallas `Art/Characters/Meshes/Qazrix_Cube.asset` y `Qazrix_Cylinder.asset` regeneradas al reconstruir `Prototype`; `Z01_Relieve` las comparte por referencia.
  - Tests: `PrimitiveGeometryTests` (+2: caja y cilindro con aristas redondeadas y extremos intactos; normales convexas).
- Comandos ejecutados (Unity 6000.3.6f1): RebuildBatch de Prototype, VerifyProject.RunBatch, tests EditMode y PlayMode.
- Pruebas: VerifyProject 0 errores; aprobadas 138/138 EditMode y 38/38 PlayMode.
- Resultado: cubo 12→2.256 triángulos, cilindro 80→3.008; hidalgo 25.868→403.888 (15,6×), guardia 12.754→201.408 (15,8×).
- Build y plataforma: sin build nuevo.
- Evidencia: ninguna captura nueva; sin ver en pantalla.
- Derechos/licencias de activos nuevos: ninguno (mallas generadas por código, dentro de GEN-002).
- Decisiones tomadas: D62.
- Problemas conocidos: sin jugar a mano; la textura puede deformarse en la franja estrecha de las aristas; la redondez es la misma para todas las piezas (las muy finas, como la hoja, quedan casi ovaladas); no se ha medido el efecto en fps con unos 600.000 triángulos entre los dos personajes.
- Próximo paso más pequeño: que Jorge lo vea y diga si la redondez (0,3) es poca o mucha; es un solo número.

### Ajuste posterior: sin ninguna esquina

- A petición de Jorge («quitar todas las esquinas, edificios y modelos») `Roundness` pasa de 0,3 a 0,5: cajas y cilindros de los personajes sin ninguna esquina viva. Los extremos siguen siendo los mismos (±0,5 y ±1).
- Pruebas tras el cambio (RebuildBatch de Prototype, VerifyProject, EditMode, PlayMode): VerifyProject 0 errores; aprobadas 155/155 EditMode y 40/40 PlayMode.
- Los edificios no son de este incremento (esquinas y chaflán de los edificios: ART-10 y su continuación).
