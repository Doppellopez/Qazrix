# QAZRIX — Especificación integral de producto y desarrollo

**Versión:** 1.0 · **Fecha:** 9 de noviembre de 2026  
**Estado:** especificación inicial para prototipado; no constituye aprobación de producción completa.  
**Destinatarios:** equipo de desarrollo, dirección artística y Claude Code.  
**Motor:** Unity 6.3 LTS (versión exacta de parche fijada al crear el repositorio).  
**Idioma de trabajo:** español para diseño y documentación; inglés para nombres de clases, métodos, interfaces y rutas de código.

> **Premisa:** Cáceres, 1229. Un capitán del ejército leonés recibe una misión de espionaje en la ciudad sitiada de Qazrix. Su relación con la hija del caíd abre la puerta a un pasadizo secreto, pero cada promesa afecta al destino de la ciudad. El asalto culminará en una traición —o en una decisión capaz de cambiar la leyenda de la Mansaborá.

> **Regla de oro:** Qazrix **no es una ciudad medieval genérica**: es una Cáceres reconocible en su geometría, recorridos, desniveles, muralla, torres y lugares emblemáticos. La ambientación es una recreación artística de 1229 sobre esa geografía actual. No se presentará como una reconstrucción arqueológica literal.

---

## 0. Cómo interpretar este documento

- **[MUST]** imprescindible para aceptar el hito correspondiente.
- **[SHOULD]** recomendado; puede aplazarse con una razón documentada.
- **[COULD]** posibilidad de ampliación, fuera de la primera versión.
- **[RESEARCH]** hipótesis o dato por comprobar, nunca convertir en hecho por inferencia.
- **[FICTION]** decisión de guion o arte inventada deliberadamente.
- **MVP técnico:** prototipo que demuestra el bucle jugable, sin exigencia de ciudad completa.
- **Vertical slice:** fragmento pequeño, jugable y pulido que demuestra el nivel de calidad final.
- **Ciudad 1.0:** casco antiguo explorable completo en exteriores; interiores seleccionados.

La versión final no está aprobada por el mero hecho de que Claude Code genere scripts. Hay que **abrir Unity, ejecutar, jugar, medir, comparar visualmente y verificar las licencias**.

## 1. Visión y límites

### 1.1 Fantasía del jugador

Ser un espía/acróbata en una ciudad-fortaleza viva: recorrer tejados y adarves, infiltrarse por callejuelas y edificios, negociar con personajes que recuerdan decisiones, descubrir pasadizos y tomar una decisión que cambia el final de una leyenda.

### 1.2 Pilares del producto, en orden de prioridad

1. **Cáceres es protagonista.** El jugador debe reconocer la ciudad; la exploración libre tiene valor por sí misma.
2. **Movimiento excelente.** Recorrer el entorno en tercera persona es satisfactorio incluso sin misión.
3. **Sigilo legible y justo.** Cada patrulla y cada alarma tienen causas visibles y consecuencias comprensibles.
4. **Personajes y dilema moral.** La hija del caíd es un agente de la historia, no un objeto que obtener.
5. **Misterio sobrio.** El elemento sobrenatural surge gradualmente; la maldición culmina la trama.
6. **Alta calidad donde importa.** Un sector pequeño y extraordinario antes que una ciudad enorme y mediocre.

### 1.3 Propuesta de formato

- Género: aventura de acción y sigilo en tercera persona, semimundo abierto compacto.
- Inspiración funcional: parkour y movimiento contextual, infiltración urbana y misiones con rutas alternativas. **No copiar** personajes, interfaz, animaciones, logotipos ni elementos protegidos de Assassin's Creed.
- Plataforma inicial: **PC Windows**, teclado/ratón y mando. Mac/Linux solo después de comprobar compatibilidad. No diseñar inicialmente para móviles.
- Modelo de negocio: premium de un jugador; sin monetización dentro del juego.
- Modo principal: campaña narrativa de aproximadamente **4–7 horas como objetivo aspiracional** (a validar con prototipos, no compromiso).
- Modo **Paseo por Qazrix**: exploración libre sin guardias hostiles ni presión de tiempo; desbloqueable desde la primera demo de ciudad.
- Clasificación de contenido aspiracional: violencia moderada, sin gore ni sexualización; clasificación formal pendiente.
- Dificultad: normal por defecto, asistencia configurable, reinicio por checkpoints.
- Offline; sin cuenta, backend, multijugador o contenido procedural narrativo generativo en tiempo de ejecución.

### 1.4 Fuera de alcance inicial

Multijugador, combate masivo a campo abierto, caballos como sistema, cientos de interiores, todos los tejados accesibles, mundo abierto fuera de Cáceres, juego para móvil, NPC con LLM online, crafting complejo, gestión de inventario tipo RPG, árbol de habilidades gigante, doblaje profesional completo y reconstrucción históricamente exacta de 1229. Posponer todo ello evita ocultar un proyecto AAA dentro de una demo independiente.

## 2. Contexto histórico, leyenda y criterio artístico

### 2.1 Hechos y fuentes

- La toma cristiana de Cáceres se sitúa en **1229**, vinculada a Alfonso IX de León y a San Jorge. Las narraciones populares difieren incluso en algunos detalles cronológicos: la versión de Extremadura Misteriosa menciona tanto el 22 como el 23 de abril. Tratar el día exacto de cada escena como una **convención narrativa**, no como prueba documental. [R01]
- La tradición de la **Mansaborá** describe a la hija del caíd, un capitán cristiano, encuentros secretos, una llave, un pasadizo y una maldición que la transforma, junto a sus acompañantes, en gallina y polluelos. La historia es **folclore**, no relato histórico acreditado de esos personajes. [R01]
- La ciudad monumental inscrita por la UNESCO ocupa aproximadamente **9 ha** y conserva un recinto amurallado de unos **1.174 m**, con arquitectura de varias épocas, notablemente islámica, gótica y renacentista. [R02]
- Las murallas y algunas torres sí tienen base islámica; buena parte de las fachadas y palacios que hoy reconocemos son posteriores al siglo XIII. El **Arco de la Estrella actual es de 1726**; el edificio actual de **las Veletas** es posterior, pero conserva un aljibe de época medieval y el emplazamiento del antiguo alcázar. [R03] [R04]
- La leyenda menciona el **pasadizo de la Victoria** y un hallazgo en 1942; su trazado y su relación histórica con el episodio necesitan verificación independiente. El recorrido de juego será una **interpretación ficticia**, no una guía a túneles reales. [R01]

### 2.2 Contrato de autenticidad

Separar tres capas:

| Capa | Fidelidad pretendida | Ejemplo |
|---|---|---|
| **A. Geografía actual** | Alta y medible | posición relativa de calles, murallas, plazas, pendientes y volúmenes exteriores |
| **B. Ambientación del siglo XIII** | Plausible y documentada, con libertad artística | indumentaria, puestos, vegetación, iluminación, soldados, vida cotidiana |
| **C. Relato fantástico** | Expresamente ficción | personajes centrales no documentados, pasadizo jugable, maldición y finales |

**[MUST]** Incluir en menú/créditos una nota sencilla: «Qazrix está inspirado en una leyenda cacereña y recrea libremente la ciudad de 1229 usando como referencia su fisonomía actual. Algunos edificios, acontecimientos y personajes se han adaptado por razones artísticas». No afirmar que el aspecto actual representa exactamente 1229.

**[MUST]** En una ficha por monumento, indicar época, fuentes, grado de certeza y anacronismos deliberados. No extrapolar una fecha a todo un inmueble cuando tiene fases constructivas.

### 2.3 Sensibilidad cultural

Mostrar a los habitantes andalusíes y leoneses como personas diversas, no como bandos culturalmente uniformes. Evitar clichés «civilización contra barbarie», exotización, caricatura religiosa o lenguaje moderno puesto en boca de todos sin cuidado. La princesa/doncella debe poder negarse, decidir y tener objetivos políticos propios; la relación romántica no se resuelve con una mecánica de manipulación o secuestro presentado como premio. La violencia y la caída de la ciudad tienen consecuencias humanas.

## 3. Historia propuesta

