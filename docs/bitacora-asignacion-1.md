# Bitácora de la Asignación 1

## 1. Objetivo

Documentar una práctica de Programación III relacionada con control de
versiones, Git, ramas, commits y colaboración mediante GitHub.

## 2. Tarea delegada al agente

### Qué le pedí

Le pedí al agente que trabajara con un repositorio de un compañero, clonara
el proyecto dentro de `PIII`, agregara un comentario en el archivo `.gitignore`,
creara un commit y subiera el cambio al repositorio remoto.

### Qué devolvió

El agente clonó el repositorio `pirry-ledger`, trabajó en la rama
`feature/create-gitignore` y agregó el comentario:

```gitignore
# Muy bien - maxwelkn
```

Después creó el commit:

```text
14f434c docs: add positive comment to gitignore
```

Inicialmente el push fue rechazado porque la cuenta no tenía permisos de
escritura. Más adelante, al verificarse nuevamente el acceso, el push se
completó correctamente.

## 3. Error cometido

### Error

Durante la primera preparación de esa tarea, el agente aplicó el cambio del
`.gitignore` en el repositorio equivocado, `TallerOs`, en vez de aplicarlo en
`pirry-ledger`.

### Cómo lo detecté

El error se detectó revisando el estado del repositorio y comparando el diff.
El comando `git status` mostró que `.gitignore` había sido modificado dentro
de `TallerOs`, aunque la tarea correspondía al repositorio del compañero.

### Cómo lo corregí

Se restauró el `.gitignore` original de `TallerOs`, se eliminó la copia local
incorrecta de `pirry-ledger` y luego se repitió el trabajo apuntando al
repositorio y a la rama correctos.

## 4. Flujo de trabajo utilizado

El flujo seguido fue:

1. Inspeccionar el estado del repositorio antes de modificarlo.
2. Clonar el repositorio del compañero dentro de `PIII`.
3. Cambiar o confirmar la rama de trabajo.
4. Modificar únicamente el archivo `.gitignore`.
5. Revisar el diff antes de crear el commit.
6. Crear un commit atómico con un mensaje descriptivo.
7. Ejecutar `git push` y verificar el resultado remoto.

## 5. Resultado y aprendizaje

La práctica mostró la importancia de confirmar el repositorio, la rama y el
working tree antes de editar. También permitió comprobar que un commit local
no significa que el cambio ya esté publicado: el push debe ejecutarse y
verificarse por separado.
