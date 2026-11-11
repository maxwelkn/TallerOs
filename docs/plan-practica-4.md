# Práctica 4: documentos, Docker y API externa

## Alcance aprobado

Implementar RF-DOC-01 a 05, ejecutar el proyecto desde un clon mediante `docker compose up --build`, conservar datos y documentos fuera de los contenedores, integrar una API externa con clave y repetir la revisión acumulada. La fuente académica es `../Recursos/Practica4_ProyectoContenedor_PIII.docx.pdf` (4 páginas); se complementa con `../Recursos/Requerimientos_Core_PIII.pdf` para los atributos mínimos y requisitos transversales. Son materiales de referencia, no autorización para adelantar otras unidades.

Este archivo registra el plan aprobado, no certifica que sus funcionalidades estén implementadas. Los resultados reales se incorporan a [verificación de Práctica 4](verificacion-practica-4.md). La entrega no está lista mientras existan criterios pendientes.

## Matriz de trazabilidad

| Fuente y criterio | Peso | Propuesta | Comprobación requerida |
| --- | --- | --- | --- |
| Práctica, §1.1 y rúbrica: RF-DOC-01 a 04 | 2.0 | Componente Documentos con subida, listado, descarga y borrado lógico. | Metadatos completos; propietario y Administrador autorizados; usuario ajeno rechazado; registro y archivo conservados tras borrar lógicamente. |
| Práctica, §1.2 y rúbrica: contenedor | 1.5 | Dockerfile, .dockerignore, docker-compose.yml y sección Contenedor. | Desde clon limpio, copiar .env.example y arrancar con un comando; dirección y detención reproducibles. |
| Práctica, §1.3 y rúbrica: RF-DOC-05 y RD-09 | 1.0 | Volúmenes nombrados separados para SQLite y documentos. | `down` sin `-v`, nuevo arranque, login, listado y descarga con bytes idénticos. |
| Práctica, §1.4 y rúbrica: API externa, RD-07/08/10 | 1.5 | Revisión de entrega que interpreta clima real; secreto solo en entorno de ejecución. | Éxito real sin publicar clave, rechazo controlado ante indisponibilidad y auditoría de historial e imagen. |
| Práctica, §1.5 y rúbrica: revisión acumulada | 2.0 | Preservar acceso, permisos y taller. | Registro/activación, login/logout, rechazo administrativo, recuperación, permisos y operación/transición del negocio desde el README. |

## Diseño y límites

### Documentos del Core

Domain protege el estado de Documento; Application coordina casos de uso, entrega DTO y declara los contratos de metadatos y archivos. El nuevo proyecto Infrastructure implementará EF Core y disco. API validará el transporte y traducirá resultados. El componente obtendrá ID y rol de `ICurrentActor`; no consultará tablas de acceso ni tipos del taller.

Usará un contexto y un archivo SQLite propios dentro del volumen de datos, sin reorganizar el contexto heredado. Se aplicará una migración nueva para sus tablas y se preservarán las bases anteriores. Sus metadatos contendrán ID, nombre, tipo declarado, tamaño real, fecha UTC, usuario, identificador interno de almacenamiento y marca de eliminado. Los DTO no expondrán rutas físicas.

Rutas propuestas, todas registradas en `AccessAuthorization` y con sesión válida:

| Método y ruta | Entrada o resultado |
| --- | --- |
| `POST /api/documents` | Multipart con un archivo; devuelve metadatos. |
| `GET /api/documents` | Lista solo visibles y no eliminados. |
| `GET /api/documents/{id}/download` | Bytes para propietario o Administrador. |
| `DELETE /api/documents/{id}` | Borrado lógico para propietario o Administrador. |

Se rechazarán archivo ausente, vacío o mayor de 10 MiB. El nombre recibido nunca será una ruta de almacenamiento. Un eliminado no se podrá descargar ni aparecerá en listados, incluido el del Administrador. Si falla guardar los metadatos de una subida nueva, se intentará retirar solo su archivo recién creado. Los fallos esperables de disco tendrán respuestas controladas.

### Contenedores y correo de demostración

Dockerfile multietapa con imágenes oficiales .NET 10: SDK para construir, runtime ASP.NET para ejecutar con usuario sin privilegios. El contexto excluirá Git, bases, secretos, compilaciones y archivos personales.

Compose arrancará la aplicación, Mailpit interno y una inicialización de duración limitada. La inicialización aplicará migraciones y creará el Administrador ficticio antes de arrancar la API. Solo la aplicación publicará un puerto, `http://localhost:5000`. Mailpit se localizará por nombre dentro de la red.

SQLite se conserva porque es un motor embebido, no un servidor independiente. El enunciado habla de otro servicio para el motor; esta es la interpretación técnica acordada, no una aclaración obtenida del profesor. Habrá volúmenes nombrados separados para datos y archivos. No se borrarán bases habituales ni se usará `down -v` para la comprobación de persistencia.