### 3.1 Título, lema y protagonistas

**QAZRIX — La llave de la Mansaborá** (subtítulo provisional).  
Lema: *«Una ciudad sitiada. Un juramento imposible. Una leyenda por escribir»*.

**Personajes [FICTION]** — los nombres son provisionales y deberán revisarse con asesoría cultural:

- **Rodrigo**, capitán leonés; personaje jugador, leal al ejército pero con dudas morales. Sus acciones definen qué clase de hombre llega a ser.
- **Zahra**, hija del caíd. Conoce redes de sirvientes, abastecimiento y pasadizos; desea proteger a su gente y no acepta ser moneda de cambio. Su confianza se gana con hechos, no con regalos o una puntuación de «seducción».
- **El caíd**, padre de Zahra, responsable de una ciudad bajo asedio. Antagonista por intereses enfrentados; capaz de afecto y dureza, no villano plano.
- **Alfonso IX**, figura histórica con presencia escénica limitada. Sus instrucciones imponen el conflicto del capitán.
- **La confidente**, ayudante de Zahra; enlace entre interior y exterior, con voluntad y riesgos propios.
- **El jefe de la guardia**, rival táctico que aprende de los patrones de infiltración, sin inteligencia artificial mágica.

No atribuir nombres inventados a personajes históricos ni presentar diálogos ficticios como citas documentales.

### 3.2 Estructura en cuatro actos

**Prólogo — La ciudad cerrada.** El ejército de Alfonso IX acampa fuera de la muralla. Tutorial de movimiento en un entorno pequeño; el jugador participa en una embajada ante el caíd. Cruza una mirada con Zahra. Se presenta el dilema: conquistar la plaza sin sacrificar a sus habitantes o cumplir órdenes a toda costa.

**Acto I — El pañuelo.** Un mensaje conduce al protagonista a una fuente entre huertas y luego a un acceso oculto. Aprendizaje de sigilo y parkour. Zahra pide que se investigue un asunto de abastecimiento antes de confiar. El jugador descubre sectores de la ciudad y se abre el modo paseo.

**Acto II — La llave.** Misiones de información, infiltración en almacenes, ayuda a civiles, alianzas y encuentros privados. Las acciones modifican la confianza y el nivel de sospecha. Zahra posee la llave o puede acceder a ella, pero no la entregará automáticamente: debe decidir si hacerlo sirve a sus propios fines. Un agente del ejército presiona para aprovechar el pasadizo.

**Acto III — La noche del asalto.** Se coordinan dos planes. El jugador puede cumplir la orden de permitir una entrada armada, proteger una evacuación o buscar una salida de compromiso; cada opción cambia rutas, aliados y pérdidas. La victoria militar y la supervivencia de Zahra **no** son variables que el jugador pueda maximizar sin coste.

**Epílogo — La Mansaborá.** El caíd pronuncia o intenta pronunciar la maldición. Se revela la dimensión sobrenatural. La experiencia termina con una escena jugable breve, asociada a la suerte de Zahra y la memoria de Qazrix. Las apariciones de gallina y polluelos son perturbadoras/poéticas, no un gag visual.

### 3.3 Finales (mínimo tres, sin explosión combinatoria)

| Final | Condiciones conceptuales | Consecuencia |
|---|---|---|
| **La leyenda conocida** | la llave se utiliza para facilitar el asalto, confianza traicionada | caída de la ciudad y maldición; Zahra desaparece |
| **La fuga** | confianza alta, protección efectiva de civiles, renuncia a una ventaja militar | Zahra escapa; el capitán pierde honor o posición; final agridulce |
| **El juramento roto** | intento de salvar ambos bandos mediante negociación/evacuación, decisiones específicas | se limita la tragedia, pero se paga un precio y la maldición queda como ambigüedad |

Los tres finales **requieren** las mismas tres misiones finales con variantes de objetivos, diálogos y set pieces: no construir tres campañas separadas. Posible final secreto [COULD] solo si las tres rutas principales funcionan.

### 3.4 Modelo narrativo implementable

Variables mínimas, no un RPG social complejo:

- `ZahraTrust`: estados discretos **Low / Medium / High**; resultado de decisiones relevantes.
- `CitySuspicion`: **Calm / Alert / Lockdown** por zona o capítulo.
- `CivilianOutcome`: **Protected / Mixed / Harmed**; derivado de acciones trazables.
- `PassageKnowledge`: **Unknown / Found / Shared / Concealed**.
- `FinalChoice`: selección explícita en Acto III.

**[MUST]** Los finales dependen de predicados conocidos y comprobables, con pistas suficientes al jugador; nunca de puntuaciones invisibles y caprichosas. Se pueden implementar por `ScriptableObject` con un evaluador determinista y tests de todas las rutas.

## 4. Diseño de misiones

### 4.1 Lista de campaña — objetivo aspiracional

| ID | Misión | Zona principal | Mecánicas | Resultado |
|---|---|---|---|---|
| Q00 | La embajada | campamento → acceso principal → sede del caíd | caminar, cámara, diálogo | presentación de Zahra |
| Q01 | El pañuelo | extramuros, huertas, fuente | exploración, pistas | contacto secreto |
| Q02 | Bajo la muralla | entrada oculta → galerías | agacharse, oscuridad, patrulla | acceso a la medina |
| Q03 | La ciudad que resiste | entorno Santa María/San Jorge | desplazamiento urbano, favor a civiles | confianza inicial |
| Q04 | Sobre los adarves | muralla/torres seleccionadas | parkour, persecución | conocer vigilancia |
| Q05 | El precio del agua | entorno del alcázar/aljibe ficcionalizado | infiltración, objeto e información | dilema sobre abastecimiento |
| Q06 | La llave | sede del caíd y rutas altas | acceso restringido, diálogo | control del secreto |
| Q07 | Dos lealtades | campamento y ciudad | conversación, elección | plan de asalto |
| Q08 | Noche de San Jorge | ciudad, pasadizo, puerta | sigilo, alarma, escape, combate limitado | bifurcación final |
| Q09 | La Mansaborá | alcázar y subsuelo | epílogo sobrenatural | uno de tres finales |

La correspondencia exacta entre cada misión y las calles se ajustará **después** de validar geometría y tiempos de recorrido; no inventar distancias.

### 4.2 Misiones del primer vertical slice

Una misión **completa**, de **15–20 min como hipótesis de diseño**, integrada en el sector de mayor calidad:

1. Comienzo en un borde de la Plaza Mayor (ubicación ajustada al mapa).
2. Objetivo: encontrar una carta/sello e identificar una ruta hacia el interior.
3. Dos aproximaciones válidas: a pie por calles con controles, o por cubiertas y un tramo accesible de muralla.
4. Dos guardias con rutas legibles, zonas de sombra/cobertura y un punto de distracción.
5. Interacción con un contacto que aporta una pista y conduce a una entrada oculta **ficticia**.
6. Escena corta de diálogo con Zahra o su confidente (no exigir aquí modelo final del personaje).
7. Elección menor con consecuencia observable y checkpoint final.
8. Final de demo: acceso al modo paseo y créditos de fuentes.

La misión se diseña sobre la **geometría real del sector**; si una azotea no es geométricamente plausible para un salto, se mueve la acción, no el edificio patrimonial.

## 5. Diseño del mundo y política de reconstrucción

### 5.1 Frontera jugable

- **Ciudad 1.0 [MUST]:** exteriores peatonales del recinto monumental completo, conectados de forma continua; muralla visible en toda su extensión; acceso a determinadas torres y azoteas por rutas predefinidas.
- **Exterior [SHOULD]:** borde de Plaza Mayor y conexiones inmediatas necesarias para entender el asedio y el perímetro; paisaje distante de Sierra de la Mosca como fondo verificable, no mundo abierto.
- **Interiores [MUST en campaña, limitados]:** sala principal del alcázar (recreación), aljibe/espacio inspirado en él, una casa/estancia de contacto y el sistema subterráneo ficticio.
- **No jugable:** la mayoría de viviendas e interiores. Puertas pueden estar cerradas o llevar a pequeños espacios de fachada si son útiles, sin promesas de acceso general.

