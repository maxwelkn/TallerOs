# Bitácora de la Asignación 2

Fecha: 20 de octubre de 2026.

## Trabajo solicitado

Le pedí al agente analizar `Asignacion2_GestionPermisos_PIII.pdf` y trabajar el feature completo con la skill `feature-talleros`. Después de revisar su plan, lo aprobé. Antes de empezar, le proporcioné `Diseno_Componentes_PIII2.pptx` para reforzar la separación entre el Core y el módulo de negocio. Luego autoricé la implementación con «luz verde, empieza a trabajar de la mejor manera».

El alcance aprobado fue RF-GP-01 a RF-GP-07, persistencia, autorización del lado del servidor, pruebas unitarias con xUnit, instrucciones en el README y esta bitácora. Como decisión técnica, se eligió el permiso `WorkOrders.Read`: permite probar el efecto de una aprobación consultando órdenes, sin cambiar el rol del solicitante.

## Qué devolvió el agente

- Una matriz de requisitos con la rúbrica y un plan previo a los cambios.
- La entidad `PermissionRequest`, que comienza en `Pending` y encapsula las operaciones de aprobación, aplicación y rechazo. `PermissionRequestTransitions` concentra las transiciones permitidas.
- El caso de uso `PermissionRequestService`, separado de HTTP y EF Core; comprueba el actor, limita las consultas y coordina la resolución con la concesión.
- Contratos de identidad y permisos para que la pieza no consulte tablas de usuarios ni conozca entidades del taller.
- Adaptadores SQLite y una migración que agrega solicitudes y concesiones. La resolución y el permiso se guardan juntos; el estado original sirve para detectar resoluciones concurrentes.
- Rutas protegidas de solicitudes y una consulta de órdenes que exige el permiso. Un usuario Estándar sigue sin tener acceso a las operaciones de administración de usuarios.
- Una suite xUnit de 34 casos y un README con el comando de pruebas y la secuencia de comprobación de cada requisito.

La adaptación arquitectónica fue incremental: se agregaron `TallerOs.Domain` y `TallerOs.Application`; los adaptadores nuevos permanecen aislados dentro del proyecto API. El acceso anterior, SMTP y el modelo del taller no se migraron por completo. Las dos máquinas de estados siguen separadas.

## Errores detectados y corregidos durante la sesión

### Corregí el nombre de la rama creada por el agente

Habíamos acordado usar `feature/<tema>` para las ramas de funcionalidades. Al implementar esta asignación, el agente creó la rama local `codex/feature-permissions`, que no respetaba la convención que yo había pedido. Detecté el error cuando informó el nombre de la rama al presentar el trabajo realizado.

Le señalé la diferencia: «¿no quedamos que sería feature/... codex no va nunca?». El agente reconoció que había aplicado el prefijo predeterminado de su entorno en lugar de nuestra convención. Le indiqué que no debía usarlo y la rama se renombró a `feature/gestion-permisos`, conservando los cambios de la asignación. También se actualizó `AGENTS.md` para dejar explícita la prohibición de usar `codex/` y se comprobó el nombre con `git branch --show-current`.

La corrección ocurrió antes de publicar la rama en GitHub; no se había creado una rama remota con ese nombre. Este fue un error del agente que detecté y pedí corregir personalmente durante el trabajo de la Asignación 2.

### El comprobador esperaba el código incorrecto al registrar

El agente preparó una comprobación HTTP con usuarios ficticios y una base temporal. En el primer intento asumió que el registro devolvía 200. La comprobación se detuvo con este mensaje:

```text
AssertionError: registro ficticio: expected 200, received 201
```

Se revisó `src/TallerOs.Api/Access/AccessEndpoints.cs`, donde el registro exitoso usa `Results.Created`. El error estaba en la expectativa del comprobador. Se corrigió a 201, conservando el contrato existente de registro.

### El comprobador suponía que todas las respuestas eran JSON

En el siguiente intento, el comprobador quiso interpretar la activación como JSON y recibió:

```text
json.decoder.JSONDecodeError: Expecting value: line 1 column 1 (char 0)
```

La revisión del mismo archivo mostró que la activación exitosa usa `Results.Text`. Se cambió el comprobador para consultar `Content-Type` y procesar texto o JSON según la respuesta. Después de corregir ambas suposiciones, el recorrido HTTP terminó con 37 respuestas verificadas.

Estas detecciones las produjo la comprobación del agente durante la sesión; no se atribuyen a una revisión manual del estudiante. No se provocó un fallo artificial en la aplicación para completar la bitácora.

### Revisión de la calidad de las pruebas

