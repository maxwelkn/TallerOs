# Verificación de Práctica 4

## Estado de la unidad

Plan aprobado; incremento 1 verificado el 10 de noviembre de 2026 (hora del estudiante). Este informe distingue las comprobaciones de base de la futura evaluación funcional en contenedores. No certifica todavía cumplimiento de la rúbrica ni una entrega lista. Se pausa para revisión antes del incremento 2.

Consulta el [plan y la matriz de trazabilidad](plan-practica-4.md) para el alcance acordado. No se ha creado `practica-4` ni se han alterado etiquetas anteriores.

## Incremento 1: base de código y preparación

### Punto de partida

- Raíz comprobada con `git rev-parse --show-toplevel`.
- Checkout inicial `main`, árbol limpio, commit `44e0bded61d07c8c64e674c9979d70fe3e0ca348`.
- `git ls-remote origin refs/heads/main` confirmó el mismo commit al iniciar.
- Rama de trabajo creada: `feature/practica-4-contenedor`.
- SDK de .NET disponible: `10.0.402`.
- `AGENTS.md`, `guidelines.md` y `.codex/MEMORY.md` comprobados como ignorados; no se incorporan al historial.

### Comprobaciones ejecutadas

Desde la raíz del repositorio:

```powershell
dotnet build TallerOs.slnx
dotnet test TallerOs.slnx --no-build --verbosity minimal
```

| Comprobación | Resultado observado |
| --- | --- |
| Build | Correcto; 0 errores, 0 advertencias. |
| Unitarias | 159 aprobadas, 0 fallidas, 0 omitidas. |
| Integración | 87 aprobadas, 0 fallidas, 0 omitidas. |
| Total | 246 aprobadas, 0 fallidas, 0 omitidas. |

Las pruebas de integración existentes incluyen SQLite y una API lanzada en un proceso aislado con base temporal y cuentas ficticias. La fixture elimina las variables SMTP del proceso hijo. Esta ejecución no usa la base habitual ni envía correos externos; tampoco comprueba todavía los seis frentes de la rúbrica desde Docker.

### Entorno Docker

WSL `2.7.11.0`, kernel `6.18.33.2-2`, distribución Ubuntu configurada como WSL 2. Windows 11 Home. Al iniciar no había CLI ni Docker Desktop operativo; los archivos restantes correspondían a una desinstalación anterior.

La instalación por usuario mediante Winget no encontró un instalador aplicable. Se descargó el instalador desde el dominio oficial `desktop.docker.com` y se comprobó firma Authenticode válida de Docker Inc antes de ejecutarlo en modo por usuario y backend WSL 2. El instalador terminó con código 0. Docker Desktop se inició y su estado informó `running`.

| Comprobación Docker | Resultado observado |
| --- | --- |
| Docker Desktop | 4.99.0. |
| CLI y motor | 29.9.3 en ambos. |
| Compose | v5.7.0. |
| Contexto activo | `desktop-linux`. |
| Tipo de motor e imagen | Linux, arquitectura amd64. |
| `docker run --rm hello-world` | Correcto; imagen descargada y ejecutada, contenedor eliminado al finalizar. |
| `docker ps` posterior | Sin contenedores en ejecución. |

La descarga de `hello-world` quedó identificada como `hello-world@sha256:c567b231f65614688711a75f31518a064258a51d442117a3e771a3a8fdba78ef`. No se montaron carpetas ni bases del proyecto y no se publicó ningún puerto para esta comprobación.

La sesión de PowerShell del agente ya estaba abierta antes de instalar. Invocar solo la ruta absoluta de `docker.exe` permitió consultar versiones pero falló al descargar la imagen porque su auxiliar `docker-credential-desktop` no estaba en el PATH heredado. Se añadió el directorio de la CLI al PATH de ese proceso y se repitió el contenedor con éxito; no se borró ni se reemplazó la configuración de credenciales.

Para reproducir la comprobación, abre una terminal nueva después de instalar. Si una terminal antigua todavía no encuentra los ejecutables, actualiza únicamente el entorno de esa sesión:

```powershell
$dockerCliDirectory = Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\resources\bin'
$env:Path = $dockerCliDirectory + [IO.Path]::PathSeparator + $env:Path
docker version
docker compose version
docker info --format '{{.OSType}}'
docker run --rm hello-world
```

Esta preparación no añadió Dockerfile ni Compose. La prueba acredita el entorno Linux y el acceso al registro público; todavía no acredita que TallerOs se construya o arranque dentro de Docker, ni su persistencia.

### Material personal externo

Se prepararon el índice de Práctica 4, el tema 01 (rúbrica, arquitectura y recorrido) y el tema 02 (fundamentos de Docker) en `../estudios/TallerOs/practica-4`, enlazados desde el índice general externo. No se duplican en Git ni se trasladan las guías previas. Incluyen preguntas, ejercicios y una instrucción autocontenida para estudiar con el chatbot.

El material distingue teoría, propuesta aprobada y comportamiento comprobado. Los temas de los siguientes incrementos se desarrollarán antes de implementarlos y se completarán con sus resultados reales. No se declara cerrada la Práctica 4 ni la Práctica 3.

### Revisión documental

Se comprobaron enlaces relativos y codificación de los dos documentos del repositorio y los tres nuevos documentos externos, sin destinos locales ausentes. Se revisó el diff y se ejecutó `git diff --check`. Ningún archivo de producción, prueba, dependencia o base de datos forma parte de este incremento.

## Criterios funcionales pendientes

| Criterio | Estado |
| --- | --- |
| Documentos y autorización | Pendiente de incremento 2. |
| Arranque desde clon con Compose | Pendiente de incremento 3. |
| Persistencia tras `down` sin `-v` | Pendiente de incremento 3. |
| Integración climática real y fallos controlados | Pendiente de incremento 4; éxito real requiere clave válida solo en entorno local. |
| Revisión acumulada desde Docker | Pendiente de incremento 5. |
| Historial, secretos y capas de imagen | Pendiente de auditoría final. |
| PR, etiqueta y entrega Moodle | Pendientes; fusión y etiqueta requieren autorización expresa. |