### 5.2 Zonas para catálogo y producción

- **Z01:** Plaza Mayor / Torre de Bujaco / frente occidental de la muralla.
- **Z02:** acceso hacia la Plaza de Santa María y su entorno.
- **Z03:** entorno de San Jorge y Cuesta de la Compañía.
- **Z04:** entorno de Plaza de las Veletas / San Mateo / emplazamiento del alcázar.
- **Z05:** adarves, torres y accesos del perímetro restante.
- **Z06:** calles interiores secundarias, patios visibles y rutas de conexión.
- **Z07:** huertas, fuente, campamento y paisaje extramuros — adaptación artística.
- **Z08:** interiores narrativos y pasadizos — geografía dramatizada.

Estas zonas son **paquetes de trabajo**, no un plano históricamente certificado. El mapa final saldrá de una importación GIS y de un inventario de puntos de control.

### 5.3 Reconstrucción por capas de datos

**A. Base geoespacial [MUST]:** digitalizar o importar terreno, límite intramuros, ejes/corredores de calles, huellas de edificios y murallas, altitudes. Mantener tabla de procedencia.

**B. Volumetría [MUST]:** proporciones, alturas, anchuras, pendientes y perfil de cubiertas; material neutro primero. Las calles no se enderezan para comodidad del jugador.

**C. Hero assets [MUST]:** Bujaco, lienzos de muralla y accesos del sector demo, conjunto de Veletas/alcázar, aljibe, plazas clave. Modelado dedicado basado en referencias autorizadas. Identificar anacronismos.

**D. Materiales [MUST]:** granito, mampostería, tapial, revocos, tejas, madera, hierro, suelos. Texturas PBR, uso de trim sheets, decals y variación controlada.

**E. Vida y decorado [SHOULD]:** tenderetes, cestas, cántaros, animales, telas, luz de fuego, sonidos, actividad de ciudad sitiada; sin tapar puntos emblemáticos.

**F. Jugabilidad [MUST]:** colisiones simplificadas, superficies transitables y escalables, rutas de IA y volúmenes de ocultación **separados** del render 3D.

### 5.4 Política contra «la maqueta bonita pero injugable»

La fotogrametría y el LiDAR son excelentes **referencias**, pero pueden contener personas, vegetación, ruido, superficies perforadas, artefactos, farolas y mobiliario moderno. No asumir que un mesh fotogramétrico sirve automáticamente para navegación, parkour, destrucción, iluminación o colisiones. La estrategia preferida es:

1. **Base GIS y fotogrametría como guía dimensional y visual.**
2. **Malla optimizada de juego**, re-topologizada o remodelada.
3. **Colisionadores proxy** y puntos explícitos de agarre/salto.
4. **Modelos hero** de alta fidelidad para el primer plano.
5. **LOD/HLOD o partición** y descarte por visibilidad para edificios de fondo.

Si el CNIG autoriza una malla 3D aprovechable, hacer un prototipo A/B de calidad/retopología/coste antes de decidir el método de producción de la ciudad.

### 5.5 Fichas por edificio y banco de referencias

Cada edificio o tramo debe tener `id`, denominación actual, coordenadas, precisión/fecha de fuente, fase histórica del elemento, dimensión/altura, fotografías de referencia con permiso, nivel de detalle, estado de modelado, diseñador/responsable, licencia y notas de jugabilidad. Ejemplo de campos:

```yaml
id: CAC_Z01_TORRE_BUJACO
name: Torre de Bujaco
zone: Z01
source_period: almohade; intervenciones posteriores
reference_urls:
  - https://cacereshistorica.caceres.es/patrimonio-historico/edificios-historicos/arquitectura-militar/torre-de-bujaco/
geometry_status: needs_survey
asset_status: not_started
historical_confidence: medium
playable: exterior_and_selected_route
rights_status: references_only
notes: "Conservar geometría reconocible; documentar añadidos posteriores."
```

### 5.6 Georreferenciación

- Trabajar cartografía peninsular con referencia **ETRS89 / UTM huso 29N (EPSG:25829)** cuando el origen y metadatos lo confirmen. No mezclar coordenadas proyectadas, WGS84, alturas elipsoidales y cotas ortométricas sin transformación explícita.
- Definir un **origen local documentado** cercano al centro jugable; importar coordenadas dobles en herramientas y convertir a metros locales (`Vector3`) al exportar a Unity. Ejes y altura deben quedar documentados en una transformación única y probada.
- Definir `GroundControlPoints.csv`: entre 15 y 25 puntos reconocibles (esquinas, torres, centros de puertas y plazas), coordenadas fuente, coordenadas Unity, fuente, error horizontal/vertical y confianza.
- La **tolerancia inicial propuesta** es error horizontal mediano ≤ 2 m en control points fiables; para hero assets del sector demo, objetivo ≤ 1 m tras revisión manual. Son metas de QA, no precisión garantizada por los datos disponibles. Registrar siempre el máximo y los casos fuera de tolerancia.
- Mantener orientación cardinal, ancho y pendiente de calles; hacer comparativas de capturas con vista aérea y fotografías a pie de calle.
- No inventar alturas para resolver huecos: estimarlas con metodología etiquetada y sustituirlas cuando existan medidas fiables.

### 5.7 Pipeline GIS → arte → Unity

1. Inventario de capas y sus licencias en `Research/Datasets.md`.
2. QGIS: reproyección, limpieza, recorte del área, validación de geometrías, análisis de elevación.
3. Opcional: Python/GDAL/PDAL para procesar raster y LAZ, dejando comandos reproducibles y versiones.
4. Exportar polígonos y curvas **desde archivos locales autorizados**, no extraer datos mediante técnicas contrarias a condiciones de uso de visores.
5. Blender: generación paramétrica de volúmenes, ajuste de fachadas y cubiertas, UVs y materiales.
6. Blender → glTF/FBX → Unity, respetando metros, ejes, UVs, normales y pivotes consistentes.
7. Herramientas Unity Editor para importar, agrupar y regenerar zonas de forma idempotente.
8. Regenerar mapas de navegación y proxies; validar puntos de control y rutas.

**[MUST]** Documentar todos los pasos que dependan de una acción manual. Scripts de automatización seguros e idempotentes; no sobrescribir trabajo artístico sin copia y advertencia.

## 6. Jugabilidad — sistemas y criterios

### 6.1 Movimiento y cámara

**[MUST en prototipo]** controlador en tercera persona: caminar/correr, aceleración y frenado, giro, saltar, aterrizar, caer, agacharse y atravesar pequeños obstáculos. Cámara con orbitado, colisión, suavizado, zoom/FOV moderado y ajuste de sensibilidad; compatible con ratón y mando.

**[MUST en vertical slice]** parkour **contextual y predecible**, no un sistema procedural universal:

- Vault en obstáculos bajos, trepar puntos marcados, colgarse de bordes autorizados, subir/bajar y saltar a plataformas adyacentes dentro de rangos medidos.
- Detección mediante consultas físicas y **componentes/volúmenes de parkour** colocados o generados con revisión. Las etiquetas de material por sí solas no habilitan escalada universal.
- Señalización discreta de ruta (lectura del entorno, sin inundar con iconos).
- Ayuda de aterrizaje limitada y configurable; control completo de cámara donde sea posible.
- Caídas grandes producen daño o respawn sin animaciones interminables.

**Criterio de aceptación:** completar 20 recorridos consecutivos del circuito de demo sin quedar atrapado, saltos fantasma o atravesar mallas; mantener consistencia entre 30 y 120 fps; ruta repetible con mando/teclado. Establecer longitudes/velocidades después de jugar, no fijarlas arbitrariamente hoy.

### 6.2 Sigilo y alarma

Modelo inicial simple y verificable:

`Patrol → Suspicious → Investigate → Alert → Search → Return`

- Visión con cono, distancia, obstrucción por geometría y nivel de exposición; audición por eventos cuantificados y configuración por tipo de superficie/acción.
- La noche y las sombras pueden modular detección, pero deben ser consistentes y explicables.
- La IA nunca «ve a través» de paredes; estados, objetivos y sensores visibles en gizmos de depuración.
- Señal de descubrimiento gradual y localización de último avistamiento. Una alarma puede afectar a zona, no mágicamente a toda la ciudad.
- Rutas alternativas viables: distraer, esconderse, rodear, huir; no convertir cada fallo en combate obligatorio.

