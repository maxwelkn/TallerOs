# TallerOS

API ASP.NET Core 10 para el taller. Este primer incremento incorpora registro, activación por correo y una cola SMTP independiente. Los demás módulos de la Práctica 1 se integran en los siguientes pull requests.

## Ejecutar

Desde la raíz del repositorio, instala el SDK de .NET 10 y ejecuta:

```powershell
dotnet restore TallerOs.slnx
dotnet run --project src/TallerOs.Api --no-launch-profile --urls http://localhost:5000
```

La base SQLite se crea en `data/talleros.db` y conserva los datos al reiniciar. `GET http://localhost:5000/health` devuelve `{"status":"ok"}`. `DB_PATH` permite elegir otra ruta. `APP_BASE_URL` indica la URL base que aparecerá en el correo de activación; por defecto es `http://localhost:5000`.

## Registro y activación

`POST /api/access/register` recibe JSON con `name`, `email`, `password`. La contraseña exige ocho caracteres, letras y números. Un correo repetido o mal formado se rechaza. `POST /api/access/resend-activation` recibe `email` y devuelve siempre la misma respuesta. El enlace recibido abre `GET /api/access/activate?token=...`; no puede usarse dos veces ni después de vencer.

La petición guarda el correo en la cola sin contactar SMTP. Para enviarlo, configura `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASSWORD` y `SMTP_FROM` en variables de entorno de una segunda terminal y ejecuta:

```powershell
dotnet run --project src/TallerOs.Api --no-launch-profile -- send-mail
```

Usa el mismo `DB_PATH` en ambas terminales si cambiaste la ruta por defecto. El comando envía los correos pendientes; repetirlo no reenvía los marcados como enviados. Nunca guardes valores de credenciales en el repositorio.
