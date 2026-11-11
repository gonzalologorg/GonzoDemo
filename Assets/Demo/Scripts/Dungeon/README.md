# Dungeon: autoridad y encuentros

`DungeonManager` es un `NetworkBehaviour`. Solo el servidor genera la ruta, inicia
encuentros y cambia clearance. Replica la ruta, la última sala liberada, la sala
en combate y el intervalo de recompensas con NetworkList/NetworkVariable. Un cliente
tardío aplica ese estado; no genera otra ruta ni invoca eventos de encuentros.

`DungeonSpawnManager` es un componente local de ejecución exclusiva del servidor:
consulta la autoridad del DungeonManager antes de procesar triggers, coroutines,
spawns, muertes y eventos. No necesita un NetworkObject por sala. Las entidades
generadas sí tienen NetworkObject y los cierres se derivan del estado replicado.

## Configuración

1. Colocar un DungeonManager por escena y un **NetworkObject en el mismo objeto**.
   Los objetos que ya existían antes de este cambio necesitan agregarlo; el Inspector
   ofrece el botón `Add required NetworkObject`. Mantenerlo fuera de los contenedores
   de salas. Cargar la misma escena en todos los peers mediante el scene manager de NGO.
2. En cada sala agregar DungeonSpawnManager, asignar el volumen trigger (puede estar
   en un hijo) y el Transform `spawners`. Cada hijo directo de spawners es un marcador.
   Se autodetectan el nombre spawners y los colliders trigger si no hay referencias.
   Para jerarquías ambiguas, asignar `RoomObject.SpawnManager` y las referencias de sala.
   Un único encuentro por RoomObject. Los triggers necesitan detectar la capa del jugador.
3. En `Pools` definir prefabs Minion y pesos de selección. En `Waves`, una entrada por
   oleada con PoolIndex (desde 0), cantidad, intervalo de spawn y espera inicial.
   Los minions deben llevar su comportamiento enemigo/ataque y existir en NetworkPrefabs,
   al igual que proyectiles, drops y RewardPrefabs. Hornear el NavMesh para sus tipos
   de agente. Un marcador no válido para el agente aborta el encuentro y abre las puertas.
4. Ajustar Difficulty. El factor global es:
   `BaseMultiplier * Wave.Difficulty * (1 + PerRoom * (RoomIndex - 1) + PerWave * waveIndex)`.
   Este factor se multiplica por el ajuste de cada propiedad. Los valores menores que
   1 debilitan; los mayores que 1 fortalecen. Se escalan vida/chakra máximos (se rellenan),
   velocidad, velocidad de ataque, armadura, resistencia mágica y daño del componente
   de ataque. No modifica los assets de prefabs ni las definiciones de habilidades.
5. Configurar `RewardPrefabs`: se elige uno al entrar todos en una sala marcada de
   recompensa. Son las salas 3, 6, 9… por defecto, excluyendo la sala principal 0.
   La recompensa sustituye completamente a las oleadas. La sala se libera al generar
   el premio; no espera a recogerlo. El flag IsRewardRoom lo asigna DungeonManager.
6. En DoorObject asignar `blockingColliders` (sólidos); si está vacío se usan los
   colliders no-trigger de sus hijos. Opcionalmente asignar Animator y parámetro Locked.
   No incluir colliders de paredes permanentes en ese conjunto. El estado físico y
   visual se aplica en cada peer sin invocar eventos de gameplay locales.

## Secuencia

- Al generar la ruta, los spawnpoints de GameManager se reemplazan por los hijos
  directos de `spawners` de la sala principal (también admite spawners como hermano
  del RoomObject dentro del contenedor de sala). Los clientes aplican la misma sala
  recibida del servidor. GameManager elige entre esos puntos para aparición y respawn;
  actualizar la ruta no teletransporta jugadores que ya están jugando.
- RoomIndex 0 nunca dispara oleadas; ClearingIndex comienza en 0 y significa última
  sala liberada. Solo se activa la siguiente sala de la ruta.
- Mientras falta algún jugador conectado y vivo, las puertas de entrada quedan abiertas.
  Un cliente cuyo PlayerObject todavía no apareció también mantiene la espera.
  Muertos no bloquean la entrada; se necesita al menos un jugador vivo.
- Cuando todos están dentro, se cierran las puertas de la ruta y se inicia el encuentro.
  Los jugadores deben usar PlayerObject de NGO con PlayerStats en su raíz.
- Los eventos OnEncounterStarted, OnWaveStarted, OnMinionDied, OnWaveCleared,
  OnRoomCleared y OnEncounterAborted se invocan **solo en servidor**. Los índices de
  eventos de oleada comienzan en 1. Conectar lógica de juego a esos eventos;
  para UI remota usar datos replicados o un RPC propio.
- Una oleada espera a todas sus muertes antes de empezar la siguiente. Un despawn externo
  elimina al minion del conteo, pero no fabrica un evento de muerte ni una recompensa.
- La última oleada libera la sala, incrementa clearance y abre las puertas accesibles.
  Puertas hacia salas no seleccionadas o más adelante siguen cerradas.
- Desconexiones antes del inicio recalculan automáticamente la espera. Entradas tardías
  durante combate no reinician ni duplican el encuentro. Esos jugadores esperan hasta
  su finalización; su ubicación de aparición corresponde al sistema de spawn de jugadores.
- Si no queda ningún jugador vivo conectado, o falla un spawn, se cancela el encuentro,
  se retiran sus minions sin Kill/drops y se reabren las puertas. Permite reintento.

La ruta usa un catálogo estable ordenado por la jerarquía de RoomObjects de la escena;
no crear, reordenar ni eliminar estas salas de forma distinta entre peers durante
la conexión. La regeneración de ruta está bloqueada durante un encuentro.
Los objetos de sala sin NetworkObject fuera de la ruta se desactivan en todos los
peers; contenedores con objetos de red permanecen activos para no romper late join.

## Comprobación en Play Mode

- Host y cliente: misma ruta, colores, puertas y clearance, sin eventos de encuentro
  en el cliente. Conectar un segundo cliente durante una oleada y repetir después
  de liberar varias salas.
- Entrar primero con un jugador: sin spawn ni bloqueo. Entrar con el último: un solo
  encuentro. Salir/reentrar mientras se espera y probar varios colliders/triggers
  superpuestos; desconectar al jugador que falta también debe permitir el inicio.
- Matar parcialmente una oleada: puertas cerradas. Matar el último: siguiente oleada
  o clearance, exactamente una vez. Verificar ambos lados de una puerta conectada.
- Entrar en sala 0: nada. Entrar en sala 3: un premio y cero minions; salir/reentrar no
  duplica el premio. Repetir en sala 6.
- Probar multiplicadores 0.5 y 2 y comprobar stats replicados y daño en servidor.
- Desconectar/matar a todo el grupo y comprobar limpieza/reapertura. Probar pool vacío,
  premio ausente y marcador fuera del NavMesh: error visible, sin encierro permanente.
