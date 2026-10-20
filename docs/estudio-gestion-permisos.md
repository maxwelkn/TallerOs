# Guía de estudio: Gestión de permisos

## Objetivo

Comprender cómo una solicitud de acceso elevado se convierte en un permiso efectivo, manteniendo separados la identidad del usuario, la decisión administrativa y la acción del taller. La referencia práctica es RF-GP-01 a RF-GP-07 de la Asignación 2. Los pasos de ejecución están en el [README](../README.md#asignación-2-gestión-de-permisos); esta guía orienta la lectura y la investigación.

## Temas para investigar, en orden

| Tema | Qué debes poder explicar en este feature |
| --- | --- |
| Rol, permiso y actor | Por qué conceder `WorkOrders.Read` no convierte a un Estándar en Administrador y por qué el solicitante se toma de la sesión. |
| Entidad e invariantes | Qué identidad conserva una solicitud y qué estados o datos deben permanecer intactos cuando una operación se rechaza. |
| Máquina de estados | Por qué aprobar, aplicar y rechazar son operaciones distintas, aunque la aprobación HTTP termine directamente en `Applied`. |
| Fronteras de componentes y capas | Qué pertenece a Gestión de permisos, qué ofrece Control de acceso y dónde empieza la operación propia del taller. |
| Persistencia y atomicidad | Qué ocurriría si se guardara la resolución sin conceder el permiso, o se concediera sin guardar la resolución. |
| Concurrencia optimista | Cómo se detecta que otro administrador ya resolvió la solicitud y qué garantiza el índice único de concesiones. |
| Pruebas de comportamiento | Por qué comprobar una excepción no basta para demostrar que una transición prohibida conserva el estado. |

## Recorrido del código

Lee los archivos siguiendo el flujo y trata de explicar cada paso antes de avanzar:

1. [AccessAuthorization.cs](../src/TallerOs.Api/Access/AccessAuthorization.cs): encuentra las rutas declaradas, la validación de sesión y la comprobación del rol. Distingue 401 de 403. La consulta de órdenes todavía debe comprobar su permiso específico en aplicación.
2. [HttpCurrentActor.cs](../src/TallerOs.Api/Access/HttpCurrentActor.cs): observa cómo convierte el usuario autenticado en un `Actor`, sin pasar la entidad persistida a los casos de uso.
3. [PermissionEndpoints.cs](../src/TallerOs.Api/Permissions/PermissionEndpoints.cs): identifica los DTO HTTP y la llamada al caso de uso; las transiciones no se deciden aquí.
4. [PermissionRequestService.cs](../src/TallerOs.Application/Permissions/PermissionRequestService.cs): recorre `Create`, `List` y `Resolve`. Localiza la restricción por rol, el filtro de solicitudes propias y la prohibición de resolver una solicitud del mismo actor.
5. [PermissionRequest.cs](../src/TallerOs.Domain/Permissions/PermissionRequest.cs): estudia `Create`, `Approve`, `Reject`, `Apply` y `PermissionRequestTransitions.IsAllowed`. Relaciona cada regla con un estado o dato protegido.
6. [AccessContracts.cs](../src/TallerOs.Application/Access/AccessContracts.cs) y [PermissionContracts.cs](../src/TallerOs.Application/Permissions/PermissionContracts.cs): identifica qué necesita cada consumidor y qué detalles quedan ocultos.
7. [AccessPermissions.cs](../src/TallerOs.Api/Infrastructure/Access/AccessPermissions.cs), [PermissionRequestStore.cs](../src/TallerOs.Api/Infrastructure/Permissions/PermissionRequestStore.cs) y [AppDbContext.cs](../src/TallerOs.Api/Data/AppDbContext.cs): sigue la concesión preparada, el guardado único y la detección de conflictos. `StageGrant` no confirma cambios por sí solo.
8. [Program.cs](../src/TallerOs.Api/Program.cs): comprueba cómo se conectan contratos e implementaciones y por qué los adaptadores comparten el mismo contexto scoped.
9. [WorkOrderQuery.cs](../src/TallerOs.Application/Workshop/WorkOrderQuery.cs): sigue la consulta del permiso antes de acceder al lector de órdenes. Aquí empieza el caso propio del taller; el Core no consulta sus entidades.

El recorrido de aprobación es: sesión válida, rol Administrador, solicitud ajena pendiente, registro de responsable y fecha, preparación de la concesión, transición a `Applied` y guardado transaccional. Después, el solicitante puede consultar órdenes con la misma sesión. Contrasta ese recorrido con el rechazo: exige motivo y no prepara una concesión.

## Arquitectura, POO y principios aplicados

**Encapsulación y cohesión.** `PermissionRequest` tiene setters privados y operaciones que protegen sus invariantes. El servicio coordina dependencias; no reemplaza el comportamiento de la entidad. Los DTO son datos de entrada o salida y tienen una función distinta.

**Abstracción y polimorfismo.** `ICurrentActor`, `IAccessPermissions` e `IPermissionRequestStore` permiten usar adaptadores reales o dobles de prueba. Identifica la promesa de cada contrato. Una prueba que usa memoria no demuestra el comportamiento transaccional de SQLite; por eso la comprobación de almacenamiento se hizo aparte.

**Responsabilidad única e inversión de dependencias.** HTTP, reglas, coordinación y persistencia tienen motivos de cambio distintos. Application depende de Domain y de sus contratos; no depende de EF Core. `Program.cs` conecta las implementaciones. El código heredado y los adaptadores siguen dentro del proyecto API: la separación física completa todavía está pendiente.

**Interfaces pequeñas.** El actor, el catálogo y las concesiones se expresan mediante contratos enfocados. No se introdujo una interfaz para cada clase ni un repositorio genérico. Evalúa si una nueva operación pertenece a uno de esos contratos o requiere otra responsabilidad.

**Construcción y adaptación.** `PermissionRequest.Create` es un método de creación con reglas; eso por sí solo no implica el patrón GoF Factory Method. Los adaptadores de acceso y persistencia conectan los contratos con recursos concretos. No se implementaron Strategy, un mediador ni una clase por estado: la tabla de transiciones resuelve el problema actual.

**Core y negocio.** Las máquinas de solicitudes y órdenes son independientes. El identificador del permiso se registra en la composición; Gestión de permisos no importa entidades del taller. Quitar el módulo requiere ajustar su composición, sin reescribir las reglas de las solicitudes.

## Cómo leer las pruebas

En [PermissionRequestTests.cs](../tests/TallerOs.UnitTests/PermissionRequestTests.cs), empieza por `RF_GP_03_Approve_RecordsAdministratorAndResolutionTime` y `RF_GP_06_ApplyRejectedRequest_RejectsAndPreservesReason`. Localiza preparar, actuar y verificar; explica por qué el estado y los datos de resolución también se comprueban después de un rechazo.

En [PermissionRequestServiceTests.cs](../tests/TallerOs.UnitTests/PermissionRequestServiceTests.cs), sigue `RF_GP_05_Approve_AppliesPermissionAndUnlocksAction` y `RF_GP_07_PromotedRequesterCannotResolveOwnRequest`. Revisa el reloj fijo y los dobles de actor, acceso y almacenamiento. Cada escenario crea sus propios datos. No presupongas que el nombre de una prueba demuestra un comportamiento que sus aserciones no comprueban.

El comando de la suite es `dotnet test TallerOs.slnx`. No inicia la API ni demuestra por sí solo migraciones, autenticación HTTP o persistencia tras reiniciar.

## Preguntas de autoexplicación

- ¿Por qué `Approved` existe si una aprobación HTTP exitosa devuelve `Applied`? ¿Qué fallo impediría guardar ambos cambios?
- ¿Por qué se comprueba el rol en servidor y también en el caso de uso? ¿Qué comprueba cada nivel?
- ¿Cómo demuestras RF-GP-07 usando un solicitante que ahora es Administrador, sin confundirlo con un rechazo por rol?
- ¿Qué impediría que el cliente pidiera el permiso para otro usuario enviando un `requesterId` distinto?
- ¿Qué valor original compara EF al guardar una resolución? ¿Por qué el dominio no necesita conocer esa configuración?
- ¿Cómo pueden dos solicitudes distintas competir por la misma concesión? ¿Qué ocurre con la que no se puede guardar?
- ¿Qué cambiarías si el permiso habilitara una acción de otro módulo? ¿Qué archivos del Core no deberían cambiar?
- ¿Qué evidencias necesitarías antes de afirmar que la aplicación cumple todos los criterios, además de un build correcto?

## Ejercicios breves

1. Dibuja de memoria el flujo de aprobación e indica en cada paso el componente y la capa responsables. Después compáralo con los archivos anteriores.
2. Elige una transición prohibida, predice el estado y los datos que deben conservarse y ejecuta la prueba correspondiente por separado.
3. En una rama de práctica, modifica una transición válida de forma deliberada. Identifica qué prueba falla y si su mensaje permite entender el error. Restaura el cambio antes de integrar; este ejercicio es una propuesta de estudio, no una verificación ejecutada para esta entrega.
4. Explica cómo probarías atomicidad con dos contextos SQLite y por qué un doble de almacenamiento que siempre devuelve éxito no bastaría.

## Lecturas oficiales

- [Pruebas con .NET y xUnit](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit): proyecto de pruebas, aserciones y ejecución mediante `dotnet test`.
- [Transacciones en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions): comportamiento de una llamada a `SaveChanges` y límites del guardado atómico.
- [Conflictos de concurrencia en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency): valores originales, tokens de concurrencia y tratamiento de actualizaciones obsoletas.

Enlaces comprobados el 20 de octubre de 2026. Notificaciones, auditoría completa, CI/CD y la suite de estados del taller pertenecen a otras unidades.
