# Guía de estudio: Práctica 1

Estudia cada bloque después de ejecutar sus rutas. Para debatirlo en Chat, comparte este archivo y pide que te haga preguntas sin darte la respuesta de inmediato.

## Registro y activación

- Explica por qué el correo es único, la cuenta nace inactiva y el token se guarda como hash.
- Investiga `PasswordHasher<T>`, sal, hash de contraseñas y diferencia entre hash y cifrado.
- Sigue una petición desde `AccessEndpoints` hasta `AccessService`, `AppDbContext` y SQLite. Identifica la responsabilidad de cada componente.
- Pregúntate qué pasa si el correo falla después de registrar el usuario y cómo ayuda la cola persistente.

## Sesiones y autorización

- Explica cómo se crea, valida, vence y revoca una sesión. Diferencia autenticación de autorización.
- Busca `RoleGuard` y comprueba por qué un usuario Estándar recibe 403 en rutas de Administrador aunque escriba la URL manualmente.
- Analiza el bloqueo temporal de intentos y por qué la respuesta a correo inexistente y clave incorrecta debe ser igual.

## Recuperación de contraseña

- Sigue el ciclo del código de recuperación: generación, hash, vencimiento, consumo y rechazo al reutilizarlo.
- Explica por qué se invalidan todas las sesiones al restablecer o cambiar la clave.
- Compara la respuesta de recuperación para un correo existente y otro inexistente. Explica qué información protege.

## Administración y diseño

- Recorre los cambios de rol, activación de cuentas y restablecimiento forzado. Identifica dónde se evita desactivar al propio Administrador.
- Dibuja las relaciones `Customer → Vehicle → WorkOrder → Diagnosis` y explica las transiciones permitidas de una orden.
- Relaciona el principio de responsabilidad única con `AccessService`, `OutboxSender`, `RoleGuard` y el modelo del taller. Señala una mejora posible para dividir `AccessService` cuando el proyecto crezca.
- Revisa los cuatro PR y explica por qué cada uno puede compilar de forma aislada.

## Preguntas para comprobar comprensión

1. ¿Qué diferencia hay entre un token de activación, un código de recuperación y una sesión?
2. ¿Por qué la base persiste después de reiniciar la API?
3. ¿Qué prueba demuestra que el correo se entrega realmente y no solo se encola?
4. ¿Qué error esperas al pasar una orden directamente de `Received` a `InRepair`?
5. ¿Qué parte cambiarías si mañana se usa otro proveedor SMTP sin cambiar las reglas de acceso?