`.env.example` contendrá valores ficticios suficientes para arrancar. La clave de ejemplo producirá un rechazo controlado. La inicialización del Administrador será solo de demostración y no sobrescribirá uno existente. Las credenciales reales no se incorporarán al código, construcción, imagen o historial.

`send-mail` seguirá siendo un comando independiente que usa la misma imagen y almacenamiento. El modo de demostración permitirá SMTP interno sin TLS ni autenticación mediante configuración explícita; el proveedor real conservará su configuración segura. Dos rutas administrativas, habilitadas solo en demostración, permitirán listar y leer mensajes recibidos en Mailpit mediante un adaptador HTTP. No habrá reenvíos externos durante las pruebas autorizadas.

### Revisión climática del negocio

`GET /api/workshop/orders/{id}/delivery-review` exigirá el acceso de las consultas de órdenes y estado `InRepair`; otro estado producirá 409. Un caso de uso obtendrá datos mediante un puerto de Application implementado con HTTP en Infrastructure. El Core no conocerá órdenes.

WeatherAPI recibirá la ubicación `WORKSHOP_LOCATION` (ejemplo: Santo Domingo) y la clave `WEATHER_API_KEY`, solo en ejecución. El resultado combinará orden, lugar, hora de observación, precipitación y recomendación. Si la precipitación actual es mayor de cero se recomendará coordinar bajo techo; de otro modo se indicará que no se observa precipitación. No altera la máquina de estados, confirma entrega ni garantiza seguridad vial.

Ausencia de clave, clave inválida, cuota, error de red, respuesta inválida o espera de más de 10 segundos producirán 503 con mensaje comprensible. Las otras operaciones seguirán disponibles. Ni URLs con clave ni mensajes del proveedor con secretos se registrarán o devolverán al cliente. La evidencia positiva requiere una clave real en el .env local; no se sustituirá por una respuesta inventada.

## Orden de incrementos y verificación

1. **Fundamentos y entorno:** teoría inicial externa, rama `feature/practica-4-contenedor`, build y pruebas de base, Docker Desktop/WSL 2 y ejecución de un contenedor Linux.
2. **Documentos:** preparar su teoría; dominio, aplicación, persistencia y HTTP; pruebas aisladas y HTTP con dos Estándar y un Administrador; comprobar borrado lógico y fallos de archivos.
3. **Composición y persistencia:** preparar teoría de Compose y volúmenes; construcción multietapa, inicialización, buzón local y documentación; verificar arranque limpio y recreación con datos persistentes.
4. **Clima:** preparar teoría HTTP y adaptadores; operación del negocio y tratamiento de fallos; dobles HTTP y consulta real con clave local.
5. **Evaluación final:** clon limpio, seis frentes acumulados, documentos, volúmenes, secreto, historial, capas de imagen y evidencia por criterio; preparar PR y entrega.

Después de cada incremento se completará el material con referencias y resultados reales, se revisará el diff y se publicarán commits de propósito único. Se pausará para revisión antes de iniciar el siguiente incremento. Documentación y código se separarán cuando sean cambios independientes. Las fusiones y la creación de `practica-4` requieren autorización expresa; no se moverán etiquetas anteriores.

Los cambios de código requieren `dotnet build TallerOs.slnx`, pruebas pertinentes y comprobaciones de comportamiento. Los cambios exclusivamente documentales requieren revisar contenido, rutas y `git diff --check`. La verificación desde Docker no se sustituye por una ejecución local de .NET.

## Estudio y exclusiones

El material personal se guarda fuera del repositorio en `../estudios/TallerOs/practica-4`. Su índice enlaza siete temas: rúbrica/arquitectura, Docker, Compose/secretos, persistencia, documentos, API externa y defensa. Cada tema se desarrolla antes de su incremento y se completa después, distinguiendo teoría, propuesta e implementación comprobada.

Se conserva la documentación previa. El cierre y consolidación de la unidad requieren declaración expresa del estudiante. No se interpreta una respuesta, un build o un push como cierre. El plan excluye React, presupuestos, RF-DOC-06, nuevas notificaciones, CI/CD, migración de motor y refactorización general. No cierra automáticamente la Práctica 3 ni el proyecto completo.

## Referencias oficiales

- [Docker: conceptos y arquitectura](https://docs.docker.com/get-started/docker-overview/).
- [Docker Desktop para Windows](https://docs.docker.com/desktop/setup/install/windows-install/).
- [Compose](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-docker-compose/).
- [Volúmenes](https://docs.docker.com/engine/storage/volumes/).
- [SQLite sin servidor](https://www.sqlite.org/serverless.html).
- [WeatherAPI](https://www.weatherapi.com/docs/).
- [API de Mailpit](https://mailpit.axllent.org/docs/api-v1/).
