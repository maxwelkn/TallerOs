# Máquina de estados de la orden de reparación

La entidad central del módulo TallerOS es `WorkOrder`. Sus cinco estados están en `WorkOrderStatus` y las transiciones se declaran en `WorkOrderTransitions`. Esta práctica entrega la estructura; las operaciones y pruebas de estas transiciones corresponden a la fase posterior del módulo de negocio.

| Desde | Hacia | Quién la ejecuta | Condición |
| --- | --- | --- | --- |
| Received | Diagnosed | Estándar o Administrador | Se registró el diagnóstico del vehículo. |
| Diagnosed | Authorized | Estándar o Administrador | El cliente autorizó la cotización. |
| Authorized | InRepair | Estándar o Administrador | El trabajo autorizado puede comenzar. |
| InRepair | Delivered | Estándar o Administrador | Se completó el trabajo y se entrega el vehículo. |
| Received | InRepair | Nadie | **Prohibida explícitamente:** falta diagnóstico y autorización. |
| Cualquier otra combinación | — | Nadie | Prohibida; el estado no cambia. |

`Delivered` es terminal: ninguna transición sale de él. Las condiciones de autorización y entrega se detallarán con el módulo de negocio; por ahora la política comprueba las parejas de estados desde un único lugar.
