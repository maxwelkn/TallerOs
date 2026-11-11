# TallerOS

Sistema de gestión para un solo taller de automóviles y motocicletas. Incluye Control de acceso, Gestión de permisos, Documentos y el dominio y casos de uso del taller con pruebas unitarias y de persistencia. Las operaciones HTTP del taller incluyen clientes, vehículos, órdenes, diagnósticos y avance de reparación con autorización en servidor. La API usa C# y ASP.NET Core 10, Entity Framework Core y SQLite. React se incorporará en una etapa posterior.

## Contenedor

Requiere Docker Desktop con contenedores Linux y Compose. Desde la raíz de un clon, copia el ejemplo y arranca; no necesitas instalar .NET ni preparar bases manualmente:

```powershell
Copy-Item .env.example .env
docker compose up --build
```

La aplicación responde en **http://localhost:5000**, con comprobación en **http://localhost:5000/health**. La primera construcción descarga las imágenes y restaura paquetes, por lo que requiere conexión a Internet. Espera a que `app` esté saludable; puedes consultar el estado con `docker compose ps`. Para detener y retirar los contenedores, desde otra terminal en la misma carpeta:

```powershell
docker compose down
```

Ese comando conserva los volúmenes. No añadas `-v` si quieres conservar datos. Para volver a levantar sin reconstruir usa `docker compose up`; si cambias código, usa de nuevo `docker compose up --build`. Si el puerto 5000 está ocupado, cambia tanto `APP_PORT` como `APP_BASE_URL` en `.env` y usa esa nueva dirección en las peticiones. Una variable ya definida en la terminal puede prevalecer sobre `.env`; revisa esa configuración si el resultado difiere del ejemplo. No publiques la salida de `docker compose config`, pues puede incluir valores sensibles.

El ejemplo activa `DEMO_MODE=true` y crea el Administrador ficticio **admin@example.test**, contraseña **DemoTaller2026**. Es una configuración local de demostración con valores públicos de ejemplo. El comando de inicialización no sobrescribe un Administrador que ya exista. Cambiar su contraseña en `.env` después de crearlo no cambia la cuenta persistida. El puerto se publica solo en el loopback del equipo; SMTP y el buzón no publican puertos.

### Correo local y activación

Mailpit captura los correos dentro de Compose, sin entregar mensajes a buzones externos. Para empezar la demostración:

```powershell
$base = 'http://localhost:5000'
$demoLogin = @{ email = 'admin@example.test'; password = 'DemoTaller2026' } | ConvertTo-Json
$adminSession = Invoke-RestMethod "$base/api/access/login" -Method Post -ContentType 'application/json' -Body $demoLogin
$adminHeaders = @{ Authorization = "Bearer $($adminSession.token)" }
$registration = @{ name = 'Estudiante de prueba'; email = 'estudiante@example.test'; password = 'UsuarioDemo2026' } | ConvertTo-Json
Invoke-RestMethod "$base/api/access/register" -Method Post -ContentType 'application/json' -Body $registration
docker compose run --rm --no-deps app send-mail
$mailPage = Invoke-RestMethod "$base/api/admin/demo-mail" -Headers $adminHeaders
$mail = Invoke-RestMethod "$base/api/admin/demo-mail/$($mailPage.messages[0].id)" -Headers $adminHeaders
$mail.text
```

Abre el enlace de activación mostrado y después inicia sesión como `estudiante@example.test` usando el flujo de login de este README. Para repetir el registro usa otro correo ficticio. Después de solicitar recuperación, reenvío de activación o restablecimiento forzado, ejecuta nuevamente `send-mail` y consulta el mensaje correspondiente. Ejecutar el procesador otra vez sin nuevos pendientes debe informar cero envíos. Las peticiones solo encolan correo; el procesador es independiente.

| Ruta de demostración | Resultado |
| --- | --- |
| `GET /api/admin/demo-mail?start=0` | Página de hasta 50 mensajes: `total`, `start`, `limit`, `messages`, con `id`, `subject` y `to`. Incrementa `start` para páginas posteriores. |
| `GET /api/admin/demo-mail/{id}` | Mensaje con `id`, `subject`, `to` y `text`; no sirve HTML ni adjuntos del buzón. |

Ambas requieren Administrador, usan `no-store` y rechazan sesiones inválidas o usuarios Estándar con 401/403. Con `DEMO_MODE=false` no se mapean y devuelven 404. Un mensaje inexistente produce 404; un buzón caído produce 503 controlado y el resto de la API continúa funcionando. Estas rutas son una herramienta de demostración, no el componente futuro de Notificaciones ni una API de envío abierta.

### Datos y despliegue

`app-data` guarda las bases de acceso/taller y metadatos de documentos; `document-files` guarda los bytes privados; `mail-data` conserva los correos capturados. Compose añade el nombre de proyecto al nombre real de cada volumen. Mantén el mismo proyecto al repetir la demostración. Son datos separados de tus archivos locales en `data/`; copiar `.env.example` no importa la base de Windows.

Para comprobar persistencia, sube un archivo como usuario activado, guarda su ID y compara el hash de una descarga. Ejecuta `docker compose down` sin `-v`, vuelve a levantar con `docker compose up`, inicia sesión con ese mismo usuario, lista el ID y descarga de nuevo. Los bytes deben tener el mismo hash. El apartado Documentos detalla las peticiones. El arranque aplica migraciones automáticamente y no borra bases existentes.

SQLite es un motor embebido: no necesita un servidor ni un contenedor de base separado. Esta es la interpretación técnica aplicada a la referencia del enunciado sobre un servicio de base de datos. Sus archivos persistentes se montan en el volumen de datos y no publican ningún puerto.

```mermaid
flowchart LR
    Host[Equipo: localhost:5000] -->|HTTP publicado| App[app: API ASP.NET]
    Init[init: migraciones y Administrador] -->|prepara antes del arranque| App
    Init -->|escribe como usuario app| Data[(app-data: SQLite)]
    App -->|lee y escribe| Data
    App -->|guarda y descarga| Files[(document-files)]
    Sender[app: comando send-mail] -->|lee pendientes| Data
    Sender -->|SMTP interno: mailpit:1025| Mail[mailpit]
    App -->|consulta administrativa interna: mailpit:8025| Mail
    Mail -->|persiste mensajes| Captured[(mail-data)]
    App -->|consulta HTTPS para revisar entrega| Weather[WeatherAPI: servicio externo]
```

Esta es una vista de despliegue y almacenamiento, separada del diagrama funcional del Core. `app` e `init` usan la misma imagen multietapa y ejecutan como el usuario sin privilegios `app`; la imagen final incluye el runtime ASP.NET y la publicación, sin SDK. El estado de init `Exited (0)` es esperado. No se copia `.env`, Git, bases, compilaciones locales ni documentos personales al contexto de construcción.

`.env` queda ignorado por Git. `.env.example` contiene valores ficticios y una clave climática de ejemplo; el proyecto arranca con ella y la revisión de entrega responde con un error controlado. No guardes valores reales en el Dockerfile, Compose, ejemplos, capturas ni commits. La composición incluida es de demostración local; para SMTP real conserva TLS y autenticación, define las variables del proveedor y desactiva las rutas de buzón de demostración.

## Ejecución local con .NET

1. Instala el SDK de .NET 10 y clona este repositorio. Ejecuta los comandos desde la raíz `TallerOs`.
2. Ejecuta `dotnet restore TallerOs.slnx` y `dotnet build TallerOs.slnx`.
3. En PowerShell, crea un Administrador inicial con un correo distinto del que usarás para probar el registro:

   ```powershell
   $env:ADMIN_EMAIL = Read-Host 'Correo del Administrador'
   $secure = Read-Host 'Contraseña del Administrador' -AsSecureString
   $env:ADMIN_PASSWORD = [System.Net.NetworkCredential]::new('', $secure).Password
   dotnet run --project src/TallerOs.Api --no-launch-profile -- bootstrap-admin
   Remove-Item Env:ADMIN_PASSWORD
   ```

   La contraseña debe tener al menos ocho caracteres, letras y números. Este comando solo crea el primer Administrador; si ya existe uno, no crea otro.

4. En la misma terminal, inicia la API:

   ```powershell
   $env:APP_BASE_URL = 'http://localhost:5000'
   dotnet run --project src/TallerOs.Api --no-launch-profile --urls http://localhost:5000
   ```

   Abre `http://localhost:5000/health`. Debe responder `{"status":"ok"}`. La base SQLite se crea en `data/talleros.db`, fuera del control de versiones. Al reiniciar la API conserva usuarios y correos. `DB_PATH` permite elegir otra ruta.