**Aceptación:** probar detección frontal, lateral, tras obstáculo, dentro de cobertura, sonido fuera de vista, pérdida de contacto y retorno a patrulla. Cada caso debe tener prueba automatizada parcial y escenario de QA reproducible.

### 6.3 Combate

**[MUST en campaña, [SHOULD] en primera demo]** combate deliberadamente limitado: bloquear/esquivar, ataque ligero, aturdimiento, retirada; pequeño número de enemigos activos. Evitar convertir la experiencia en un brawler o una guerra de cientos de soldados. Los encuentros de misión tienen soluciones de sigilo, huida o intervención, no exclusivamente matar.

### 6.4 Interacción, diálogos, decisiones

- Interacciones contextuales con `Interactable` común: puertas, cartas, pistas, escondites, palancas y conversaciones.
- Sistema de diálogo **data-driven** (nodos, opciones, condiciones, efectos, localización) con archivos fáciles de revisar y tests de referencias.
- Las conversaciones críticas pueden pausarse; saltar líneas no debe saltar efectos o condiciones.
- Registrar decisiones relevantes en una bitácora accesible desde menú.
- Evitar una barra de «seducción». Comunicar confianza mediante hechos, diálogo y consecuencias.

### 6.5 Misiones y guardado

- Estado de misión por objetivos con identificadores estables, condiciones de activación, completado, fallo y checkpoint.
- Guardado local **versionado**, seguro frente a interrupción (escritura temporal + sustitución validada), no usar serialización directa de referencias Unity volátiles.
- Guardar misión, decisiones, zona, transform permitido, estado esencial de NPC y opciones. Nunca usar posición del jugador inválida al cargar; definir punto seguro alternativo.
- Autorrecuperación tras detección/fallo sin bloqueos narrativos. Menú de reiniciar checkpoint.
- Logs de transición de misión en builds de desarrollo.

### 6.6 «Paseo por Qazrix»

**[MUST desde la vertical slice urbana]** modo sin enemigos ni misiones obligatorias; movimiento y cámara, ubicación, mapa sencillo, puntos de interés y **alternancia configurable de capas**:

- **Qazrix 1229:** ambientación ficcional medieval del juego.
- **Información del Cáceres actual:** fichas de monumentos, fechas, diferencias y fuentes. No exige reconstruir dos ciudades completas; puede ser una capa informativa en un mismo mundo, con la opción de activar/desactivar objetos contemporáneos en futuras versiones [COULD].

Esto convierte la geografía en una característica de producto y facilita revisar la reconstrucción con personas de Cáceres.

### 6.7 Accesibilidad mínima

Subtítulos desde el principio, tamaños legibles, remapeo de controles si el sistema elegido lo permite, sensibilidad de cámara, inversión de ejes, reducción de movimiento brusco y FOV/cámara donde sea viable, opciones de dificultad/detección, modos sin eventos de precisión excesiva y no depender únicamente del color para indicar alarma.

## 7. Dirección artística y audio

### 7.1 Identidad visual

**Realismo estilizado contenido**, no fotorrealismo imposible para equipo pequeño. Priorizar siluetas reales de Cáceres, piedra verosímil, atmósfera, iluminación y animaciones sólidas. Evitar filtro sepia genérico; el pasado no era monocromo. Paleta: granito cálido/frío, telas ricas pero creíbles, luz dorada de tarde, noches azules con focos cálidos, interiores aljibe húmedos y oscuros.

Tres escenarios de referencia técnica: **día despejado**, **atardecer** y **noche de sigilo**. Congelar primero el aspecto de un sector antes de producir todas las calles.

### 7.2 Materiales y arquitectura

PBR con UV y escala consistente; lluvia/decal suciedad solo donde tenga sentido; texturas de granito no repetidas de forma evidente; materiales específicos para tapial y mampostería, teja, carpintería y metal. No confundir materiales posteriores con elementos medievales sin anotación. Las fachadas emblemáticas deben tener modularidad interior pero detalles propios.

### 7.3 Personajes y animaciones

Comenzar con modelos temporales licenciados y animaciones de prueba; sustituir después el protagonista y tres personajes principales. Para parkour, preferir una biblioteca pequeña, bien transicionada y con ajuste del movimiento sobre animaciones vistosas pero poco controlables. Valorar IK de manos y pies, root motion solo donde proporcione consistencia, eventos de animación testeados. El tiempo de animación 3D es un riesgo crítico; no depender de que Claude Code lo solucione escribiendo scripts.

### 7.4 Sonido y música

Diseño por capas: campanas/cuadros religiosos solo donde proceda, animales, voces, herrería, agua, tela, viento, pasos por piedra/madera/tierra, tensión de guardias, muralla. Investigar instrumentos, modos musicales y tradición local sin inventar una «banda sonora árabe genérica». Música original o librerías con licencia apta; nada de reutilizar melodías comerciales. Sonido direccional y variación por zona/hora. Mezcla de voz, FX y música independiente.

## 8. Arquitectura Unity

### 8.1 Versión y renderizado

- **Unity 6.3 LTS** fijado por `ProjectVersion.txt`, actualización únicamente por hito acordado. [R05]
- **Decisión abierta de render pipeline [RESEARCH]**: probar una escena representativa en **URP** y **HDRP** durante Fase 0 (fachada detallada, luz de día/noche, algunos personajes, cámara y GPU objetivo). URP favorece rango de hardware y simplicidad; HDRP puede ser apropiado si se fija PC de gama alta y calidad visual extrema. **Elegir uno tras benchmark, no mantener dos pipelines en paralelo.** Cambiar después es caro.
- Sistema de entrada oficial `Input System`; cámara `Cinemachine` si la versión compatible funciona en el proyecto; navegación `AI Navigation`/NavMesh y movimiento NPC encapsulado para no atarse a un único componente.
- Usar sistema de iluminación coherente con pipeline, perfilado en **build** real y presets de calidad.
- Dependencias: solo versiones de paquete compatibles con el editor fijado; registrar `Packages/manifest.json` y `packages-lock.json`.

### 8.2 Arquitectura orientativa

```text
Qazrix/
├── Assets/
│   └── _Qazrix/
│       ├── Art/{Architecture,Characters,Materials,Props,Textures,VFX}
│       ├── Audio/{Music,Ambience,SFX,Voice}
│       ├── Data/{Buildings,Dialogues,Guards,Missions,Zones}
│       ├── Prefabs/{Environment,Gameplay,NPC,UI}
│       ├── Scenes/{Bootstrap,Prototype,City_Z01,City_Z02,...,Interiors,Tests}
│       ├── Scripts/
│       │   ├── Core/{Events,Save,Time,State}
│       │   ├── Character/{Movement,Parkour,Camera}
│       │   ├── AI/{Perception,Patrol,Navigation,Alert}
│       │   ├── Gameplay/{Interactions,Missions,Dialogue}
│       │   ├── World/{Streaming,Zones,GeoReference}
│       │   ├── UI/
│       │   └── Editor/{Importers,Validation,BuildTools}
│       ├── Settings/
│       └── Tests/{EditMode,PlayMode}
├── Packages/
├── ProjectSettings/
├── Research/{Sources,Survey,Datasets,ReferenceBoards}
├── Design/{Narrative,World,Missions,Art,Decisions}
├── Tools/{GIS,Blender,Validation}
├── Reports/{Performance,Builds,QA}
├── Documentation/
├── CLAUDE.md
├── README.md
└── .gitignore
```

La estructura es una **propuesta**, no un requisito de generar decenas de carpetas vacías. `Assets`, `Packages` y `ProjectSettings` están versionados; `Library`, `Temp`, `Logs`, `Obj`, builds temporales y cachés quedan fuera del repositorio. `.meta` visibles y serialización de assets en texto. Git LFS para archivos binarios grandes con filtros definidos y probados.

### 8.3 Principios de implementación