La primera suite tenía 38 casos e incluía una prueba que comparaba la tabla de transiciones con una expresión equivalente a la implementación. Esa repetición aportaba menos evidencia que ejecutar las operaciones de la entidad. El agente la sustituyó por casos que intentan aprobar, rechazar o aplicar desde estados prohibidos y comprueban que el estado y los datos de resolución no cambian. También agregó casos de consulta y rechazo del servicio. La suite final tiene 34 casos.

## Comprobaciones realizadas por el agente

| Comprobación | Resultado observado |
| --- | --- |
| `dotnet build TallerOs.slnx` | Sin errores ni advertencias. |
| `dotnet test TallerOs.slnx` | 34 casos aprobados, ninguno fallido ni omitido. |
| Ejecución filtrada por cada método de prueba | 21 métodos ejecutados por separado, incluidas sus variantes de teoría; ninguno falló. |
| Copia temporal sin `bin`, `obj` ni archivos locales del IDE | El comando documentado restauró dependencias y aprobó los 34 casos; el build de la solución también pasó. No se presentó esta copia como un clon de una etiqueta publicada. |
| HTTP sobre una base temporal | 37 respuestas verificadas, con usuarios ficticios y una orden ficticia; incluyó creación, privacidad, aplicación inmediata, rechazo, doble aprobación, resolución propia y errores de entrada. |
| Reinicio de la API con la misma base temporal | Se conservaron solicitudes, motivos, permisos y sesiones válidas. |
| Migración sobre una base con el esquema anterior | Conservó el usuario ficticio existente y agregó las nuevas tablas. |
| Dos contextos resolviendo la misma solicitud | Se rechazó la resolución obsoleta y no quedó concedido su permiso. |
| Dos concesiones concurrentes del mismo permiso | La segunda guardada se rechazó; su solicitud quedó pendiente y no hubo concesiones duplicadas. |
| Correspondencia modelo/migraciones | `dotnet ef migrations has-pending-model-changes --project src/TallerOs.Api --no-build` informó que no hay cambios pendientes. |

Las comprobaciones HTTP y de almacenamiento fueron auxiliares locales, fuera de la suite unitaria. No enviaron correos: la activación de los usuarios ficticios se comprobó leyendo el enlace en la cola temporal. Los archivos auxiliares quedaron fuera del repositorio.

## Relación con los criterios

| Requisito | Evidencia |
| --- | --- |
| RF-GP-01 | Creación devuelve `Pending`, usando el solicitante autenticado. |
| RF-GP-02 | `/mine` limita resultados al actor y `/pending` exige Administrador. |
| RF-GP-03 | Aprobación registra responsable y fecha; la respuesta final es `Applied`. |
| RF-GP-04 | Motivo vacío produce 400 sin resolver; el rechazo válido conserva su motivo. |
| RF-GP-05 | La misma sesión obtiene 200 en la consulta de órdenes inmediatamente después de aprobar. |
| RF-GP-06 | Doble aprobación y aprobación de rechazada producen 409; las pruebas de dominio verifican operaciones prohibidas y conservación de datos. |
| RF-GP-07 | Un solicitante promovido a Administrador recibe 403 al aprobar o rechazar su propia solicitud. |
| RD-04/06/07/09/12 | Transiciones centralizadas, autorización en servidor, errores controlados, persistencia tras reinicio y suite sin iniciar la aplicación. |

## Estado y límites

Revisé la bitácora y pedí agregar la corrección del nombre de la rama como evidencia de mi intervención. Después autoricé trabajar en GitHub. Los cambios se organizaron en commits por propósito y la rama `feature/gestion-permisos` se publicó en `origin`, con el [PR #14](https://github.com/maxwelkn/TallerOs/pull/14).

La revisión previa a la integración comprobó el commit `7deb2b8` desde un clon limpio: 34 pruebas aprobadas, build sin errores ni advertencias y 37 respuestas HTTP verificadas. También se repitieron la comprobación de la migración y los conflictos concurrentes. GitHub no tenía CI configurado; las comprobaciones se ejecutaron localmente sobre el código publicado.

Tras recibir ese resultado, autoricé hacer el merge y terminar lo pendiente. Para cerrar la unidad se agregó la [guía de estudio](estudio-gestion-permisos.md). El cierre contempla conservar el historial al integrar, publicar `asignacion-2` y verificar un clon de esa etiqueta. El estado de la integración y la etiqueta se consulta en GitHub; este párrafo no sustituye esa comprobación. La entrega en Moodle corresponde al estudiante.

No se adelantaron notificaciones internas, auditoría completa, CI/CD ni pruebas de la máquina de estados del taller.

## Documentación técnica consultada

- [Pruebas unitarias con .NET y xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit): uso del proyecto de pruebas y `dotnet test`.
- [Conflictos de concurrencia en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency): tokens de concurrencia y rechazo de actualizaciones obsoletas.
- [Limitaciones de SQLite con EF Core](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations): límites del proveedor que afectan las decisiones de persistencia.
