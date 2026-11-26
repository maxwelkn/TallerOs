# Criterios de aceptación de la Práctica 5

Desde un clon de demostración, seguir Contenedor en el README. Usar dos administradores activos y confirmados A y B, dos Estándar C y D y un administrador deshabilitado. Registrar y activar cuentas ficticias, promoverlas con la ruta administrativa y usar su token en cada petición. Para consumir la cola ejecutar `docker compose run --rm --no-deps app send-mail`.

| Requisito | Dado | Cuando | Entonces |
| --- | --- | --- | --- |
| RF-NOT-01 | C puede solicitar WorkOrders.Read | C crea una solicitud | A y B ven un aviso nuevo sin leer. C, D y el administrador deshabilitado no lo reciben. |
| RF-NOT-02 | C y D tienen solicitudes pendientes | A aprueba la de C y rechaza la de D con motivo | C recibe la aprobación y D el rechazo con motivo. Repetir la resolución se rechaza y conserva un aviso por evento. |
| RF-NOT-03 | Existe un vehículo y C tiene sesión | C recibe una orden con POST /api/workshop/orders | A y B ven la referencia de la orden en el aviso. Una recepción inválida no genera avisos. |
| RF-NOT-04 | A y B tienen avisos distintos | Consultan GET /api/notifications | Cada uno solo ve los propios, con readUtc nulo o con la fecha de lectura. Sin sesión se obtiene 401. |
| RF-NOT-05 | A tiene un aviso sin leer | B intenta PUT /api/notifications/{id}/read y A lo ejecuta dos veces | B recibe 403 y A obtiene la misma fecha de lectura en ambas operaciones. |
| RF-NOT-06 | A y B conservan la preferencia inicial | Se crea una solicitud y se procesa la cola | Cada uno tiene el aviso interno y su correo en Mailpit. |
| RF-NOT-07 | B usa PUT /api/notifications/preferences/email con enabled=false | C recibe una orden nueva | B recibe el aviso interno y no se encola el correo para B. A recibe ambos canales. |
| RF-NOT-08 | Mailpit está detenido | Se registra una cuenta ficticia | El registro termina correctamente y el Administrador observa un correo pendiente. |
| RF-NOT-09 | Hay un correo pendiente y Mailpit está disponible | Se ejecuta send-mail | El mensaje aparece en Mailpit y figura como enviado en la cola. |
| RF-NOT-10 | Hay un pendiente con cero intentos y SMTP está caído | Se ejecuta send-mail cuatro veces | Queda pendiente tras los intentos uno y dos, fallido con último error tras el tercero y conserva tres intentos tras el cuarto. |
| RF-NOT-11 | La cola contiene mensajes | A y C consultan GET /api/admin/mail-outbox | A ve estado, intentos, fechas y último error. C recibe 403. La respuesta omite cuerpos, enlaces y códigos. |
| RF-NOT-12 | Mailpit está disponible y hay un pendiente | Se ejecuta el procesador dos veces | Mailpit contiene una sola copia. Dos procesos simultáneos tampoco duplican la entrega. |
| RF-NOT-13 | El tag e historial están disponibles | Se revisan árbol, historial y configuración SMTP | Las credenciales se suministran por variables de entorno. Se conservan únicamente valores ficticios de ejemplo en Git. No se abre .env. |
| RF-REP-01 | Hay dos solicitudes del mismo mes y estado | A consulta GET /api/reports/permission-requests con from y to | Aparece una fila de ese mes y estado con cantidad dos. |
| RF-REP-02 | Hay dos entregas del mismo tipo de vehículo, de una y tres horas | A consulta GET /api/reports/workshop-delivery-times en el rango de entrega | Ese tipo tiene dos órdenes y promedio de dos horas. Las fechas reproducibles usan el reloj de las pruebas de integración. |
| RF-REP-03 | Hay varias filas por grupo | A pide ambos reportes | Cada grupo aparece una vez y hay menos filas que registros de origen. |
| RF-REP-04 | C y D tienen registros en el mismo rango | C consulta ambos reportes | Totales y promedios solo incluyen solicitudes de C y órdenes recibidas por C. |
| RF-REP-05 | Hay datos en dos días UTC | A cambia from y to en ambos reportes | Cambia el agregado. Ambos días límite cuentan, los rangos vacíos devuelven lista vacía y las fechas inválidas o invertidas producen 400. |

## Revisión acumulada

1. Dado un clon con la etiqueta y configuración de ejemplo, al registrar una cuenta y ejecutar la cola se recibe el enlace. El login funciona una vez activada.
2. Dado un Estándar activado, una ruta administrativa devuelve 403 y, después de cerrar sesión, su token devuelve 401.
3. Dada una cuenta activada, al solicitar recuperación, procesar el correo y usar el código se puede iniciar sesión con la nueva contraseña.
4. Dadas dos solicitudes, se puede aprobar una y rechazar otra con motivo. Repetir una resolución devuelve 409 y conserva el estado.
5. Dado un vehículo válido, se puede recibir y diagnosticar una orden. El salto directo de Received a InRepair devuelve 409.
6. Dado un archivo subido, tras docker compose down sin -v y el arranque con el mismo proyecto, su descarga conserva el ID y SHA-256.