- C# claro y testeable; composición sobre herencia profunda; dependencias explícitas; eventos tipados o interfaces en lugar de `FindObjectOfType` disperso.
- Separar **simulación** (lógica de misiones, estados, detección abstracta) de **vista Unity** (Animator, NavMesh, GameObject). Tests de EditMode para las reglas.
- `ScriptableObjects` para configuraciones y definiciones; evitar usarlos como estado de partida mutable compartido accidentalmente.
- `Assembly Definition Files` por módulos cuando mejore tiempos de compilación y tests, no multiplicarlos innecesariamente.
- Identificadores persistentes estables para entidades y misiones; migración o rechazo explícito de saves incompatibles.
- Herramientas de debug: FPS/frametime, estado de guardia, último estímulo, sensores, zona, misión, superficies de parkour y origen GIS.
- No «mega manager» ni service locator global sin un motivo documentado.
- Evitar paquetes pagos o dependencias externas sin autorización expresa; establecer su coste y términos antes.

### 8.4 Carga de mundo

Para el prototipo, una sola escena pequeña. Al crecer: `Bootstrap` persistente + escenas aditivas por sector, geometría/iluminación/navegación separables, activación por distancia o portales, con pruebas de continuidad. No implementar streaming sofisticado antes de medir necesidad. Rutas de patrulla cruzando sectores, eventos y save/load deben funcionar con cargas parciales. Imposibilidad de caer fuera del mundo durante la carga.

### 8.5 Performance y calidad

**Objetivo provisional** (se recalibra tras benchmark Fase 0): **60 fps estables a 1080p** en un PC de referencia elegido y documentado por el equipo; sin definir hardware no se puede declarar que se cumple. Medir percentiles de *frametime* y picos, no solo promedio. Perfiles separados de día, noche, NPC, en cubierta elevada y durante streaming.

Presupuesto inicial de diseño, no promesa: optimizar draw calls, materiales y overdraw; LOD en edificios; atlas/trim sheets; occlusion culling cuando sea ventajoso; compactar colisiones; bakear lo estático cuando encaje con la iluminación; evitar `Update()` masivos y búsquedas de escena por frame. **No imponer números de triángulos o memoria sin dispositivo objetivo y medición.**

### 8.6 Automatización de editor y CI

- Métodos `Editor` para crear escenas de ejemplo, auditar assets, validar referencias de misiones, compilar/build y exportar informe de georreferenciación.
- Unity ejecutable por `-batchmode -projectPath ... -executeMethod ... -quit -logFile ...`, usando el binario existente real del editor; correr `-runTests -testPlatform editmode/playmode -testResults` según opciones soportadas por la versión elegida. [R06]
- Una herramienta `VerifyProject` debe fallar con código distinto de cero ante errores de compilación, GUID rotos, misiones inválidas o ausencia de escena principal; otro `BuildWindows` genera build reproducible.
- GitHub Actions/self-hosted runner o CI equivalente solo tras confirmar licencias y disponibilidad de Unity; nunca escribir secretos en el repositorio.
- Todos los cambios generados por herramientas Unity deben quedar revisables en Git; **no editar YAML de escenas/prefabs a mano cuando haya API Editor fiable**.

## 9. Recursos existentes y derechos de uso

### 9.1 Prioridad de investigación: datos de Cáceres

| Recurso | Qué sabemos | Valor | Estado de uso |
|---|---|---|---|
| **Visor 3D SIG Cáceres** [R07] | visor municipal con terreno y ciudad vieja texturizada | guía de morfología y fachadas | **solo visualización verificada**; no presuponer descarga ni reutilización |
| **Pilotos CNIG 2023 y 2024** [R08][R09] | campañas de fotogrametría y captura terrestre/LiDAR; la de 2024 contempla aplicaciones para videojuegos | fuente potencial de modelo real 3D | **contactar CNIG/Ayuntamiento** para disponibilidad, cobertura, formatos, permisos y costes |
| **Plan Director de la Muralla** [R10] | documentación y visor por tramos y enclaves | reconstrucción de muralla, torres y fases | consulta pública; verificar derechos de reutilización de planos y fotos |
| **Cáceres Histórica** [R03][R11] | fichas, época, ubicación y datos arquitectónicos | catálogo monumental y anacronismos | material de referencia, no asumir licencia de imágenes/textos |
| **Museo de Cáceres / Ministerio de Cultura** [R04] | aljibe del siglo XII bajo palacio reformado | modelado del alcázar/aljibe | fichas y posible colaboración, derechos gráficos a comprobar |
| **PNOA/CNIG: ortofoto, MDT, LiDAR** [R12] | catálogo público, descarga de muchos productos con reconocimiento y licencia compatible CC-BY 4.0 | planimetría, alturas, cubiertas y relieve | comprobar producto, ficha y licencia concretos |
| **Cartografía catastral** [R13] | posibilidad de consultar/descargar datos, con condiciones según modalidad | huellas/volúmenes de edificios | verificar autenticación, licencia, alcance y versiones |
| **OpenStreetMap** [R14] | datos abiertos ODbL, con atribución y obligaciones adicionales en determinados derivados | red peatonal/toponimia de apoyo | revisión de ODbL y de distribución, no mezclar automáticamente datos con otras licencias |

**Acción crítica de Fase 0:** contactar por escrito al SIG municipal y CNIG, explicar que se propone un videojuego Unity ambientado en 1229 sobre la Cáceres actual y solicitar: (a) modelo utilizable de alta resolución o acceso a derivados, (b) resolución/cobertura, (c) georreferenciación y texturas, (d) formatos posibles —OBJ, glTF, 3D Tiles, mallas y ortofotos—, (e) permiso de transformación, redistribución en build y uso comercial potencial, (f) créditos obligatorios y restricciones, (g) posibles datos de altura de tejados y planos históricos. **No prometer que existen esos formatos hasta recibir respuesta.**

### 9.2 Herramientas y materiales auxiliares

- **QGIS** para análisis/cartografía; **Blender** para modelado, UVs y optimización [R15].
- **GDAL/PDAL** cuando su instalación y formatos queden probados; scripts versionados y reproductibles.
- **Poly Haven** y **ambientCG** para HDRIs, texturas y algunos modelos bajo CC0, verificando la ficha de cada activo y guardando captura/URL/fecha de licencia [R16][R17].
- **Cesium for Unity** [R18] — opción de investigación si se entrega un modelo legalmente reutilizable en 3D Tiles o se requiere georreferenciación. **No añadir Cesium por defecto**: la ciudad es pequeña y una exportación estática optimizada puede resultar más sencilla y rápida.
- Unity Asset Store/Fab/otros catálogos: solo activos con licencia apta para incluir en un juego distribuido; no presumir que «gratis» significa libre de atribución o de redistribución en proyecto fuente.
- Fotografías propias de referencia tomadas desde espacios permitidos; solicitar permisos para interiores o capturas especiales. No hacer vuelos de dron sin cumplir regulación y autorizaciones.

### 9.3 Registro obligatorio de licencias

Archivo `Research/ASSET_LICENSES.csv` con `asset_id`, nombre, autor/proveedor, fuente URL, fecha, versión, tipo de activo, licencia, texto de atribución, restricciones, estado de verificación, ruta en repositorio y persona que lo aprobó. Ningún activo entra a una build pública sin registro. Diferenciar **usar una fotografía para observar** de **incluir sus píxeles o reconstruir un modelo derivado de un dataset restringido**. Evitar extracción no autorizada de Google Maps/Street View, visores SIG, modelos comerciales o plataformas de fotogrametría.

## 10. Plan de ejecución incremental: gates, no fechas ciegas

Los plazos orientativos dependen de experiencia, presupuesto, recursos gráficos y acceso a datos. Asumir **equipo pequeño** con Claude Code como asistente de programación, no como sustituto de arte 3D y testing. No iniciar la fase siguiente si no se supera el gate previo.

### Fase 0 — Investigación y decisiones críticas (1–3 semanas orientativas)

**Objetivo:** eliminar incertidumbre que puede invalidar todo el proyecto antes de construir el mundo.

