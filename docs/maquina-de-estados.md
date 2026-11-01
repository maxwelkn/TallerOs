# Máquina de estados de la orden de reparación

La máquina del taller pertenece a `TallerOs.Domain.Workshop` y es independiente de Gestión de permisos. `WorkOrderStatus` declara los cinco estados en un solo lugar; `WorkOrderTransitions` centraliza las parejas y condiciones. `WorkOrder.MoveTo` valida antes de cambiar el estado.

## Transiciones permitidas

| Desde | Hacia | Quién la ejecuta | Condición |
| --- | --- | --- | --- |
| Received | Diagnosed | Estándar o Administrador | Existe al menos un diagnóstico registrado mediante `AddDiagnosis`. |
| Diagnosed | Authorized | Estándar o Administrador | `customerAuthorizationConfirmed` es verdadero: se confirma la autorización del cliente para el trabajo. |
| Authorized | InRepair | Estándar o Administrador | La orden está autorizada y se solicita iniciar la reparación. |
| InRepair | Delivered | Estándar o Administrador | `workCompletionConfirmed` y `deliveryConfirmed` son verdaderos. |

Registrar un diagnóstico no avanza el estado. Las confirmaciones son entradas explícitas de la transición; no hay cotización económica en esta práctica. Si falta una condición, se rechaza la transición y se conservan los datos de la orden.

## Transiciones prohibidas

La primera fila es la prohibición explícita RF-NEG-04 y será la referencia del defecto 1. No se permite ninguna combinación fuera de la tabla de permitidas, incluidas las repeticiones del mismo estado.

| Desde | Hacia | Regla |
| --- | --- | --- |
| Received | InRepair | Prohibida explícitamente: omite diagnóstico y autorización. |
| Received | Received | La máquina no permite esta combinación. |
| Received | Authorized | La máquina no permite esta combinación. |
| Received | Delivered | La máquina no permite esta combinación. |
| Diagnosed | Received | La máquina no permite esta combinación. |
| Diagnosed | Diagnosed | La máquina no permite esta combinación. |
| Diagnosed | InRepair | La máquina no permite esta combinación. |
| Diagnosed | Delivered | La máquina no permite esta combinación. |
| Authorized | Received | La máquina no permite esta combinación. |
| Authorized | Diagnosed | La máquina no permite esta combinación. |
| Authorized | Authorized | La máquina no permite esta combinación. |
| Authorized | Delivered | La máquina no permite esta combinación. |
| InRepair | Received | La máquina no permite esta combinación. |
| InRepair | Diagnosed | La máquina no permite esta combinación. |
| InRepair | Authorized | La máquina no permite esta combinación. |
| InRepair | InRepair | La máquina no permite esta combinación. |
| Delivered | Received | Delivered es terminal. |
| Delivered | Diagnosed | Delivered es terminal. |
| Delivered | Authorized | Delivered es terminal. |
| Delivered | InRepair | Delivered es terminal. |
| Delivered | Delivered | Delivered es terminal. |

## Cobertura y estado del incremento

Las dos tablas enumeran las 25 combinaciones de los cinco estados: cuatro permitidas y 21 prohibidas. `WorkOrderTests.RF_NEG_03_AllowedTransition_MovesToRequestedState` cubre cada fila permitida; `RF_NEG_04_RD_04_ForbiddenTransition_PreservesOrder` cubre cada fila prohibida. Las expectativas de las pruebas se declaran explícitamente y no se obtienen de la política de producción.

`RF_NEG_05_DeliveredOrder_CannotLeaveTerminalState` intenta los cinco destinos desde Delivered. Las pruebas de condiciones verifican diagnóstico ausente, autorización no confirmada y cada combinación incompleta de confirmación de trabajo y entrega. Los rechazos conservan identidad, vehículo, motivo, estado y diagnósticos.

Este incremento implementa y prueba el dominio; los endpoints operativos se incorporarán en los siguientes incrementos. El dominio no consulta cuentas ni roles: Application verificará el actor y el transporte aplicará Control de acceso. La columna de roles describe el contrato aprobado para esas operaciones, no una comprobación HTTP ya implementada.

Los nombres de estados y el esquema relacional existente se conservan. Los modelos persistentes del taller pasan a Domain, mientras el contexto EF compartido permanece en API como adaptación heredada.
