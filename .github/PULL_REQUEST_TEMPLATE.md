<!--
Guía de uso: completa las cuatro secciones de abajo.
Estas explicaciones son solo de ayuda; no se muestran al renderizar el PR.
-->

## Qué cambia

<!--
Inventario concreto del diff: archivos, rutas y qué se hizo con cada uno
(agregar, modificar, eliminar). Es lo primero que lee el revisor y permite
detectar de inmediato si el PR hace más de lo que dice.
Usa el formato ruta — descripción para nombrar rutas reales, en vez de
escribir "varios cambios".
-->

- Agrega / Modifica / Elimina: `ruta/del/archivo` — descripción breve.

## Por qué

<!--
Justificación del problema que resuelve el PR: qué faltaba, qué se rompe o qué
objetivo del proyecto atiende. Es la sección que separa un PR razonable de un
PR imposible de revisar sin contexto. Basta una o dos frases, no un ensayo.
-->

## Cómo probarlo

<!--
Pasos verificables para que otra persona reproduzca y compruebe el cambio sin
preguntarte nada. En un backend como TallerOs será una secuencia concreta
(dotnet build, dotnet run, curl contra el endpoint, revisar logs).
Alterna comandos exactos con el resultado esperado.
-->


## Qué NO incluye

<!--
Frontera explícita del alcance: lo que deliberadamente queda fuera. Los PRs
reales del repo usaron esta sección para aclarar que no se agregaba base de
datos, ni endpoints, ni lógica de negocio. Evita el scope creep y reduce los
comentarios de revisión.
-->

- No modifica la rama `main`.