**Tareas:** instalar y fijar Unity; investigar renderer URP/HDRP con microbenchmark; abrir repositorio; auditar fuentes, licencias y descarga; escribir a CNIG/SIG; definir hardware objetivo; recopilar 20–30 puntos de control y fotos geolocalizadas; diseñar formato GIS local; proponer concepto visual, guion y lista de riesgos.

**Entregables:** `README`, `CLAUDE.md`, matriz de fuentes/licencias, comparativa renderer con capturas y métricas, plano de ámbito, puntos GIS, arquitectura inicial, guion breve de vertical slice y `DecisionLog.md`.

**Gate F0:** proyecto Unity limpio abre y compila en máquina del desarrollador; prueba de material/luz/escena reproducible; ruta plausible hacia datos de geometría (con o sin CNIG); ninguna dependencia crítica sin licencia clara. Si no hay modelo CNIG, activar *Plan B* (PNOA/OSM + modelado modular); no bloquear indefinidamente.

### Fase 1 — Prototipo jugable sin ciudad real (2–4 semanas)

**Objetivo:** demostrar movimiento, cámara y sigilo con geometría gris (**graybox**) independiente del arte final.

**Tareas:** controlador third-person, camera collision, saltos, parkour contextual de un conjunto mínimo de obstáculos, NPC con patrulla y 4 estados de percepción, sistema de interacción, HUD simple, checkpoint, tests esenciales, build Windows.

**Gate F1:** circuito de 3–5 min jugable de principio a fin, sin fallos bloqueantes; al menos 20 repeticiones QA; detección coherente; build en PC ajeno al del autor; código testeado y registrado. **No pasar a modelar toda Cáceres** solo porque haya movimiento básico.

### Fase 2 — «Cáceres reconocible» en graybox georreferenciado (2–6 semanas)

**Objetivo:** demostrar que las calles y desniveles **son realmente Cáceres**.

**Tareas:** pipeline QGIS → Blender → Unity, georreferenciación, relieve, muralla/volúmenes y callejero de Z01–Z04 con enfoque en sector demo; comparar puntos de control; primeras torres y recorridos; rutas de jugador y guardias; visor de diferencias, importador idempotente.

**Gate F2:** recorrido urbano reconocible desde Plaza Mayor hacia Santa María/San Jorge; errores horizontales medidos contra puntos de control; ausencia de calles imposibles, huecos de suelo o colisiones falsas; 3 vistas comparadas con referencias y revisión por una persona que conozca Cáceres. Aceptación explícita de qué partes son anacrónicas.

### Fase 3 — Vertical slice de calidad (6–12+ semanas; puede ser el tramo más largo)

**Objetivo:** primera experiencia **publicable como demo** de 15–20 min en un sector pequeño de Cáceres.

**Tareas:** high-quality hero assets Z01 y ruta elegida, PBR, iluminación día/noche, ambiente, dos rutas de infiltración, diálogo, misión completa, guardias, UI, checkpoints, sonido, tutorial contextual, modo paseo en el sector y optimización.

**Gate F3:** demo jugable por tres personas externas sin ayuda; cada una completa la misión; dos rutas reales; no bugs bloqueantes; ausencia de *placeholder art* visible en los puntos focales acordados; capturas comparables con referencias; FPS/frametime registrados sobre hardware acordado; créditos y licencias auditados. **Este es el primer gran objetivo del proyecto.**

### Fase 4 — Ciudad 1.0 y paseo libre (8–20+ semanas, muy variable)

**Objetivo:** ciudad intramuros completa en exteriores, calidad coherente; modo paseo terminado.

**Tareas:** modelado progresivo Z02–Z06 por prioridad de cámaras y rutas; escenarios de fondo; torres/adarves jugables seleccionados; sistema de zonas si es necesario; mini mapa; puntos de interés y fichas; ambientación; benchmarks y regresión.

**Gate F4:** red continua de calles con orientación/altitudes plausibles; todos los puntos de control inspeccionados; ningún vacío de geometría alcanzable; rendimiento estable y calidad visual coherente en rutas críticas; paseo libre sin misiones ni agresiones.

### Fase 5 — Campaña narrativa completa (8–20+ semanas, variable)

**Objetivo:** implementar Q00–Q09 con actos, decisiones y tres finales, reutilizando infraestructura del vertical slice.

**Tareas:** misiones y diálogos data-driven, interiores limitados, personajes principales y animaciones definitivas, variantes de asalto, epílogo sobrenatural, combate mínimo, cinemáticas cortas y localización.

**Gate F5:** ruta completa sin bloqueos; tests automatizados de variables y finales; cada decisión importante tiene consecuencia comprobable; los tres finales alcanzables y distinguidos; ningún objetivo produce save irreparable.

### Fase 6 — Calidad, optimización y entrega (4–8+ semanas)

**Objetivo:** demo avanzada o release candidate según presupuesto.

**Tareas:** profiling en builds de distribución, QA de colisiones/streaming, revisión histórica y cultural, accesibilidad, subtítulos, localización, controles, instalador/paquetización, licencias, pruebas de estabilidad y marketing honesto.

**Gate F6:** checklist de lanzamiento firmado; cero bloqueantes, métricas publicadas internamente, derechos aclarados, documentación y build reproducible, registro de problemas conocidos.

### Reglas de priorización

- **No modelar más de un sector en calidad final mientras fallan los gates de movimiento o fidelidad.**
- Cada fase entrega **algo ejecutable** y una captura/vídeo, no solo documentos.
- En cada incremento: *plan pequeño → implementación → pruebas → ejecución real → captura/perfil → revisión → commit*.
- Primer hito de éxito comercial/creativo = **F3**, no F6. Si la demo no cautiva, revisar el concepto antes de multiplicar producción.
- Si no se consiguen activos municipales reutilizables, el pipeline B de GIS + modelado manual debe ser viable desde F0.

## 11. Priorización MoSCoW por versión

| Función | F1 prototipo | F3 demo | F4 ciudad | F5 campaña |
|---|---|---|---|---|
| Movimiento/cámara | MUST | refinado | refinado | refinado |
| Trepar y parkour contextual | básico | MUST | ampliado | ampliado |
| Sigilo con guardias | básico | MUST | escalado | ampliado |
| Geometría real Cáceres | no | MUST sector | MUST casco | mantener |
| Arquitectura hero/PBR | no | MUST sector | MUST hitos | mantener |
| Modo paseo | no | MUST sector | MUST ciudad | mantener |
| Misiones y diálogos | prototipo | 1 misión | mantener | MUST 10 misiones objetivo |
| Combate | opcional | mínimo/evitable | igual | MUST limitado |
| Interiores | no | 1 pequeño | opcional | 3–4 narrativos |
| Tres finales | no | semilla | no | MUST |
| Multijugador/caballos/mercado abierto | NO | NO | NO | NO |

## 12. Riesgos y mitigaciones

| Riesgo | Probabilidad / impacto | Mitigación y condición de parada |
|---|---|---|
| Malla 3D oficial no descargable o sin derecho de uso | alta incertidumbre / muy alto | consulta F0; Plan B PNOA + GIS + modelos propios; no realizar extracción no autorizada |
| Gran parte de la arquitectura actual es posterior a 1229 | seguro / medio | contrato de autenticidad y fichas históricas; mapa real + ambientación ficticia reconocida |
| Magnitud del modelado 3D | alta / muy alto | vertical slice, modularidad + hero assets, presupuesto explícito de arte y colaboradores externos |
| Parkour procedural fracasa en geometría irregular | media-alta / alto | sistema contextual, authoring manual de rutas, pruebas de 20 recorridos |
| Rendimiento de fotogrametría/vegetación | alta si se importa en bruto / alto | decimación, retopología, LOD, colisiones simplificadas, perf gates |
| IA de guardias errática en desniveles | media / alto | estados simples, navegación por sector, zonas exclusión y QA reproducible |
| Historia ramificada se vuelve inmanejable | media / alto | 3 estados y finales diseñados sobre misiones compartidas |
| Claude Code crea código no integrable en Unity | media / alto | scripts Editor reproducibles, compilación real, PlayMode, reports, no declarar éxito sin evidencia |
| Recursos con licencia insuficiente | media / muy alto | inventario, auditoría y sustitución obligatoria antes de builds públicas |
| Se confunde la experiencia con un Assassin's Creed comercial | media / medio | originalidad narrativa/artística; escala honesta, UI y personajes propios |
| Falta tiempo o dinero para acabar | alta / alto | F3 como producto presentable; fases posteriores opcionales |

