# TallerOs

TallerOs ayuda a administrar talleres de automóviles y motocicletas, centralizando clientes, vehículos y órdenes de reparación.

API REST escrita en **C#** (ASP.NET Core) para administrar clientes, vehículos,
órdenes de reparación, repuestos e inventario de un taller.

## Estado del proyecto

El proyecto se encuentra en desarrollo. Actualmente el repositorio contiene la
documentación inicial; el andamiaje de la API se incorporará en próximos
commits, por lo que los comandos de ejecución del backend todavía no están
disponibles.

## Stack tecnológico

| Capa          | Tecnología                    | Estado    |
| ------------- | ----------------------------- | --------- |
| Lenguaje      | C#                            | Definido  |
| Framework     | ASP.NET Core (API REST)       | Definido  |
| Versión .NET  | Por definir                   | Pendiente |
| Persistencia  | Por definir                   | Pendiente |
| Autenticación | Por definir                   | Pendiente |
| Documentación | Por definir (Swagger/OpenAPI) | Pendiente |

## Requisitos previos

- **Git** para clonar y colaborar en el repositorio.
- **.NET SDK** (versión por definir) para compilar y ejecutar la API cuando el
  código esté disponible.
- Un editor: Visual Studio, Visual Studio Code o Rider.

## Clonar el repositorio

### HTTPS (repositorio público)

```bash
git clone https://github.com/maxwelkn/TallerOs.git
cd TallerOs
```

### SSH

```bash
git clone git@github.com:maxwelkn/TallerOs.git
cd TallerOs
```

La variante SSH requiere tener una clave configurada en tu cuenta de GitHub
(clave pública en `Settings` > `SSH and GPG keys`).

### Verificar la clonación

```bash
git remote -v
git status
```

La rama por defecto del repositorio es `main`.

## Estructura del repositorio

```text
TallerOs/
├── .gitignore
├── README.md
└── docs/
    └── bitacora-asignacion-1.md
```

El directorio `src/` se creará cuando se defina la estructura de la solución de
la API.

## Flujo de trabajo

- Trabaja en ramas propias con prefijo: `feature/...` para funcionalidad,
  `docs/...` para documentación y `fix/...` para correcciones.
- No hagas push directo a `main`; sube tu rama y abre un Pull Request.
- Sigue el estilo de commits ya usado en el repositorio:

```text
docs: add assignment 1 log
chore: add .NET ignore and welcome function
refactor: keep branch limited to dotnet ignore
```

Antes de abrir el Pull Request revisa tus cambios:

```bash
git status
git diff
```

## Documentación

- `docs/bitacora-asignacion-1.md` — bitácora de la asignación 1 (Git, ramas,
  commits y colaboración).

## Próximos pasos

1. Definir la versión de .NET y la estructura de la solución.
2. Modelar el dominio: clientes, vehículos, órdenes de reparación y repuestos.
3. Definir la capa de persistencia y el esquema de base de datos.
4. Definir autenticación y autorización.
5. Incorporar el andamiaje de la API con un endpoint de salud.
