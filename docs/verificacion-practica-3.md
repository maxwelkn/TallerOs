# Verificación de la entrega de Práctica 3

El 1 de noviembre de 2026 se verificó un clon nuevo de `https://github.com/maxwelkn/TallerOs.git`, situado en la etiqueta publicada `practica-3`, commit `68439777338340e2b6a0904ddf0e0bef40fcc6ce`. Este informe se incorpora después de esa etiqueta: documenta la comprobación y no cambia el punto de entrega.

## Preparación y alcance

Se siguieron los contratos, comandos y flujos del README. Las peticiones HTTP se ejecutaron mediante un verificador temporal externo al repositorio. Se utilizaron usuarios ficticios de `example.test`, contraseñas generadas durante la ejecución, una base SQLite temporal y procesos propios de la comprobación.

La API y los comandos se ejecutaron desde la raíz del clon, con `DB_PATH` apuntando al archivo temporal y `APP_BASE_URL` a la dirección local de la API. Se utilizó `bootstrap-admin` para preparar el administrador y `send-mail` para despachar la cola. Ninguna petición de registro o recuperación envió correo directamente.

El receptor SMTP escuchó únicamente en loopback y capturó cuatro mensajes con STARTTLS y autenticación. Usó el certificado de desarrollo local ya disponible, leído del almacén del usuario; no se exportaron claves ni se modificó la confianza. Esta prueba demuestra recepción SMTP local y uso de los mensajes para completar los flujos. **No se comprobó recepción en un buzón externo.**

## Revisión acumulada y recorrido del taller

| Frente | Resultado observado |
| --- | --- |
| Registro y activación por correo | Tres registros crearon correos pendientes antes de iniciar SMTP. El inicio de sesión previo a activar fue rechazado. Se activaron las cuentas mediante los enlaces recibidos; reutilizar un enlace fue rechazado. Los correos enviados no se reenviaron. |
| Inicio y cierre de sesión | Se obtuvo identidad con sesión válida, se rechazó la consulta sin sesión y el token dejó de funcionar después del cierre. Fue posible iniciar una sesión nueva. |
| Autorización administrativa | El usuario Estándar recibió 403 en administración; el Administrador pudo consultar. Las respuestas no expusieron hashes. |
| Recuperación de contraseña | Las respuestas para correo existente e inexistente fueron iguales. Se recuperó mediante el código recibido por SMTP; se comprobó su hash almacenado, el consumo único, la revocación de sesiones y el rechazo de la contraseña anterior. La contraseña nueva permitió entrar. |
| Gestión de permisos | Se crearon solicitudes, se filtraron las propias y las pendientes, se rechazó la resolución por Estándar y la autorresolución por Administrador. Aprobar habilitó la lectura del taller conservando el rol Estándar; rechazar mantuvo la denegación y guardó el motivo. Resolver nuevamente una solicitud terminada fue rechazado. |

También se crearon cliente, vehículo y orden relacionados. Se recorrieron los cinco estados hasta `Delivered`. Se rechazaron `Received → InRepair`, el avance sin diagnóstico, la autorización sin confirmación, la entrega incompleta y la salida del estado terminal. Registrar un diagnóstico conservó `Received` hasta solicitar explícitamente el avance.

Después se detuvo y reinició la API sobre el mismo archivo SQLite. Se conservaron sesiones válidas, concesiones, solicitudes aprobadas y rechazadas con su motivo, cliente, vehículo, orden entregada y diagnóstico. Los cuatro correos quedaron enviados, con un intento y sin error; ejecutar nuevamente `send-mail` no duplicó mensajes. Se comprobó además que dos contraseñas iguales tenían hashes distintos.

El recorrido terminó con **111 comprobaciones correctas**. Los procesos de API y SMTP se detuvieron y se eliminaron la base temporal, sus archivos auxiliares y los mensajes capturados. No se usó ni se eliminó la base habitual del proyecto.

## Compilación y pruebas del punto publicado

Desde la raíz del clon en `practica-3`:

```powershell
dotnet restore TallerOs.slnx
dotnet build TallerOs.slnx --verbosity quiet
dotnet test TallerOs.slnx --verbosity minimal
```

La compilación terminó con cero errores y cero advertencias. Pasaron las **246 pruebas**: 159 unitarias y 87 de integración, sin omisiones. La suite incluye integración real entre Gestión de permisos, concesiones y taller sin servidor; las comprobaciones HTTP son una verificación separada.

Para cada variante publicada se cambió el checkout del clon y se ejecutó `dotnet test TallerOs.slnx` sin modificar producción, pruebas ni documentación:

| Etiqueta | Fallidas esperadas y observadas | Aprobadas | Total |
| --- | ---: | ---: | ---: |
| `practica-3-defecto-1` | 3 | 243 | 246 |
| `practica-3-defecto-2` | 3 | 243 | 246 |
| `practica-3-defecto-3` | 4 | 242 | 246 |
| `practica-3-defecto-4` | 10 | 236 | 246 |

Los nombres de las pruebas detectoras y los cambios introducidos figuran en la sección **Defectos** del README. La cuarta variante debe fallar también en los cuatro casos de integración real. Su advertencia de parámetro sin usar es consecuencia del guard eliminado; la base correcta compila sin advertencias.

## Referencias de entrega y límites

Las cuatro variantes tienen como padre directo el commit de `practica-3`; cada una cambia únicamente el archivo de producción correspondiente. Sus mensajes identifican archivo y línea. Las pruebas y la documentación son idénticas a las de la base. Se comprobaron las referencias remotas de las cinco etiquetas y se preservaron las etiquetas anteriores.

La entrega académica corresponde a `practica-3`, no al estado futuro de `main`. Este informe no fusiona la rama ni cierra la unidad. Quedan la entrega manual en Moodle y el cierre expreso del estudiante para consolidar los materiales de estudio externos. Los resultados aportan evidencia técnica para la rúbrica; la evaluación corresponde al maestro.

SQLite sigue siendo el motor acordado. Las pruebas demuestran los comportamientos descritos y persistencia tras reiniciar; no constituyen una medición de carga ni justifican promesas sobre concurrencia en producción. Se conserva el contexto compartido heredado y no se afirma aislamiento completo de infraestructura o del Core.