## 13. Estrategia de pruebas y aceptación

### 13.1 Pirámide de pruebas

- **Unit/EditMode:** evaluación de misión/finales, transiciones de detección, estados de guardias, conversión GIS local, interpolación de movimiento puro, migración de guardado, validador de assets.
- **PlayMode:** escena mínima de desplazamiento y colisiones, línea de visión, interacciones, checkpoint, carga de sectores.
- **Pruebas de editor:** creación idempotente de escenas y prefabs; importador GIS con fixtures pequeños; validación de referencias.
- **Playtests manuales:** recorridos grabados, uso de mando, claridad de objetivos, calidad del movimiento y recogida de bugs.
- **Golden screenshots:** cámaras fijas en puntos de control comparadas con referencias permitidas; no confiar en pixel-perfect por iluminación/perspectiva.
- **Performance:** capturas profiler y frametimes de build, documentando hardware, preset, resolución y ubicación.

### 13.2 Definition of Done para cada tarea

Una tarea solo está terminada si: (1) cambios integrados y versionados, (2) Unity abre/compila sin errores, (3) pasa tests relevantes o enumera los no ejecutados, (4) existe demostración visual/manual del comportamiento, (5) no se añaden assets con licencia desconocida, (6) README o docs actualizados si cambió el flujo, (7) se documentan riesgos y limitaciones. Si el agente no dispone de Unity, debe marcar **«generado, pendiente de verificación en Editor»**, jamás «funciona».

### 13.3 Condiciones objetivas para la fidelidad de Cáceres

- `GroundControlPoints.csv` trazable y validado; revisión manual de puntos incoherentes.
- Itinerario turístico/geográfico del vertical slice sin teletransportes ni atajos imposibles.
- Vista elevada del sector y dos vistas a pie de calle confrontadas con referencias fechadas.
- Arquitectura principal identificable por observadores locales sin rótulos.
- Elevaciones/pendientes coherentes; ningún lugar real importante desplazado intencionadamente **sin** una anotación artística.
- Revisión del catálogo de anacronismos antes de publicar imágenes con pretensión histórica.

## 14. Reglas de trabajo con Claude Code

Claude Code debe actuar como **programador y asistente técnico**, no afirmar que puede fabricar por sí solo una reconstrucción artística excelente. Encargo a la herramienta: generar C#, tests, importadores, automatización, prototipos, documentación y scripts de Blender/QGIS cuando proceda; solicitar revisión humana en arte, historia y diseño.

**Contrato de autonomía:**

1. Antes de cambiar código, leer `QAZRIX_SPEC_v1.md`, `CLAUDE.md`, `README.md`, `Design/DecisionLog.md` y el estado del proyecto. No comenzar la campaña completa sin orden de fase.
2. Inspeccionar archivos existentes y versión real de Unity. Nunca crear un segundo proyecto sin autorización.
3. Seleccionar **una sola tarea o corte vertical pequeño** que avance el gate actual; describir cambio, tests y criterios de aceptación.
4. Implementar el mínimo necesario, manteniendo el repositorio compilable. Evitar refactors ajenos al objetivo.
5. Ejecutar tests/build si existe acceso a Unity. Registrar comandos, versiones, resumen de logs y fallos. Si no existe, producir pasos concretos para verificación humana.
6. Los `.unity`, `.prefab`, materiales y referencias complejas se crean, en lo posible, mediante herramientas Editor y se guardan en Unity. Prohibido inventar GUIDs o declarar escenas válidas sin abrirlas.
7. Assets: no descargar/importar de fuentes con derechos dudosos y no automatizar scraping de visores restringidos.
8. Añadir una línea a `Design/DecisionLog.md` para decisiones estructurales y cambios de alcance.
9. Al finalizar cada iteración, entregar **qué se cambió, cómo probarlo, pruebas ejecutadas, evidencia, pendientes y próximo paso recomendado**.
10. Si el desarrollo requiere una decisión irreparable de diseño, licencia, coste o recurso externo, explicar alternativas y parar **esa decisión**; seguir con tareas no bloqueadas.

### 14.1 Formato del informe de cada entrega

```markdown
## Incremento <ID>: <título>
- Objetivo/gate:
- Cambios de código/escenas/assets:
- Comandos ejecutados:
- Pruebas: aprobadas / fallidas / no ejecutadas (por qué):
- Build y plataforma:
- Evidencia: capturas, logs y recorridos:
- Derechos/licencias de activos nuevos:
- Decisiones tomadas:
- Problemas conocidos:
- Próximo paso más pequeño:
```

### 14.2 Primer trabajo exacto que debe hacer Claude Code

**No desarrollar toda la especificación en una sola sesión.** En la primera sesión debe realizar **F0-A: arranque técnico e inventario**, con entregables:

1. Revisar entorno y detectar si Unity 6.3 LTS, Git, Git LFS, Blender y QGIS están disponibles (sin instalarlos o modificar el equipo sin permiso).
2. Crear/adecuar repositorio Unity mínimo, `.gitignore`, `.gitattributes` LFS, `README`, `CLAUDE.md` y estructura necesaria.
3. Compilar una escena `Prototype` mínima con suelo, cápsula y cámara. Mostrar controles, versión de Editor y pipeline.
4. Crear una herramienta Editor `VerifyProject` que compruebe existencia de escena y configuraciones esenciales, junto con guía para ejecución headless.
5. Incorporar 2–3 tests de lógica independientes de assets.
6. Crear `Research/Datasets.md`, `Research/ASSET_LICENSES.csv` y `Design/DecisionLog.md`, con fuentes ya conocidas y columnas pendientes.
7. Documentar decisión provisional de pipeline y plan de prueba URP/HDRP, **sin** iniciar modelado de Cáceres ni comprar assets.
8. Entregar informe conforme al formato anterior; cualquier operación no verificable debe indicarse expresamente.

### 14.3 Segunda iteración sugerida

**F0-B: prueba geoespacial.** Seleccionar una pequeña zona real Z01, obtener datos descargables autorizados, transformar a coordenadas locales, importar un relieve y tres volúmenes simples, demostrar 3 puntos de control y exportar errores/avisos. Mantener el resultado simple y reproducible, sin ornamentación. Después comenzar F1.

## 15. Presupuesto, roles y estrategia realista

- **Dirección de producto y narrativa:** decide alcance, finales, experiencia y aceptación.
- **Desarrollo Unity/C#:** sistemas, herramientas, test/CI, builds y performance; Claude Code acelera este rol, pero necesita supervisión.
- **Artista 3D / artista técnico:** medición, escultura/modelado arquitectónico, retopología, UV, texturas, rigging y animación. Probablemente la **principal carga de trabajo** del proyecto.
- **Historiador/arqueólogo local o especialista de patrimonio:** revisión selectiva de hechos, representaciones, toponimia y anacronismos.
- **Diseñador de niveles:** adapta la geografía exacta a rutas emocionantes sin cambiar monumentos arbitrariamente.
- **Audio y QA:** pueden entrar por hitos, pero deben tener presupuesto/tiempo reservado.

Tres escenarios viables:

1. **Experimento de aficionado:** F0–F2, ciudad graybox y movimiento; algunas decenas/centenares de horas de equipo, sin prometer resultado comercial.
2. **Demo muy cuidada:** F0–F3 con 1–2 desarrolladores y colaboración 3D; probablemente meses, no días.
3. **Juego completo:** F0–F6, colaboración artística sostenida y testeo; calendario y coste se estiman **después** de la vertical slice.

No asignar euro-costes sin conocer equipo, licencias, artistas y horas. El presupuesto debe contener costes explícitos de herramientas, assets, trabajo 3D, audio, QA y posibles autorizaciones.

## 16. Registro de decisiones iniciales

