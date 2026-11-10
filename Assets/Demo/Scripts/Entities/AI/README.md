# Minions

`Minion` es una unidad con autoridad del servidor. No selecciona enemigos ni decide
qué habilidad usar por sí sola. Todos los tipos de minions pertenecen al namespace `GonzoDemo.AI`.

## Montaje del prefab

1. Agregar `Minion` al objeto raíz. Requiere `NetworkObject`, `NetworkTransform`
   con autoridad del servidor, `NavMeshAgent`, `Rigidbody`, `CapsuleCollider` y
   `MinionSensor`. El Rigidbody se configura cinemático; el NavMeshAgent mueve
   únicamente en el servidor. Ajustar la cápsula y el agente al modelo y hornear
   el NavMesh. Si el prefab tiene un CharacterController, evitar duplicar la cápsula
   o ejecutar otro controlador que escriba la posición.
2. Elegir **un** ataque: `MinionMeleeAttack` o `MinionRangedAttack`.
   En rango asignar un prefab con `MinionHomingProjectile`; su movimiento dirigido
   es cinemático, sin Rigidbody ni collider, e impacta únicamente en su objetivo.
   No comprueba paredes: extender el proyectil si el juego requiere obstrucciones.
3. Agregar `MinionEnemyBehaviour` para detectar jugadores y volver al punto de
   aparición cuando se supera `LeashRadius`. Agregar `MinionPetBehaviour` para
   seguir al dueño, defenderlo y sumarse al combate. Ambos pueden coexistir:
   enemigo actúa sin dueño y mascota con dueño. `SetOwner(null)` devuelve el control
   al comportamiento autónomo instalado; sin ese componente la base queda pasiva.
4. Agregar opcionalmente `MinionBuffs`, `MinionAbilities` y `MinionRewards`.
   Registrar el minion, proyectil y cada pickup en NetworkPrefabs.
5. Conectar `onDeath`, `OnWindup` y `OnAttack` a animación/audio. Muerte se replica
   a los observadores; los drops se generan una sola vez en el servidor antes del
   despawn. Ajustar `deathDelay` a la duración de la animación.

El sensor crea una esfera trigger hija en la capa Ignore Raycast. Habilitar las
colisiones de esa capa con las capas de unidades en la matriz de física. Excluirla
de máscaras de armas personalizadas que incluyan explícitamente todas las capas.
Los eventos `UnitEntered/UnitExited` se emiten en el servidor una vez por unidad,
aunque tenga varios colliders. `Nearby` contiene unidades vivas, no objetos arbitrarios.

## API (servidor)

```csharp
minion.SetMaxHealth(200);
minion.SetHealth(200);
minion.SetChakra(50);
minion.SetSpeed(4);
minion.SetAttackSpeed(1.5f); // ataques por segundo; 0 pausa los ataques
minion.SetArmor(20);
minion.SetMagicResistance(10);
minion.MoveTo(destination);
minion.TargetTo(center); // posición alrededor del punto
minion.SetOwner(player); // objeto con NetworkObject spawneado
minion.SetEnemy(enemy);
minion.SetOwner(null);
minion.Kill();
```

Las mutaciones se rechazan en clientes: enviar solicitudes mediante el sistema de
comandos autorizado del juego y ejecutarlas en servidor. Stats, dueño, enemigo,
muerte, slots y cooldowns se replican. `MoveTo` devuelve false si no hay NavMesh
válido. Un comportamiento activo puede sustituir una orden de movimiento.

Ataque: acercamiento hasta `Range`, preparación de 0.5 segundos, impacto y cooldown
de `1 / AttackSpeed`. Cada objetivo coordina los turnos de los minions que lo atacan,
con reintentos aleatorios y `GroupRecovery` entre turnos. Para rango se reserva además
la vida máxima del proyectil para evitar impactos acumulados. Un ataque se cancela
si cambia/muere el objetivo, sale de rango o se deshabilita el atacante.
Esta coordinación no arbitra ataques de jugadores ni daño de área incidental.

`MinionAbilities.catalog` es una lista idéntica en todos los peers. `Grant(slot, index)`
otorga una habilidad; `Grant(slot, -1)` la retira. `TryUse(slot, target)` verifica chakra
y cooldown. No hay selección automática de habilidades. Crear definiciones propias
derivando de `MinionAbility`; hay un ejemplo `MinionHealAbility` en Create > Minions.
No guardar estado por unidad en los ScriptableObjects.

`MinionBuffs.Add(stat, amount, seconds)` devuelve un identificador para retirar el
buff. Los buffs temporales aditivos admiten Speed, AttackSpeed, Armor y
MagicResistance. Mientras un stat tenga buffs, usar `SetBaseStat` del componente
para cambiar su valor base. Los efectos finales se replican como stats; la lista de
buffs y su duración son internas del servidor.

`MinionRewards.Drops` configura probabilidad y cantidad de prefabs. Para ítems usar
un `ItemSack` con ItemId/Amount configurados en su prefab; para experiencia usar
`ExperienceBall`. Dinero requiere un pickup de moneda del juego o representarlo
como ítem: este cambio no introduce una economía nueva. `OnReward` permite otra
integración de recompensa en servidor.

`DamageResolver` notifica daño a jugadores y enruta proyectiles hacia minions
preservando tipo físico/mágico. La mascota reacciona a esos eventos. Otros sistemas
de daño que modifiquen salud directamente deben usar el resolvedor o reportar el
daño mediante `MinionCombat.ReportDamage`. Los contratos `IHittable` se aceptan en
servidor; no se otorga a clientes permiso para declarar daño arbitrario.

## Verificación en Unity (host y cliente remoto)

- Spawnear una base sin comportamiento: permanece inmóvil. Cambiar stats en servidor
  y comprobarlos en cliente y en un cliente que entra tarde; bajar máximos limita
  vida/chakra actuales. Las llamadas de mutación en cliente no producen cambios.
- Detectar entrada/salida con un jugador de varios colliders: un evento por unidad.
  Desactivar o despawnear el objetivo limpia Nearby.
- Probar MoveTo fuera de NavMesh y SetOwner(null), sin dueño previo ni posterior.
- Dos enemigos contra un jugador: observar windup, turnos separados, cancelación al
  alejarse y regreso al origen fuera de LeashRadius; repetir con ataque de rango.
- Mascota: seguir al dueño, reaccionar al daño recibido/causado, retomar seguimiento
  al morir el enemigo y liberar al dueño cuando muere/despawnea.
- Apilar buffs, retirarlos en distinto orden y dejar expirar; cambiar base con buffs.
  Otorgar/retirar habilidad y comprobar chakra, cooldown y aislamiento entre unidades.
- Aplicar daño letal varias veces: una recompensa, evento visual en ambos peers y
  despawn después del delay. Comprobar pickups registrados y recogerlos en cliente.

La compilación C# por dotnet no sustituye estas pruebas de Play Mode ni la generación
de código RPC de Unity Netcode.
