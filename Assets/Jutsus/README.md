# Jutsus

Cada jutsu agrupa sus archivos en `Assets/Jutsus/<elemento>/<jutsu>/`.

```text
fire/
  fireball/
    Fireball.asset                 # JutsuDefinition: balance e información
    FireballBehaviour.asset        # Instancia configurada del comportamiento
    FireballJutsuBehaviour.cs       # Implementación del comportamiento
    prefabs/Fireball.prefab         # Proyectil de red
    assets/                        # Icono, materiales y texturas
  multifireball/
    MultiFireball.asset
    MultiFireballBehaviour.asset
    MultiFireballJutsuBehaviour.cs
    prefabs/SmallFireball.prefab
    assets/
```

Las definiciones y los comportamientos configurados son assets de ScriptableObject,
no prefabs. Las clases base `JutsuDefinition`, `JutsuBehaviour`, `JutsuContext` y
`JutsuInstance`, y el componente `ProjectileBase`, siguen siendo infraestructura común.
No hace falta un script de definición distinto para cada jutsu.

## Crear otro jutsu

1. Crear su carpeta por elemento, con subcarpetas `prefabs` y `assets`.
2. Crear allí el script que herede de `JutsuBehaviour`. El archivo debe tener el
   mismo nombre que la clase; añadir `CreateAssetMenu` para crear su asset configurado.
3. Crear el asset del comportamiento en esa misma carpeta desde su menú de creación.
4. Crear la definición desde `Create > Content > Jutsu` dentro de la carpeta del jutsu.
   Configurar icono, costes, cooldowns, stats y la lista `Behaviours`.
5. Guardar los prefabs en `prefabs` y sus recursos propios en `assets`.
   Asignar el proyectil al comportamiento y registrarlo en la lista de prefabs de Netcode.
6. Asignar la definición al slot del `JutsuController`.

Fireball y MultiFireball tienen copias independientes de los materiales y texturas
de fuego para permitir cambios visuales por separado. Los shaders de los paquetes
de Unity y los componentes comunes se siguen referenciando desde su ubicación original.
Los prefabs se referencian directamente; no necesitan estar dentro de `Resources`.

Mover siempre los assets junto con sus `.meta` (preferiblemente desde Unity) para
conservar sus GUID y las referencias de escenas, inventarios y prefabs de red.