| ID | Decisión | Estado |
|---|---|---|
| D01 | Inspiración narrativa: Mansaborá, Cáceres, 1229 | **aprobada para especificar** |
| D02 | Reconstrucción geográfica del casco actual, ambientación de 1229 | **pilar esencial** |
| D03 | PC Windows, single player, tercera persona | **propuesta inicial** |
| D04 | Historia con agencia de Zahra y tres finales | **propuesta de guion**, sujeta a revisión |
| D05 | Unity 6.3 LTS, versión parche fija | **propuesta técnica** |
| D06 | URP o HDRP | **pendiente benchmark F0** |
| D07 | Aprovechar modelo 3D CNIG | **pendiente disponibilidad/licencia** |
| D08 | Primer objetivo de producción = vertical slice Z01 | **recomendación firme** |
| D09 | Parkour contextual por rutas, no escalada universal | **propuesta técnica** |
| D10 | Modo paseo como función esencial | **recomendación firme** |

## 17. Preguntas abiertas que NO bloquean el inicio

- ¿Qué GPU/CPU y resolución se usarán para aceptación de 60 fps? **Resolver en F0.**
- ¿Quién realizará arquitectura 3D, personajes y animaciones? **Resolver antes de F3.**
- ¿Proporcionará CNIG/Ayuntamiento modelos, mallas o solo referencias? **Investigar F0, con Plan B.**
- ¿Será la historia más realista o habrá presagios sobrenaturales antes del epílogo? **Probar en el guion F3.**
- ¿El título comercial será Qazrix o «Qazrix: La Mansaborá»? **Sin bloquear producción.**
- ¿Habrá combate jugable en vertical slice o solo infiltración? **Decidir tras F1.**
- ¿Qué accesos reales merecen documentación fotográfica de campo? **Decidir con mapa F2.**

## 18. Fuentes verificadas y pistas de investigación

**Consulta web realizada el 9 de noviembre de 2026.** Una fuente de referencia visual no equivale a una licencia de importación de activos. Esta lista registra hechos, no atribuye a terceros la autoría del diseño del videojuego.

- **[R01]** Extremadura Misteriosa, *La leyenda de la Mansaborá*: https://www.extremaduramisteriosa.com/mansabora-la-doncella-encantada-de-caceres
- **[R02]** UNESCO, *Old Town of Cáceres* (extensión, muralla, épocas): https://whc.unesco.org/en/list/384/
- **[R03]** Cáceres Histórica, *Arco de la Estrella*: https://cacereshistorica.caceres.es/patrimonio-historico/edificios-historicos/arquitectura-civil/arco-de-la-estrella-o-puerta-nueva/
- **[R04]** Ministerio de Cultura, *Casa de las Veletas* (aljibe y fases constructivas): https://www.cultura.gob.es/cultura/areas/museos/mc/arquitectura-museos/museos-gestion-transferida/extremadura/70-caceres/70a-casa-veletas.html
- **[R05]** Unity, soporte y versiones Unity 6: https://unity.com/releases/unity-6/support
- **[R06]** Unity Manual, argumentos de línea de comandos (verificar manual exacto de versión): https://docs.unity3d.com/6000.1/Documentation/Manual/CommandLineArguments.html
- **[R07]** Ayuntamiento de Cáceres, visor municipal 3D: https://sig.caceres.es/servicios/visores/visor-3d/
- **[R08]** Ayuntamiento, iniciativa 3D en 2023: https://www.ayto-caceres.es/noticias/caceres-primera-ciudad-patrimonio-de-la-humanidad-contar-recreacion-3d-recinto-intramuros/
- **[R09]** IGN-CNIG, boletín noviembre de 2024, *Proyecto piloto de escena 3D del casco histórico de Cáceres*: https://www.ign.es/web/resources/acercaDe/Boletines/Boletin_IGN_nov_2024.pdf
- **[R10]** Ayuntamiento, Plan Director de la Muralla: https://sig.caceres.es/patrimonio/pdmuralla/ y visor https://sig.caceres.es/visores/PD_Muralla_Caceres/
- **[R11]** Cáceres Histórica, *Torre de Bujaco*: https://cacereshistorica.caceres.es/patrimonio-historico/edificios-historicos/arquitectura-militar/torre-de-bujaco/
- **[R12]** CNIG, Centro de Descargas, políticas de licencia y productos PNOA: https://centrodedescargas.cnig.es/CentroDescargas/home
- **[R13]** Sede del Catastro, difusión de datos: https://www.sedecatastro.gob.es/Accesos/SECAccDescargaDatos.aspx
- **[R14]** OpenStreetMap, copyright/ODbL y requisitos de atribución: https://www.openstreetmap.org/copyright/es y https://osmfoundation.org/wiki/Attribution_Guidelines
- **[R15]** Blender: https://www.blender.org/about/ ; QGIS: https://qgis.org/
- **[R16]** Poly Haven, licencia CC0: https://polyhaven.com/license
- **[R17]** ambientCG, licencia CC0: https://docs.ambientcg.com/license/
- **[R18]** Cesium for Unity (opcional): https://cesium.com/learn/unity y referencia técnica https://cesium.com/learn/cesium-unity/ref-doc/
- **[R19]** Ministerio de Cultura, panorama histórico: https://www.cultura.gob.es/cultura/areas/patrimonio/mc/patrimoniomundial/bienes-declarados/por-ano-de-inscripcion/1986/caceres.html
- **[R20]** Ayuntamiento, visor SIG cartográfico: https://visor.caceres.es/visor?map=18

**Comprobaciones pendientes prioritarias:** permisos de acceso/descarga y distribución de la escena CNIG de alta resolución; derechos de reutilización de planos del Plan Director; selección de ficheros PNOA específicos que cubran el recinto; idoneidad de los datos de Catastro para uso de juego; status histórico del pasadizo y consulta a especialista local.

---

## Apéndice A. Recorrido de aceptación visual del sector piloto

Crear una pequeña batería de vistas de cámara fija (coordenadas finales se miden en F2):

- **A1:** vista desde Plaza Mayor hacia Bujaco/muralla.
- **A2:** aproximación al acceso principal del casco antiguo, con nota del anacronismo del Arco de la Estrella.
- **A3:** transición hacia Plaza de Santa María.
- **A4:** desniveles hacia San Jorge / Cuesta de la Compañía.
- **A5:** vista de cubiertas y adarves desde un punto elevado jugable.
- **A6:** aproximación al entorno de las Veletas, con cartela que diferencia palacio actual, antiguo alcázar y aljibe.

Para cada vista, capturar `reference`, `unity_day`, `unity_night`, `notes`, `distance_error_if_relevant` y una versión de assets usada. El objetivo es **reconocimiento verificable**, no coincidencia fotográfica exacta.

## Apéndice B. Ejemplos de criterios de aceptación expresados en pruebas

```text
Given un guardia en Patrol y el jugador detrás de una pared opaca,
When el jugador entra en el cono geométrico del guardia sin línea de visión,
Then el guardia no pasa a Alert por visión.

Given una misión con PassageKnowledge=Concealed y ZahraTrust=High,
When se carga un save válido,
Then las decisiones, indicadores y objetivos activos se conservan.

Given un punto del fichero de control SIG,
When se convierte desde la proyección autorizada a coordenadas locales,
Then el resultado coincide con el valor de prueba dentro del error definido.

Given la misión de demo completada,
When se selecciona Paseo por Qazrix,
Then se puede recorrer el sector sin alarmas ni objetivos obligatorios.
```

## Apéndice C. Qué significa «éxito»

**No es éxito:** 40 scripts compilando pero sin escena jugable; una ciudad preciosa con colisiones inutilizables; un visor 3D externo mostrado como si estuviese integrado; una captura de Cáceres actual presentada como modelo propio; una demo de Unreal/Unity con edificios medievales genéricos y el texto «Qazrix».

**Sí es éxito en F3:** una persona reconoce Cáceres, disfruta moviéndose por sus calles y cubiertas, completa una infiltración que depende de la geometría real y termina queriendo descubrir el secreto de Zahra. Ese es el momento de decidir si merece la pena construir el resto.