5. Abre **otra** terminal PowerShell en la raíz del repositorio para las peticiones. El correo se envía con el comando independiente `send-mail` descrito abajo.

## Variables de entorno

| Nombre | Para qué sirve |
| --- | --- |
| `DB_PATH` | Ruta opcional de la base SQLite; por defecto `data/talleros.db`. Usa la misma ruta en la API y en los comandos. |
| `DOCUMENT_DB_PATH` | Base SQLite exclusiva de Documentos; por defecto `documents.db` junto a la base indicada por `DB_PATH`. Debe ser distinta de `DB_PATH`. |
| `DOCUMENT_FILES_PATH` | Carpeta privada de los archivos; por defecto `documents` junto a la base indicada por `DB_PATH`. |
| `APP_BASE_URL` | Dirección pública de la API usada en los enlaces de activación. |
| `ADMIN_EMAIL` | Correo del primer Administrador, usado solo por `bootstrap-admin`. |
| `ADMIN_PASSWORD` | Contraseña del primer Administrador, usada solo por `bootstrap-admin`. |
| `SMTP_HOST` | Servidor SMTP que entrega los correos. |
| `SMTP_PORT` | Puerto SMTP; por defecto 587. |
| `SMTP_USER` | Usuario de autenticación SMTP. |
| `SMTP_PASSWORD` | Contraseña de aplicación o credencial SMTP. |
| `SMTP_FROM` | Dirección remitente del correo. |
| `SMTP_USE_TLS` | Usa STARTTLS; por defecto `true`. Desactivarlo requiere `DEMO_MODE=true`. |
| `SMTP_USE_AUTHENTICATION` | Exige credenciales SMTP; por defecto `true`. Desactivarlo requiere `DEMO_MODE=true`. |
| `DEMO_MODE` | Habilita explícitamente el buzón administrativo local y la configuración SMTP de demostración; por defecto `false` fuera de Compose. |
| `MAILPIT_URL` | URL interna del buzón de demostración; en Compose `http://mailpit:8025/`. |
| `APP_PORT` | Puerto de Windows publicado por Compose; ejemplo `5000`. Debe corresponder a `APP_BASE_URL`. |
| `WEATHER_API_KEY` | Clave del proveedor WeatherAPI, leída del entorno para la revisión de entrega. El ejemplo es ficticio y no permite obtener clima real. |
| `WORKSHOP_LOCATION` | Ubicación del único taller usada para consultar clima; ejemplo y valor predeterminado `Santo Domingo`. |

No coloques valores reales en archivos versionados ni en commits. En la ejecución local con .NET, las variables se toman del entorno: la aplicación no carga automáticamente `.env`; Compose sí utiliza ese archivo para su configuración. Para un proveedor SMTP real conserva TLS y autenticación. La demostración de Compose usa exclusivamente la captura local descrita arriba.

### Envío de los correos pendientes

