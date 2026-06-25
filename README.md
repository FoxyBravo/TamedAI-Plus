# TamedAI-Plus

Community fork of [PetAI](https://github.com/G3rste/petai) by G3rste, featuring smarter pet AI, Roam and Guard commands, map tracking for tamed animals, enhanced pet profiles, chewing bone durability, and improved companion behavior.

This Vintage Story mod serves as a library for animal taming mods such as [Wolftaming-Plus](https://github.com/FoxyBravo/Wolftaming-Plus).

<br>

## New Features (vs upstream PetAI)

| Feature | Description |
|---------|-------------|
| **Roam command** | Pets roam freely with no fixed position, exploring their surroundings |
| **Guard command** | Pets guard a 50-block radius of the command point, ideal for base defense |
| **Command + aggression combos** | Each complex command (Follow/Stay/Roam/Guard) now has individual tooltips for all four aggression levels |
| **Map tracking** | Tamed pets appear on the map under "Owned creatures" with waypoints that reflect downed/healed/caged status |
| **Enhanced pet profiles** | GUI for naming, viewing generation/size, toggling breeding, and abandoning pets |
| **Improved pathing** | Pets avoid walls, pits, fire, and lava when following or roaming |
| **Enemy awareness** | Wild wolves have a 50% chance to ignore tamed dogs; enemies ignore downed/mortally-wounded pets |
| **Chewing bone** | Throwable toy with 150 durability; dogs fetch and return it; uses red meat in recipe |
| **Pet carrier** | Pickup and transport smaller tamed pets |
| **Faster pet tracker** | Waypoint updates every 0.5s for near-realtime map tracking |
| **Hunting dog balance** | Fetch speed normalized to match tamed wolf pace |
| **Bone stacking** | Only undamaged chewing bones stack; used bones stay as singletons |

<br>

## API Information

If you want to use this to create your own pets, check out [Wolftaming-Plus](https://github.com/FoxyBravo/Wolftaming-Plus).

<br>

### Behaviors and AITasks

Here is a short overview over the most important entity behaviors and tasks:

* **behaviors**:
  * **tameable**: indicates that an entity is tameable, contains the following attributes
    * **size**: the size of the pet used to determine the size of their pet cushion, valid values are small | medium | large
    * **disobediencePerDay**: Indicates in percent how much disobedience (likelihood your pet won't follow orders) the pet gains per day if not getting care
    * **treat**: list of treats you can feed your pet to tame it / increase its obedience, each treat contains the following attributes
      * **code**: code of the item
      * **domain**: mod domain of the item
      * **progress**: how many percent of taming progress/obedience does the pet gain when receiving this treat
      * **cooldown**: cooldown in in-game hours before the pet will accept food again
  * **receivecommand**: indicates that the pet can be trained to execute certain commands
    * **availablecommands**: there are commands of various complexity available for your pet, for example:
      ```
      { "commandName":"sit", "commandType":"SIMPLE", "minObedience":0.2 },
      { "commandName":"lay", "commandType":"SIMPLE", "minObedience":0.2 },
      { "commandName":"speak", "commandType":"SIMPLE", "minObedience":0.2 },
      { "commandName":"followmaster", "commandType":"COMPLEX", "minObedience":0.6 },
      { "commandName":"stay", "commandType":"COMPLEX", "minObedience":0.1 },
      { "commandName":"roam", "commandType":"COMPLEX", "minObedience":0.1 },
      { "commandName":"guard", "commandType":"COMPLEX", "minObedience":0.3 },
      { "commandName":"NEUTRAL", "commandType":"AGGRESSIONLEVEL", "minObedience":0 },
      { "commandName":"PROTECTIVE", "commandType":"AGGRESSIONLEVEL", "minObedience":0.5 },
      { "commandName":"AGGRESSIVE", "commandType":"AGGRESSIONLEVEL", "minObedience":0.7 },
      { "commandName":"PASSIVE", "commandType":"AGGRESSIONLEVEL", "minObedience":0.8 }
      ```
  * **raisable**: now obsolete, use vanilla grow instead
* **aitasks**:
  * **petmeleeattack**: basically the same as the vanilla meleeattack, but necessary if your pet should fight alongside you
    * **isCommandable**: if set to true, your pet can aid you in combat and react to your commands
  * **petseekentity**: basically the same as the vanilla seekentity, but necessary if your pet should fight alongside you
    * **isCommandable**: if set to true, your pet can aid you in combat and react to your commands
  * **simplecommand**: lets your pet play an animation on command, useful for implementing things like sit, speak, flip; has the same attributes as the vanilla idle task
  * **followmaster**: necessary for your pet to be able to execute the follow command; has the same attributes as the vanilla staycloseto task
  * **stay**: necessary for your pet to be able to execute the stay command; has the same attributes as the vanilla staycloseto task
  * **seeknest**: lets your pet seek its cushion at certain times of day; contains attributes of vanilla idle task
  * **avoidfire**: pets will path around fire and lava
  * **happydance**: pets perform a happy animation when interacting

<br>

### Pet Accessories

Your pets can also wear accessories, armor and backpacks (see [dog collar](https://github.com/G3rste/wolftaming/blob/main/resources/assets/wolftaming/itemtypes/dogcollar.json)). Since 1.20 this uses the vanilla mechanics (see the tamed elk for reference).

If the item should also work as armor, add the `damageReduction` attribute (lets you set the damageReduction in percent, see [dog armor](https://github.com/G3rste/wolftaming/blob/main/resources/assets/wolftaming/itemtypes/dogarmor.json)).

<br>

## In-Game Handbook

Open the handbook and search for **"Taming Guide: General Concept"** for a detailed walkthrough of commands, aggression levels, items, and map tracking.

![Thumbnail](petai.png)