En la **segunda terminal**, define `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD` y `SMTP_FROM` como variables de entorno con los datos de tu proveedor. Para Gmail personal con verificación en dos pasos y una [contraseña de aplicación](https://support.google.com/accounts/answer/185833), usa estos comandos de PowerShell. La dirección remitente debe ser la de esa misma cuenta:

```powershell
$env:SMTP_HOST = 'smtp.gmail.com'
$env:SMTP_PORT = '587'
$env:SMTP_USER = Read-Host 'Tu dirección de Gmail'
$env:SMTP_FROM = $env:SMTP_USER
$secure = Read-Host 'Contraseña de aplicación de Google' -AsSecureString
$env:SMTP_PASSWORD = [System.Net.NetworkCredential]::new('', $secure).Password.Replace(' ', '')
```

El puerto 587 usa STARTTLS. No pegues la contraseña en el chat ni la guardes en el repositorio. Define `DB_PATH` si cambiaste su ruta en la API. Luego ejecuta:

```powershell
dotnet run --project src/TallerOs.Api --no-launch-profile -- send-mail
```

Cuando termines la prueba, ejecuta `Remove-Item Env:SMTP_PASSWORD` en esa terminal.

El comando informa cuántos correos envió. Ejecutarlo otra vez debe informar cero si no hay nuevos pendientes. Si SMTP falla, el registro o la recuperación ya terminaron bien y el correo permanece pendiente; corrige la conexión y repite el comando. Para la demo, ejecútalo después de registro, reenvío, recuperación o restablecimiento forzado. La aplicación **no envía correo dentro de esas peticiones**.

## API y pasos para comprobar los criterios

Todas las peticiones JSON usan `Content-Type: application/json`. En PowerShell:

```powershell
$base = 'http://localhost:5000'
$email = Read-Host 'Correo propio para recibir activación'
$password = Read-Host 'Contraseña de prueba'
$body = @{ name = 'Usuario de prueba'; email = $email; password = $password } | ConvertTo-Json
Invoke-RestMethod "$base/api/access/register" -Method Post -ContentType 'application/json' -Body $body
```

Ejecuta `send-mail` y abre el enlace recibido. La ruta del enlace es `GET /api/access/activate?token=...`. Antes de abrirlo, `/api/access/login` debe rechazar la cuenta inactiva; abrirlo dos veces debe rechazar la segunda apertura.

### Rutas públicas

| Método y ruta | JSON o parámetro | Comprobación |
| --- | --- | --- |
| `POST /api/access/register` | `name`, `email`, `password` | RF-CA-01/02/14/15: correo único; hash con sal; contraseña mínima; nace inactiva y encola enlace. Repite el correo, usa una contraseña corta y un correo mal formado para ver rechazos controlados. |
| `GET /api/access/activate?token=...` | Token del correo | RF-CA-16: activa una sola vez; un enlace usado o vencido se rechaza. |
| `POST /api/access/resend-activation` | `email` | RF-CA-17: la respuesta es igual para correo existente o inexistente; el enlace anterior deja de servir. Ejecuta `send-mail` para recibir el nuevo. |
| `POST /api/access/login` | `email`, `password` | RF-CA-03/19: entrega `token`. Correo inexistente y contraseña errónea dan el mismo mensaje. Tras cinco errores consecutivos, la contraseña correcta también se rechaza durante 15 minutos. |
| `POST /api/access/forgot-password` | `email` | RF-CA-09/10: misma respuesta para correo existente o no. Encola un código de un solo uso que vence en 30 minutos. Ejecuta `send-mail`. |
| `POST /api/access/reset-password` | `code`, `newPassword` | RF-CA-11/12/14: usa el código recibido. Repetirlo falla; la contraseña antigua y las sesiones anteriores dejan de funcionar. |

Para iniciar sesión y guardar la credencial:

```powershell
$loginBody = @{ email = $email; password = $password } | ConvertTo-Json
$session = Invoke-RestMethod "$base/api/access/login" -Method Post -ContentType 'application/json' -Body $loginBody
$headers = @{ Authorization = "Bearer $($session.token)" }
Invoke-RestMethod "$base/api/access/me" -Headers $headers
```

### Rutas con sesión

| Método y ruta | JSON | Comprobación |
| --- | --- | --- |
| `GET /api/access/me` | — | RF-CA-07: devuelve usuario y rol; sin credencial válida devuelve 401. |
| `POST /api/access/logout` | `{}` | RF-CA-18: la misma credencial deja de servir tras el cierre. |
| `POST /api/access/change-password` | `currentPassword`, `newPassword` | RF-CA-22/12/14: la contraseña actual incorrecta se rechaza; al cambiarla caducan todas las sesiones anteriores. |

Envía la cabecera `Authorization: Bearer <token>` a todas estas rutas. Tras cambiar contraseña, inicia sesión otra vez para obtener una credencial nueva.

### Rutas de Administrador

Inicia sesión con el Administrador creado por `bootstrap-admin` y usa su token en la cabecera `Authorization`. Prueba **las mismas rutas con el token de un Estándar**: deben responder 403 incluso si construyes la petición manualmente.

| Método y ruta | Datos | Comprobación |
| --- | --- | --- |
| `GET /api/admin/users/` | — | RF-CA-04/05/06/21: lista ID, nombre, correo, rol y estado; no devuelve hashes ni tokens. |
| `PUT /api/admin/users/{id}/role` | JSON `{"role":"Administrator"}` o `{"role":"Standard"}` | RF-CA-08: solo el Administrador cambia roles. |
| `PUT /api/admin/users/{id}/enabled?enabled=false` | `{}` | RF-CA-20: desactiva al usuario; su sesión abierta deja de servir. Con `enabled=true` lo reactiva. Intentar desactivar el propio Administrador se rechaza. |
| `POST /api/admin/users/{id}/force-reset` | `{}` | RF-CA-13: invalida la contraseña y sesiones existentes; encola un código nuevo. Ejecuta `send-mail` para recibirlo. |

`AccessAuthorization.cs` declara en una sola tabla el rol exigido por **cada ruta**: pública, Estándar o Administrador. El middleware consulta esa tabla antes de ejecutar la operación. Una ruta de acceso nueva que no esté declarada se rechaza, de modo que no queda pública por accidente.

### Almacenamiento y cola

La base SQLite está en `data/talleros.db`. Comprueba que `Users.PasswordHash` no contiene la contraseña escrita y que dos usuarios con la misma contraseña tienen hashes distintos. `ActivationTokens`, `RecoveryCodes` y `Sessions` almacenan **hashes de los secretos**, no los valores entregados al usuario. `EmailOutbox` muestra los correos `Pending` y `Sent`; tras enviar, ejecutar de nuevo `send-mail` no vuelve a entregar los enviados. Puedes abrir la base con cualquier visor SQLite.

Para simular SMTP caído, registra un usuario sin ejecutar `send-mail` o configura temporalmente un servidor SMTP inaccesible antes de invocar el comando. El registro responde correctamente y el correo permanece `Pending`. Reinicia la API y vuelve a consultar los usuarios para comprobar RD-09.

## Documentos

Todas las operaciones requieren una sesión válida. Cada archivo pertenece al usuario que lo sube; enviar otro ID en el formulario no cambia su propietario. Un Estándar lista únicamente sus documentos activos. El Administrador lista los activos de todos. Solo el propietario o un Administrador puede descargar o eliminar: otro usuario recibe 403. Sin sesión válida, la respuesta es 401.

| Método y ruta | Entrada o resultado |
| --- | --- |
| `POST /api/documents` | Multipart con exactamente un archivo en el campo `file`; devuelve 201 con `id`, `name`, `contentType`, `size`, `uploadedUtc` y `uploadedBy`. |
| `GET /api/documents` | Devuelve 200 con los metadatos visibles, sin documentos eliminados. |
| `GET /api/documents/{id}/download` | Devuelve el contenido como adjunto con su nombre original normalizado. |
| `DELETE /api/documents/{id}` | Marca eliminado y devuelve 204. Conserva el registro y el archivo físico. |

Un archivo ausente o vacío produce 400. El máximo por archivo es **10 MiB** (10 485 760 bytes); excederlo devuelve 400 o 413 según el límite detectado al recibir la petición. No se aceptan varios archivos ni un campo con otro nombre. Un formulario o MIME inválido produce un rechazo controlado. Un ID inexistente o eliminado devuelve 404, también al descargar como Administrador o repetir la eliminación. Un GUID mal formado no coincide con la ruta y devuelve 404.

El nombre se limita a su último segmento, admite hasta 255 caracteres y no determina la ubicación en disco. El tipo MIME se registra como declaración del cliente; no certifica el contenido ni sustituye un antivirus. Las descargas usan `application/octet-stream`, `Content-Disposition: attachment`, `nosniff` y `no-store`. Los archivos no se sirven desde una carpeta pública y los metadatos no exponen rutas ni claves de almacenamiento.

### Comprobación desde PowerShell

Con la API iniciada y `$base` y `$session.token` preparados como en la sección de inicio de sesión, selecciona un archivo ficticio de prueba. Este ejemplo usa `curl.exe`, incluido en Windows actuales:

```powershell
$documentPath = (Resolve-Path -LiteralPath (Read-Host 'Ruta del archivo de prueba')).Path
$document = curl.exe --silent --show-error --fail-with-body -H "Authorization: Bearer $($session.token)" -F "file=@$documentPath" "$base/api/documents" | ConvertFrom-Json
Invoke-RestMethod "$base/api/documents" -Headers $headers
$downloadPath = Join-Path $env:TEMP 'talleros-document-download.bin'
curl.exe --silent --show-error --fail-with-body -H "Authorization: Bearer $($session.token)" --output "$downloadPath" "$base/api/documents/$($document.id)/download"
(Get-FileHash -LiteralPath $documentPath).Hash -eq (Get-FileHash -LiteralPath $downloadPath).Hash
```

La comparación debe devolver `True`. Inicia sesión con otro Estándar y comprueba listado, descarga y eliminación: no debe ver el documento y las dos operaciones directas deben devolver 403. Un Administrador debe poder listar y descargar el mismo documento. Antes de eliminarlo, reinicia la API con las mismas rutas de datos, vuelve a iniciar sesión y repite listado y descarga.

Finalmente, con su propietario:

```powershell
Invoke-RestMethod "$base/api/documents/$($document.id)" -Method Delete -Headers $headers
Invoke-RestMethod "$base/api/documents" -Headers $headers
```

El documento ya no aparece. Descargarlo directamente o repetir la eliminación devuelve 404. El registro continúa en la tabla `Documents` con `IsDeleted = 1`; el archivo conserva su clave interna y sus bytes. No hay operación de restauración ni borrado físico en este incremento.

### Persistencia y fallos de almacenamiento

Al arrancar se aplica la migración `AddDocuments` sobre una base exclusiva, por defecto `data/documents.db`. No modifica las migraciones ni tablas existentes de usuarios, permisos o taller. Los archivos se crean bajo `data/documents`, salvo configuración distinta. Las rutas relativas de `DB_PATH`, `DOCUMENT_DB_PATH` y `DOCUMENT_FILES_PATH` se resuelven desde la raíz de la solución; API y comandos deben usar la misma configuración.

Una escritura fallida no registra el documento. Si se escribe un archivo nuevo pero falla guardar sus metadatos, se intenta retirar solo ese archivo; un fallo de compensación se registra e informa como error. Los fallos esperables de almacenamiento, incluido un archivo registrado que haya desaparecido del disco, producen 503 sin exponer rutas internas. Una caída abrupta entre escritura y guardado todavía puede dejar un archivo huérfano: no existe una transacción distribuida entre SQLite y disco. La eliminación lógica conserva ambos recursos. Deben conservarse tanto las bases como la carpeta de archivos al trasladar los datos.

Con Compose, esas rutas se montan en volúmenes nombrados como se describe en Contenedor. Auditoría RF-DOC-06 y asociaciones de documentos a órdenes todavía no están implementadas.

## API externa

Se usa [WeatherAPI](https://www.weatherapi.com/docs/realtime-api/) para revisar las condiciones de una entrega. `GET /api/workshop/orders/{id}/delivery-review` combina una orden con la precipitación actual de la ubicación configurada y genera una recomendación; no reenvía el JSON del proveedor ni cambia el estado de la orden.

Exige sesión y el mismo acceso que las consultas de órdenes: un Estándar necesita `WorkOrders.Read`; el Administrador conserva acceso. Una orden inexistente devuelve 404. Si el estado no es `InRepair`, responde 409 antes de consultar clima. Sin sesión responde 401; sin permiso, 403.

Con una clave válida, devuelve 200 con `orderId`, `orderStatus`, `location`, `observedUtc`, `precipitationMm` y `recommendation`. Precipitación mayor de cero recomienda coordinar bajo techo; cero indica que no se observa precipitación. Es orientación basada en una observación actual: no es un pronóstico ni garantiza seguridad vial. Las confirmaciones y transiciones de entrega siguen siendo las existentes.

La clave llega exclusivamente por `WEATHER_API_KEY` del entorno, desde `.env` en Compose. Para obtener datos reales debes usar una clave emitida por WeatherAPI; una clave inventada solo permite demostrar rechazo. Configura también `WORKSHOP_LOCATION` y recrea app para recoger cambios del entorno:

```powershell
docker compose up -d --force-recreate app
```

En ejecución directa con .NET, define esas variables en la terminal antes de arrancar; no se carga `.env` automáticamente. La API inicia aunque falte la clave y solo rechaza la revisión que necesita clima. Una clave ausente o rechazada, cuota, proveedor caído, datos inválidos, error de red o timeout devuelven 503 con mensaje comprensible. El límite de espera HTTP es 10 segundos. Un fallo de WeatherAPI no obliga al usuario a volver a iniciar sesión ni bloquea el resto del taller.

Para probar, prepara una orden como en la sección del taller, agrega diagnóstico y realiza `Diagnosed → Authorized → InRepair` con las condiciones necesarias. Con `$order.id`, `$base` y las cabeceras de una sesión autorizada:

```powershell
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/delivery-review" -Headers $headers
```

La respuesta usa el instante UTC obtenido de `last_updated_epoch` y la precipitación `precip_mm`. Una revisión no confirma trabajo terminado ni cambia a `Delivered`. No se siguen redirecciones del proveedor, se limita su respuesta a 64 KiB y el cliente HTTP no registra URLs, claves o cuerpos de error externos. Los mensajes al cliente son propios de TallerOs.

Ejemplo real del rechazo obtenido desde el contenedor al consultar una orden `InRepair` con una clave ficticia (HTTP 503):

```json
{"message":"El proveedor rechazó la clave de clima. Revisa WEATHER_API_KEY."}
```

Se comprobó que esa petición conserva la orden y deja disponibles las otras consultas. **La consulta meteorológica positiva real permanece pendiente de una clave válida.** Los casos positivos están comprobados con dobles HTTP y de aplicación; el ejemplo anterior es evidencia de rechazo controlado, no de obtención de clima real.

## Componentes del Core y del negocio

```mermaid
flowchart LR
    subgraph Core[Core del curso]
        Acceso[Control de acceso: identidad, roles y acceso efectivo]
        Permisos[Gestión de permisos: solicitudes y resolución]
        Notificaciones[Notificaciones: prevista; cola SMTP existente]
        Documentos[Documentos: archivos privados y borrado lógico]
        Reportes[Reportes: previsto]
        Auditoria[Auditoría: prevista]
    end
    subgraph Negocio[Módulo del taller]
        Taller[Taller: clientes, vehículos, órdenes, diagnósticos y revisión de entrega]
    end
    Taller -->|consulta identidad y permiso| Acceso
    Permisos -->|obtiene actor y concede acceso| Acceso
    Documentos -->|obtiene identidad y rol| Acceso
    Taller -.->|futuro: avisa eventos| Notificaciones
    Taller -.->|futuro opcional: adjunta archivos| Documentos
    Taller -.->|futuro: aporta datos agregados| Reportes
    Taller -.->|futuro: registra acciones| Auditoria
    Permisos -.->|futuro: avisa resolución| Notificaciones
    Permisos -.->|futuro: registra resolución| Auditoria
```

Las flechas continuas representan conexiones implementadas y las punteadas conexiones futuras. La cola SMTP ya existe para activación y recuperación; las notificaciones internas todavía no están implementadas. Todo se ejecuta dentro de la misma aplicación.

Gestión de permisos está en `TallerOs.Domain/Permissions` y `TallerOs.Application/Permissions`. Sus contratos de acceso están en `TallerOs.Application/Access`; no conocen entidades del taller. El identificador `WorkOrders.Read` se registra en `Program.cs`. La consulta que lo consume pertenece a `TallerOs.Application/Workshop`. Los adaptadores heredados de permisos y taller permanecen en `src/TallerOs.Api/Infrastructure`; comparten el contexto existente para guardar resolución y concesión en una sola transacción. La separación física completa de infraestructura y del código heredado sigue pendiente.

Documentos tiene dominio en `TallerOs.Domain/Documents`, casos de uso y contratos en `TallerOs.Application/Documents`, y adaptadores en el nuevo proyecto `TallerOs.Infrastructure`. Este depende de Application, que depende de Domain; API compone las implementaciones e invoca los casos de uso. `DocumentDbContext` solo mapea documentos y no consulta tablas de otros componentes. El uso de identidad pasa por `ICurrentActor`. El taller todavía no consume Documentos: la flecha correspondiente continúa como futura.

La revisión climática pertenece al negocio. `DeliveryWeatherAssessment` interpreta precipitación en Domain; `DeliveryReviewService` coordina la consulta autorizada y el puerto `IWorkshopWeather` en Application. `WeatherApiWeather` implementa HTTP en Infrastructure. No introduce dependencias del Core hacia el taller, tablas nuevas ni cambios de máquina de estados. El servicio externo aparece en la vista de despliegue, fuera de los componentes funcionales del Core.

## Máquina de estados del negocio

`WorkOrder` es la entidad central del taller. `Customer`, `Vehicle`, `WorkOrder` y `Diagnosis` están en `src/TallerOs.Domain/Workshop` y conservan sus relaciones persistentes. `WorkOrderStatus` declara los cinco estados y `WorkOrderTransitions` centraliza las transiciones y condiciones. Consulta [las tablas completas](docs/maquina-de-estados.md): cuatro combinaciones permitidas y 21 prohibidas. `Delivered` es terminal y `Received → InRepair` está prohibida explícitamente.

El incremento 1 de Práctica 3 implementa y prueba el dominio: diagnóstico registrado antes de marcar `Diagnosed`, confirmación del cliente para `Authorized` y confirmación de trabajo completado y entrega para `Delivered`. Registrar un diagnóstico no avanza el estado automáticamente. La consulta de órdenes existente se conserva. Los casos de uso, adaptadores y operaciones HTTP de los incrementos 2 y 3 están implementados. El incremento 4 añade la integración real de Gestión de permisos y el taller sin levantar la aplicación. El incremento 5 comprueba que esa integración detecta el defecto 4 y prepara los cuatro experimentos de entrega.

### Práctica 3: casos de uso y persistencia (incremento 2)

Los casos de uso del taller están en `src/TallerOs.Application/Workshop`; los adaptadores EF están en `src/TallerOs.Api/Infrastructure/Workshop`. La API registra sus dependencias en `Program.cs`. El contexto compartido sigue siendo una adaptación heredada; no se afirma que la separación completa de Infrastructure esté terminada.

| Caso de uso | Comprobación de aplicación | Resultado |
| --- | --- | --- |
| `CustomerService.Create` / `List` | Actor válido y datos obligatorios al crear | Vista de cliente / lista |
| `VehicleService.Create` / `List` | Actor válido; cliente existente al crear | Vista de vehículo / lista |
| `WorkOrderService.Receive` | Actor válido, vehículo existente y motivo | Orden `Received` sin diagnósticos |
| `WorkOrderService.AddDiagnosis` | Actor válido, orden existente y descripción | Diagnóstico guardado; no avanza estado |
| `WorkOrderService.Transition` | Actor válido y estado reconocido; reglas centralizadas en Domain | Detalle actualizado o conflicto |
| `WorkOrderQuery.List` / `Find` | Administrador o Estándar con `WorkOrders.Read` | Lista existente o detalle con diagnósticos |

Se admiten estados por nombre, sin distinguir mayúsculas y recortando espacios exteriores; no se aceptan códigos numéricos ni estados de Gestión de permisos. Un identificador vacío o texto obligatorio ausente produce `Invalid`; una referencia válida inexistente, `NotFound`; una condición de transición incumplida o un conflicto esperado de almacenamiento, `Conflict`. Los endpoints del incremento 3 traducen esos resultados a 400, 404 y 409, respectivamente.

`WorkOrderStore.Find` carga los diagnósticos para que la condición `Received → Diagnosed` use datos persistidos. La aplicación genera los identificadores de las entidades del taller; el mapeo EF declara `ValueGeneratedNever` para insertar correctamente diagnósticos agregados a órdenes existentes. El estado de la orden es token de concurrencia: una transición basada en un estado obsoleto no sobrescribe otra ya guardada. Los conflictos no se reintentan automáticamente; se debe consultar en una nueva petición.

No cambian tablas, relaciones ni valores persistidos de estados. La suite verifica que no hay cambios pendientes de esquema ni migraciones pendientes sobre una base temporal creada con las migraciones existentes. No se elimina la base habitual ni se incorpora un motor nuevo.

Las pruebas de persistencia ejercitan los casos sin servidor. Las rutas HTTP se comprueban por separado. La integración sin servidor que solicita y resuelve permisos mediante los componentes reales está implementada en `PermissionsWorkshopIntegrationTests`.

## Operaciones HTTP del taller (incremento 3)

Todas las rutas siguientes se declaran en `AccessAuthorization.cs`. Exigen sesión válida y rol Estándar o Administrador. Los endpoints llaman a Application y no usan `DbContext` ni deciden transiciones.

| Método y ruta | Entrada JSON | Resultado correcto |
| --- | --- | --- |
| `POST /api/workshop/customers` | `name`, `phone` | 201; cliente creado |
| `GET /api/workshop/customers` | — | 200; lista de clientes |
| `POST /api/workshop/vehicles` | `customerId`, `licensePlate`, `description` | 201; vehículo asociado a cliente existente |
| `GET /api/workshop/vehicles` | — | 200; lista de vehículos |
| `POST /api/workshop/orders` | `vehicleId`, `reason` | 201; orden en Received |
| `GET /api/workshop/orders` | — | 200; lista de órdenes |
| `GET /api/workshop/orders/{id}` | — | 200; detalle y diagnósticos |
| `POST /api/workshop/orders/{id}/diagnoses` | `description` | 201; diagnóstico sin cambiar estado |
| `POST /api/workshop/orders/{id}/transitions` | `status` y confirmaciones según destino | 200; detalle actualizado |

Las dos consultas de órdenes requieren además `WorkOrders.Read` si el actor es Estándar. El Administrador puede consultarlas sin esa concesión. Las demás operaciones del taller no requieren ese permiso de lectura. Los códigos 201 incluyen Location hacia la colección de clientes/vehículos o el detalle de la orden, según el caso.

### Preparación para probar funcionalidades por separado

Arranca la API siguiendo la sección inicial y usa cuentas activadas. Para demostrar la concesión, prepara una cuenta Estándar que todavía no tenga `WorkOrders.Read` y un Administrador diferente. En una base de demostración separada, usa la misma `DB_PATH` para API y comandos; no borres la base habitual. El correo de activación se obtiene mediante el flujo ya documentado de envío o, para pruebas locales sin SMTP, leyendo el enlace de la cola con un visor SQLite.

En PowerShell, desde la raíz, prepara las sesiones sin escribir contraseñas en los comandos:

```powershell
$base = 'http://localhost:5000'
function New-WorkshopSession {
    param([string]$Email)
    $securePassword = Read-Host 'Contraseña de esta cuenta' -AsSecureString
    $body = @{
        email = $Email
        password = [System.Net.NetworkCredential]::new('', $securePassword).Password
    } | ConvertTo-Json
    $session = Invoke-RestMethod "$base/api/access/login" -Method Post -ContentType 'application/json' -Body $body
    @{ Authorization = "Bearer $($session.token)" }
}
$operatorHeaders = New-WorkshopSession (Read-Host 'Correo de la cuenta Estándar')
$adminHeaders = New-WorkshopSession (Read-Host 'Correo del Administrador')
```

Cada funcionalidad puede probarse con datos existentes: crear cliente solo necesita sesión; registrar vehículo necesita un cliente; recibir orden necesita un vehículo; registrar diagnóstico o cambiar estado necesita una orden en el estado indicado. Los listados permiten obtener los identificadores. Las pruebas automatizadas preparan sus propios datos y no dependen de ejecutar otra prueba primero.

Los ejemplos siguientes muestran también cómo preparar esos datos desde cero. Son datos ficticios de demostración.

### Registrar y consultar clientes

```powershell
$customerBody = @{ name = 'Cliente de demostración'; phone = '8090000000' } | ConvertTo-Json
$customer = Invoke-RestMethod "$base/api/workshop/customers" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body $customerBody
Invoke-RestMethod "$base/api/workshop/customers" -Headers $operatorHeaders
```

La creación devuelve 201 y la consulta 200. Nombre y teléfono son obligatorios. Para probar solo el registro de vehículos puedes obtener un cliente de este listado y usar su ID sin crear uno nuevo.

### Registrar y consultar vehículos

```powershell
$vehicleBody = @{ customerId = $customer.id; licensePlate = 'PRUEBA-1'; description = 'Motocicleta de demostración' } | ConvertTo-Json
$vehicle = Invoke-RestMethod "$base/api/workshop/vehicles" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body $vehicleBody
Invoke-RestMethod "$base/api/workshop/vehicles" -Headers $operatorHeaders
```

Se espera 201 con la relación al cliente y 200 al listar. Un `customerId` con GUID válido inexistente produce 404; un GUID vacío, placa vacía o descripción ausente produce 400. No se exige un formato internacional específico de teléfono ni se declara unicidad de placa en este alcance.

### Recibir una orden

```powershell
$orderBody = @{ vehicleId = $vehicle.id; reason = 'Ruido en el motor' } | ConvertTo-Json
$order = Invoke-RestMethod "$base/api/workshop/orders" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body $orderBody
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)" -Headers $adminHeaders
```

La recepción devuelve 201, estado Received y diagnósticos vacíos. El detalle, leído aquí como Administrador, devuelve 200. Un vehículo inexistente produce 404 y un motivo vacío produce 400.

### Consultar mediante un permiso aprobado

Ejecuta primero la consulta como Estándar sin permiso: debe devolver 403. Después crea y aprueba su solicitud:

```powershell
Invoke-RestMethod "$base/api/workshop/orders" -Headers $operatorHeaders
$permissionBody = @{ permission = 'WorkOrders.Read' } | ConvertTo-Json
$permissionRequest = Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body $permissionBody
Invoke-RestMethod "$base/api/permissions/requests/$($permissionRequest.id)/approve" -Method Post -Headers $adminHeaders
Invoke-RestMethod "$base/api/workshop/orders" -Headers $operatorHeaders
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)" -Headers $operatorHeaders
```

Las consultas posteriores devuelven 200 con la misma sesión Estándar; su rol no cambia. Si la cuenta ya tiene el permiso, la creación de solicitud devuelve 409: usa otra cuenta sin concesión para repetir la demostración completa. Una lista vacía autorizada sigue siendo 200.

### Registrar diagnóstico y marcar Diagnosed

Desde Received, intentar Diagnosed sin diagnóstico debe devolver 409. Ejecuta esa comprobación negativa individualmente antes del registro:

```powershell
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/transitions" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body '{"status":"Diagnosed"}'
$diagnosisBody = @{ description = 'Revisar transmisión' } | ConvertTo-Json
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/diagnoses" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body $diagnosisBody
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)" -Headers $adminHeaders
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/transitions" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body '{"status":"Diagnosed"}'
```

El diagnóstico devuelve 201 y el detalle intermedio todavía muestra Received. Solo la transición posterior devuelve 200 con Diagnosed. Para comprobar la prohibición explícita, intenta `{"status":"InRepair"}` sobre otra orden Received: se espera 409 y que permanezca Received.

### Autorizar, iniciar reparación y entregar

Con la orden en Diagnosed, probar Authorized sin confirmar al cliente produce 409. El recorrido correcto es:

```powershell
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/transitions" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body '{"status":"Authorized","customerAuthorizationConfirmed":true}'
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/transitions" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body '{"status":"InRepair"}'
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)/transitions" -Method Post -Headers $operatorHeaders -ContentType 'application/json' -Body '{"status":"Delivered","workCompletionConfirmed":true,"deliveryConfirmed":true}'
Invoke-RestMethod "$base/api/workshop/orders/$($order.id)" -Headers $adminHeaders
```

Cada transición correcta devuelve 200. Si falta cualquiera de las dos confirmaciones para entregar, devuelve 409 y conserva InRepair. Desde Delivered, intentar Received o cualquier otro destino devuelve 409 sin alterar el estado ni los diagnósticos. Las confirmaciones que no se envían valen false; no son códigos de permisos ni importes de cotización.

### Seguridad, errores y persistencia

- Invocar cualquiera de las nueve rutas sin Bearer válido produce 401. También se rechazan sesiones desconocidas, vencidas o revocadas y cuentas deshabilitadas.
- Estándar sin permiso recibe 403 en consultas de órdenes y al invocar una ruta administrativa. Estándar y Administrador pueden realizar las operaciones operativas.
- JSON mal formado, campos de tipo incorrecto, identificadores vacíos y estados como `"1"` o `"Pending"` producen 400. Un GUID mal formado en una ruta con `{id:guid}` no coincide con ella y produce 404.
- Las referencias inexistentes producen 404 y las transiciones incompatibles o sin condiciones producen 409. PowerShell muestra error ante esas respuestas esperadas: ejecútalas individualmente y revisa el código antes de seguir.
- Para verificar RD-09 manualmente, detén la API, vuelve a iniciarla con la misma `DB_PATH` y consulta el detalle y los listados. Deben conservarse estado, diagnósticos y relaciones. Si la sesión venció, inicia sesión otra vez.

La suite comprueba el cierre y reapertura de un archivo SQLite y los recorridos HTTP. Además, se completó la [revisión acumulada desde un clon de GitHub de `practica-3`](docs/verificacion-practica-3.md), con datos ficticios, base temporal y captura SMTP local.

## Pruebas

Desde la raíz de un clon con el SDK de .NET 10 y acceso a NuGet:

```powershell
dotnet test TallerOs.slnx
```

El comando restaura dependencias, compila y ejecuta todas las suites de la solución:

- `TallerOs.UnitTests`: dominio y aplicación con dobles, sin API, SQLite ni SMTP. `WorkOrderTests` cubre cada fila de transiciones y el terminal; `WorkshopServiceTests` cubre identidad, permisos, validación, referencias, resultados y ausencia de guardado ante rechazos.
- `DocumentTests` y `DocumentServiceTests`, en la suite unitaria: metadatos, límites, identidad, visibilidad, propietario/Administrador, borrado lógico y compensación de una subida fallida sin recursos externos.
- `DocumentStorageTests`, en integración: migraciones propias, SQLite y disco temporales, reapertura, filtrado real, conservación del archivo al borrar lógicamente, escritura interrumpida, tamaño real y compensación ante fallo de SQLite.
- `DocumentHttpTests`, en integración: las cuatro rutas con sesiones válidas e inválidas, dos Estándar y un Administrador, descargas idénticas, borrado lógico, multipart y límites de 10 MiB. La fixture configura rutas temporales explícitas de metadatos y archivos; no usa la base habitual ni SMTP.
- `DemoMailboxServiceTests`, `MailpitMailboxTests`, `MailConfigurationTests` y las pruebas HTTP de buzón: autorización, desactivación de rutas, traducción del proveedor, errores y mantenimiento de los valores seguros SMTP. Las pruebas automatizadas sustituyen Mailpit por dobles o un destino local inaccesible; no requieren Docker ni envían correo real.
- `DeliveryReviewTests`, `WeatherApiWeatherTests` y `DeliveryReviewHttpTests`: regla climática, acceso/estado antes de red, formato de observación, codificación, errores, timeout, cancelación y ausencia de mutación. Usan dobles o configuración vacía explícita; no consumen una clave real ni llaman al proveedor desde las suites.
- `WorkshopPersistenceTests`, en `TallerOs.IntegrationTests`: casos y adaptadores reales del taller con SQLite temporal, sin API ni SMTP. Comprueba flujo entre contextos, concurrencia y rollback, FK inválida, conservación tras un rechazo, compatibilidad de modelo y cierre/reapertura del archivo.
- `WorkshopHttpTests`, en el mismo proyecto: inicia automáticamente una API real en un proceso hijo oculto, un puerto local libre y una base temporal. Prepara cuentas ficticias y sesiones, realiza login HTTP y comprueba las nueve rutas, ambos roles, permisos de lectura, entradas inválidas, estados y terminal. Al terminar detiene su proceso y elimina sus archivos temporales; no usa tu base ni envía correo.
- `PermissionsWorkshopIntegrationTests`, en `TallerOs.IntegrationTests`: cuatro casos sin servidor, sin procesos HTTP y sin dobles de Gestión de permisos o del taller. Usa `PermissionRequestService`, `WorkOrderQuery`, el catálogo configurado y los adaptadores EF reales con SQLite en memoria. Prueba listado y detalle antes/después de aprobar y antes/después de rechazar.

La suite de estados se mantiene independiente del servidor. Para ejecutarla por separado: `dotnet test tests/TallerOs.UnitTests/TallerOs.UnitTests.csproj --filter FullyQualifiedName~WorkOrderTests`. El comando de la solución incluye también las comprobaciones HTTP, por lo que el equipo debe permitir iniciar procesos `dotnet` y escuchar en loopback; no necesitas arrancar la API manualmente.

Cada prueba prepara los datos necesarios sin depender de otra prueba. Las fases se distinguen por bloques y los nombres identifican los requisitos. El fixture HTTP prepara concesiones directamente para comprobar el transporte y la autorización. La integración sin servidor utiliza un escenario distinto y crea la concesión exclusivamente mediante la aprobación real de una solicitud.

### Integración real de permisos y taller (incremento 4)

La prueba `PermissionsWorkshopIntegrationTests.RF_NEG_06_RF_GP_05_ApprovalUnlocksRealWorkshopOperation` se ejecuta dos veces: para listar órdenes y para consultar su detalle. Prepara dos usuarios distintos, Estándar y Administrador, y una orden guardada mediante los casos reales de clientes, vehículos y órdenes. El Estándar crea su solicitud `WorkOrders.Read` con `PermissionRequestService`; mientras está Pending, `WorkOrderQuery` responde Forbidden y no devuelve la orden. El Administrador aprueba mediante el mismo servicio de permisos y la solicitud termina Applied. La operación posterior del Estándar devuelve la orden persistida, conserva su estado Received y no cambia el rol de la cuenta.

La prueba complementaria `RF_NEG_06_RF_GP_04_RejectionKeepsRealWorkshopOperationForbidden` verifica las dos consultas antes/después de un rechazo real: ambas siguen Forbidden, se conserva el motivo de rechazo y no aparece una concesión.

El escenario compone los contratos con sus implementaciones reales en un contenedor de dependencias. Cada paso usa un alcance nuevo y un `AppDbContext` nuevo sobre la misma conexión SQLite en memoria, abierta solo durante esa prueba. Los usuarios se preparan como datos ficticios; `HttpCurrentActor` recibe la cuenta persistida en un `DefaultHttpContext` mínimo. Esto representa la identidad ya establecida en el límite de la aplicación: no se inicia Kestrel ni se pretende verificar allí Bearer, login o middleware. Esas comprobaciones corresponden a `WorkshopHttpTests`. No hay `FakeStore`, `FakeAccess`, `FakeActor` ni implementaciones sustitutas de los componentes en este escenario.

Para ejecutar únicamente esta integración sin iniciar ningún proceso API:

```powershell
dotnet test tests/TallerOs.IntegrationTests/TallerOs.IntegrationTests.csproj --filter FullyQualifiedName~PermissionsWorkshopIntegrationTests
```

El comando general `dotnet test TallerOs.slnx` también la ejecuta. Las aserciones anteriores a la aprobación exigen Forbidden y ausencia de datos: al quitar la consulta del permiso, la operación se permite y esos casos fallan. La sección Defectos documenta ese experimento. El [informe del incremento 6](docs/verificacion-practica-3.md) registra la revisión acumulada de los cinco frentes desde un clon remoto de `practica-3`.

## Defectos

Esta sección corresponde a **Práctica 3**. La base correcta es `practica-3`: el comando `dotnet test TallerOs.slnx` ejecuta 246 casos y todos pasan. Las cuatro variantes `practica-3-defecto-1` a `practica-3-defecto-4` son experimentos independientes; sus cambios no se incorporan a la rama de trabajo ni a `main`.

Las pruebas citadas pertenecen a [WorkOrderTests](tests/TallerOs.UnitTests/WorkOrderTests.cs) y [PermissionsWorkshopIntegrationTests](tests/TallerOs.IntegrationTests/PermissionsWorkshopIntegrationTests.cs). Cada fila identifica un caso que realmente falla con el defecto, además del total de fallos observado en toda la solución.

| Defecto | Cambio deliberado | Prueba que lo detecta | Comportamiento protegido | Fallidas / aprobadas |
| --- | --- | --- | --- | ---: |
| 1 | Permitir `Received → InRepair`, primera fila de la tabla de prohibidas | `WorkOrderTests.RF_NEG_04_RD_04_ForbiddenTransition_PreservesOrder(initial: Received, next: InRepair)` | Rechaza el salto y conserva estado y datos de la orden | 3 / 243 |
| 2 | Permitir `Delivered → Received` | `WorkOrderTests.RF_NEG_05_DeliveredOrder_CannotLeaveTerminalState(next: Received)` | No permite salir del estado terminal | 3 / 243 |
| 3 | Quitar la exigencia de diagnóstico en `Received → Diagnosed`, primera transición condicionada | `WorkOrderTests.RF_NEG_03_DiagnoseWithoutDiagnosis_RejectsAndPreservesOrder` | Sin diagnóstico, rechaza y conserva Received | 4 / 242 |
| 4 | Quitar la consulta de `WorkOrders.Read` en `WorkOrderQuery.CheckAccess` | `PermissionsWorkshopIntegrationTests.RF_NEG_06_RF_GP_05_ApprovalUnlocksRealWorkshopOperation(detail: false / true)` | La integración real debe rechazar listado y detalle mientras la solicitud está Pending | 10 / 236 |

Cada variante compila y ejecuta los mismos 246 casos con las pruebas sin modificar. Los recuentos incluyen otras pruebas que también detectan el mismo cambio: en el defecto 4 fallan los cuatro casos de integración real, además de pruebas unitarias y HTTP. Un error de compilación no cuenta como detección.

Los defectos 1 y 2 agregan una pareja en `src/TallerOs.Domain/Workshop/WorkOrderTransitions.cs`; el 3 elimina solo la guardia del diagnóstico en ese archivo. El 4 elimina solo la guardia de permiso de `src/TallerOs.Application/Workshop/WorkOrderQuery.cs`, conservando el rechazo por actor ausente. Eliminar esa consulta deja el parámetro `access` sin uso y produce la advertencia CS9113; la variante compila y falla por aserciones de seguridad. La base correcta compila sin errores ni advertencias.

### Forma de los commits y etiquetas

- `practica-3` señala la base correcta con código, pruebas y documentación.
- Cada `practica-3-defecto-N` señala un commit de un solo defecto cuyo padre directo es el commit de `practica-3`.
- Los commits defectuosos modifican únicamente un archivo de producción: no cambian pruebas, README, configuración ni dependencias. Su mensaje indica requisito, archivo y línea de la base que se modifica o elimina.
- Las cuatro variantes parten de la misma base; no se encadenan entre sí. Las etiquetas anteriores se conservan y no se mueven.

### Comprobación desde un clon de evaluación

Sobre la base correcta:

```powershell
git checkout practica-3
dotnet test TallerOs.slnx
dotnet build TallerOs.slnx
```

Para cada variante, cambia el número del ejemplo por 1 a 4. Los dos primeros comandos deben devolver el mismo commit. El diff debe incluir solo el archivo de producción descrito y el mensaje debe identificar su línea:

```powershell
git rev-parse 'practica-3^{commit}'
git rev-parse 'practica-3-defecto-1^'
git diff --stat practica-3 practica-3-defecto-1
git show --no-patch --format=full practica-3-defecto-1
git checkout practica-3-defecto-1
dotnet test TallerOs.slnx
```

Se espera al menos una prueba fallida en cada variante, con los recuentos de la tabla. Al terminar, ejecuta `git checkout practica-3` para volver al código correcto. Las pruebas HTTP que incluye el comando se aíslan en su propia base temporal, incluso al ejecutar una variante.

### Entrega de Práctica 3

En Moodle se entrega la URL del repositorio y el nombre `practica-3`; las cuatro etiquetas de defecto se consultan en el mismo repositorio. Se evalúa el punto etiquetado, no el estado futuro de `main`. La disponibilidad remota de las cinco etiquetas y la revisión acumulada desde el README de registro/activación, login/logout, rechazo administrativo, recuperación y Gestión de permisos se comprobaron en el [incremento 6](docs/verificacion-practica-3.md). El informe se publicó después del punto de entrega, sin mover sus etiquetas. Conviene confirmar su disponibilidad al realizar la entrega manual.

## Defectos de la Práctica 2 (histórico)

La Práctica 2 utiliza la variante B. Cada defecto se prueba por separado con el mismo comando de la sección **Pruebas**, sin modificar archivos de pruebas. La suite de permisos de la Práctica 2 tiene 44 casos: con el código correcto todos pasan; con cada defecto debe fallar al menos uno por el comportamiento incorrecto.

Las pruebas citadas pertenecen a [PermissionRequestTests](tests/TallerOs.UnitTests/PermissionRequestTests.cs) y [PermissionRequestServiceTests](tests/TallerOs.UnitTests/PermissionRequestServiceTests.cs). Los parámetros indicados identifican el caso concreto de cada prueba parametrizada.

| Defecto B | Requisito | Prueba que lo detecta | Comportamiento que comprueba | Casos fallidos en el experimento local |
| --- | --- | --- | --- | ---: |
| 1. Aceptar un motivo vacío al rechazar | RF-GP-04 | `PermissionRequestServiceTests.RF_GP_04_MissingReason_DoesNotSaveResolution(reason: "")` | Rechazar con motivo vacío devuelve `Invalid`, conserva `Pending` y los datos de resolución vacíos, no guarda ni concede el permiso. | 4 |
| 2. Permitir `Approved → Rejected` | RF-GP-06 | `PermissionRequestTests.RF_GP_06_ForbiddenOperation_PreservesRequest(initial: Approved, operation: "reject")` | Rechazar una solicitud aprobada lanza `PermissionRuleException` y conserva estado, responsable, fecha y motivo. | 2 |
| 3. Permitir que un Estándar apruebe | RF-GP-03 | `PermissionRequestServiceTests.RF_GP_03_RF_GP_04_StandardCannotResolveRequests(approve: true)` | La aprobación como Estándar devuelve `Forbidden`; la solicitud sigue pendiente, sin resolución, guardado ni concesión. | 1 |
| 4. Invertir la prohibición de resolver la solicitud propia | RF-GP-07 | `PermissionRequestServiceTests.RF_GP_07_PromotedRequesterCannotResolveOwnRequest(approve: true/false)` | Un Administrador no puede aprobar ni rechazar su propia solicitud: recibe `Forbidden`, conserva `Pending` y no guarda ni concede el permiso. | 31 |
| 5. Permitir `Applied → Rejected` | RF-GP-06 | `PermissionRequestTests.RF_GP_06_ForbiddenOperation_PreservesRequest(initial: Applied, operation: "reject")` | Rechazar una solicitud aplicada lanza `PermissionRuleException` y conserva estado, responsable, fecha y motivo. | 2 |
| 6. Omitir el responsable y la fecha al aprobar | RF-GP-03 | `PermissionRequestServiceTests.RF_GP_03_Approve_ReturnsAdministratorAndResolutionTime` | La aprobación conserva en la entidad y devuelve en el resultado el ID del Administrador y la fecha UTC del reloj de prueba. | 9 |

La última columna cuenta todos los casos fallidos de la suite con cada defecto, no solo la prueba citada. Las seis modificaciones compilaron y fueron detectadas en experimentos locales; después de restaurar cada una, los 44 casos volvieron a pasar. En los defectos 1 y 4 se modificaron las comprobaciones equivalentes de aplicación y dominio para reproducir el comportamiento solicitado; las demás validaciones se conservaron.

### Entrega y comprobación de la Práctica 2

El punto de entrega es `practica-2`, con el código correcto y los 44 casos aprobados. Las etiquetas `practica-2-defecto-1` a `practica-2-defecto-6` identifican los seis experimentos independientes de la tabla. Cada commit de defecto debe tener como padre directo el commit de `practica-2`, modificar únicamente código de Gestión de permisos e indicar en su mensaje el archivo y la línea modificados. No se encadenan defectos ni se incorporan sus cambios a `main`.

Desde un clon con las etiquetas descargadas, comprueba la base:

```powershell
git checkout practica-2
dotnet test TallerOs.slnx
```

Repite la comprobación con cada etiqueta de defecto, sustituyendo el número del ejemplo por 1 a 6:

```powershell
git rev-parse 'practica-2^{commit}'
git rev-parse 'practica-2-defecto-1^'
git diff --stat practica-2 practica-2-defecto-1
git show -s --format=full practica-2-defecto-1
git checkout practica-2-defecto-1
dotnet test TallerOs.slnx
```

Los dos primeros comandos deben devolver el mismo commit. El diff debe contener solo código de Gestión de permisos y el mensaje debe identificar archivo y línea. Con cada defecto el comando de pruebas debe reportar al menos un caso fallido; un error de compilación no demuestra su detección. Vuelve a `practica-2` para usar la aplicación correcta. Los experimentos locales no sustituyen la publicación ni la verificación de las siete etiquetas.

La revisión acumulada de Control de acceso pasó localmente con una base temporal y captura SMTP con STARTTLS: registro y activación, inicio y cierre de sesión, rechazo por rol y recuperación de contraseña. No se comprobó entrega a buzones externos. Para reproducir estos flujos, sigue las secciones de ejecución, envío de correo y API de este README con tu configuración SMTP.

En Moodle se entrega la URL del repositorio y el nombre `practica-2`. El evaluador consulta también las seis etiquetas de defecto en ese repositorio; comprueba su disponibilidad remota antes de entregar.

## Asignación 2: Gestión de permisos

Para estudiar el flujo y las decisiones de esta unidad, consulta la [guía de Gestión de permisos](docs/estudio-gestion-permisos.md).

La entidad registra solicitante, permiso, estado, creación UTC, responsable de resolución, fecha UTC de resolución y motivo de rechazo. Las transiciones se deciden en `PermissionRequestTransitions`: `Pending → Approved → Applied` y `Pending → Rejected`. La aprobación pasa por `Approved` dentro del caso de uso y responde `Applied`, después de guardar también la concesión. No existe una operación HTTP para aplicar manualmente o cambiar libremente el estado.

El único permiso solicitable por ahora es `WorkOrders.Read`: habilita consultar todas las órdenes mediante `GET /api/workshop/orders`. No cambia el rol ni concede administración de usuarios. Un Administrador ya tiene acceso a esa consulta. Con una base nueva, la lista estará vacía y responderá 200 si está autorizado; no se requiere crear órdenes para comprobar la concesión.

### Preparación de la comprobación

1. Ejecuta el arranque y `bootstrap-admin` de la sección inicial. La migración `AddPermissionRequests` se aplica al arrancar y agrega tablas sin eliminar datos anteriores. Para usar una base de demostración separada, define primero `$env:DB_PATH = 'data/asignacion2-demo.db'` y usa esa ruta también en los comandos.
2. Registra tres usuarios distintos con `/api/access/register`: A para aprobar, B para rechazar y C para comprobar resolución propia. Actívalos antes de iniciar sesión. Puedes usar el envío de correo ya documentado. Para una comprobación local sin SMTP, usa direcciones ficticias como `a@example.test`, abre tu SQLite con un visor, busca su fila en `EmailOutbox` y abre el enlace de activación de `Body`; no edites usuarios, estados ni concesiones directamente.
3. En una terminal para peticiones, prepara las cabeceras siguientes. Introduce los correos utilizados al crear las cuentas:

```powershell
$base = 'http://localhost:5000'
function Open-DemoSession {
    param([string]$Email)
    $securePassword = Read-Host 'Contraseña de esta cuenta' -AsSecureString
    $body = @{
        email = $Email
        password = [System.Net.NetworkCredential]::new('', $securePassword).Password
    } | ConvertTo-Json
    $session = Invoke-RestMethod "$base/api/access/login" -Method Post -ContentType 'application/json' -Body $body
    @{ Authorization = "Bearer $($session.token)" }
}
$adminHeaders = Open-DemoSession (Read-Host 'Correo del Administrador')
$userAHeaders = Open-DemoSession (Read-Host 'Correo del usuario A')
$userBHeaders = Open-DemoSession (Read-Host 'Correo del usuario B')
$userCHeaders = Open-DemoSession (Read-Host 'Correo del usuario C')
$permissionBody = @{ permission = 'WorkOrders.Read' } | ConvertTo-Json
```

PowerShell muestra un error al recibir los 400, 401, 403, 404 o 409 esperados; revisa el código HTTP y el mensaje. Estas peticiones negativas deben ejecutarse individualmente para continuar después del rechazo.

### RF-GP-01 y RF-GP-02: solicitudes y privacidad

```powershell
$requestA = Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $userAHeaders -ContentType 'application/json' -Body $permissionBody
$requestB = Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $userBHeaders -ContentType 'application/json' -Body $permissionBody
$requestC = Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $userCHeaders -ContentType 'application/json' -Body $permissionBody
Invoke-RestMethod "$base/api/permissions/requests/mine" -Headers $userAHeaders
Invoke-RestMethod "$base/api/permissions/requests/pending" -Headers $adminHeaders
Invoke-RestMethod "$base/api/permissions/requests/pending" -Headers $userAHeaders
```

La creación devuelve 201 y `Pending`. `requesterId` se toma de la sesión; enviar otro ID no permite suplantar al solicitante. `/mine` devuelve solo las solicitudes propias y `/pending` solo pendientes para Administrador. La última petición devuelve 403. No hay parámetros para pedir las solicitudes de otro usuario.

### RF-GP-03 y RF-GP-05: aprobación y efecto inmediato

Primero comprueba el 403 sin permiso; después aprueba con el Administrador:

```powershell
Invoke-RestMethod "$base/api/workshop/orders" -Headers $userAHeaders
Invoke-RestMethod "$base/api/permissions/requests/$($requestA.id)/approve" -Method Post -Headers $adminHeaders
Invoke-RestMethod "$base/api/workshop/orders" -Headers $userAHeaders
```

La aprobación devuelve `Applied`, `resolverId` y `resolvedUtc`. La consulta posterior devuelve 200 con la misma sesión de A. La resolución y la concesión se guardan juntas: un conflicto revierte ambas. En SQLite se puede inspeccionar `PermissionRequests` y `UserPermissions` sin modificarlas.

### RF-GP-04: rechazo con motivo

```powershell
Invoke-RestMethod "$base/api/permissions/requests/$($requestB.id)/reject" -Method Post -Headers $adminHeaders -ContentType 'application/json' -Body '{"reason":"   "}'
Invoke-RestMethod "$base/api/permissions/requests/mine" -Headers $userBHeaders
Invoke-RestMethod "$base/api/permissions/requests/$($requestB.id)/reject" -Method Post -Headers $adminHeaders -ContentType 'application/json' -Body '{"reason":"Acceso no requerido para sus funciones"}'
Invoke-RestMethod "$base/api/workshop/orders" -Headers $userBHeaders
```

El motivo vacío produce 400 y conserva `Pending`. Un motivo válido produce `Rejected`, responsable, fecha y `rejectionReason`. Se aceptan hasta 1000 caracteres y se quitan los espacios exteriores. B conserva el rechazo de acceso 403.

### RF-GP-06: transiciones prohibidas

```powershell
Invoke-RestMethod "$base/api/permissions/requests/$($requestA.id)/approve" -Method Post -Headers $adminHeaders
Invoke-RestMethod "$base/api/permissions/requests/$($requestB.id)/approve" -Method Post -Headers $adminHeaders
Invoke-RestMethod "$base/api/permissions/requests/mine" -Headers $userAHeaders
Invoke-RestMethod "$base/api/permissions/requests/mine" -Headers $userBHeaders
```

Las dos aprobaciones devuelven 409; A sigue `Applied` y B sigue `Rejected` con su resolución original. La suite también intenta `Rejected → Applied` directamente sobre la entidad y verifica el rechazo con conservación del estado y del motivo. Las operaciones concurrentes usan el estado original como token de concurrencia de EF Core; una resolución obsoleta no sobrescribe otra.

### RF-GP-07: nadie resuelve su propia solicitud

C creó su solicitud cuando era Estándar. Promuévelo con el Administrador para comprobar la regla sin que el rechazo se deba únicamente al rol:

```powershell
$userC = Invoke-RestMethod "$base/api/access/me" -Headers $userCHeaders
Invoke-RestMethod "$base/api/admin/users/$($userC.id)/role" -Method Put -Headers $adminHeaders -ContentType 'application/json' -Body '{"role":"Administrator"}'
Invoke-RestMethod "$base/api/permissions/requests/$($requestC.id)/approve" -Method Post -Headers $userCHeaders
Invoke-RestMethod "$base/api/permissions/requests/$($requestC.id)/reject" -Method Post -Headers $userCHeaders -ContentType 'application/json' -Body '{"reason":"Motivo de prueba"}'
Invoke-RestMethod "$base/api/permissions/requests/mine" -Headers $userCHeaders
```

Ambas resoluciones producen 403 y la solicitud sigue `Pending`. El rol se consulta en servidor en cada petición, por lo que la sesión existente de C refleja la promoción.

### RD-06, RD-07 y RD-09: seguridad, entrada y reinicio

```powershell
Invoke-RestMethod "$base/api/permissions/requests/mine"
Invoke-RestMethod "$base/api/permissions/requests/$($requestC.id)/approve" -Method Post -Headers $userBHeaders
Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $userBHeaders -ContentType 'application/json' -Body '{}'
Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $userBHeaders -ContentType 'application/json' -Body '{"permission":7}'
Invoke-RestMethod "$base/api/permissions/requests" -Method Post -Headers $userBHeaders -ContentType 'application/json' -Body '{"permission":"Administrator"}'
```

Se esperan 401 sin sesión, 403 para resolver como Estándar y 400 para entradas inválidas. Una solicitud con GUID válido inexistente devuelve 404 al intentar resolverla. El Administrador no crea solicitudes; una nueva solicitud por un permiso ya concedido devuelve 409.

Detén la API con Ctrl+C y vuelve a iniciarla con la misma `DB_PATH`. Consulta `/mine` con A y B y `/api/workshop/orders` con A: deben conservarse `Applied`, `Rejected`, el motivo, las fechas y la concesión. Las sesiones siguen funcionando mientras no hayan vencido o sido revocadas. Para repetir toda la demostración, usa otras cuentas o una nueva ruta de base de demostración; no borres la base habitual.

### Entrega de la Asignación 2

La entrega exige código, suite, README y [bitácora de sesión](docs/bitacora-asignacion-2.md) en el punto etiquetado `asignacion-2`. Una vez revisados, autorizados los commits y la publicación, comprueba el estado y crea/publica esa etiqueta sin mover las anteriores:

```powershell
git tag asignacion-2
git push origin asignacion-2
```

En Moodle corresponde entregar la URL del repositorio y `asignacion-2`. La implementación local por sí sola no acredita que la etiqueta ya esté publicada. Esta unidad no incluye auditoría completa, notificaciones de permisos, CI/CD ni la suite de estados del taller.

## Flujo Git de la práctica

Cada funcionalidad se desarrolla en una rama `feature/...`, con commits pequeños cuyo asunto está en imperativo y cita el requisito. Cada PR describe **Qué cambia, Por qué, Cómo probarlo y Qué NO incluye**. Los cuatro grupos mínimos son registro/activación, sesión, recuperación de contraseña y administración de usuarios. Tras fusionarlos a `main` y verificar todos los criterios:

```powershell
git switch main
git pull --ff-only
git tag practica-1
git push origin practica-1
```

En Moodle se entrega la URL del repositorio y el nombre `practica-1`. El evaluador usa ese punto etiquetado.
