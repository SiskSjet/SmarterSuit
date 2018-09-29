# Smarter Suit
![SmarterSuit](./Mod/thumb.png)

* [Info](#info)
* [Credits](#credits)

## Info

A small suit software update that prevents death due to malfunction of the suit.

The new suit update will now check some parameters when leaving a cockpit or after a respawn to eliminate potential sources of danger.
In addition, the speed is adjusted relative to the ship.

**helmet**
* opened when enviroment with oxygen detected.
* closed when enviroment without oxygen detected.

**jetpack**
* enabled when no gravity detected or gravity detected and no ground in range.
* disabled when gravity detected and ground is in range.

**dampeners** 
* enabled when ship is not moving or when no ground in range and in planetary gravity well.
* disabled when ship is moving.

## Options

* **AlwaysAutoHelmet/AAH** (*boolean*) - Will check everytime if helmet is needed. I don't recomend this setting for multiplayer with airthigness enabled, because of an bug with cockpits.
* **AdditionalFuelWarning/AFW** (*boolean*) - Will play a additional fuel warning at given threshold.
* **FuelThreshold/FT** (*float*) - The fuel threshold used for additional fuel warnings. Default is 0.25. Range is 0-1.
* **DisableAutoDampener/DAD** (*byte*) - Option to disable automatic dampener changes. 0 = Disabled | 1 = only mod changes disabled | 2 All dampener changes disabled.
* **HaltedSpeedTolerance/HST** (*float*) - Option to adjust the speed tolerance that declares a ship specifies as not moving. Default is 0.01.

## Commands

`Usage: /ss [command] [arguments]`

**Available commands**:
* **Enable** [*option*] *- Enables an option*
* **Disable** [*option*] *- Disables an option.*
* **Set** [*option*] [*value*] *- Set an option to value.*
* **List** *- Lists all options.*
* **Help** *- Shows a help window with all commands.*

## Mod Support

[Remove all automatic jetpack activation](https://steamcommunity.com/sharedfiles/filedetails/?id=782845808) This mod will then not change the jetpack status.

## Credits

Icons used in this mod:
* All icons were recreated as a vector icon.
* helmet icon from [SpaceEngineers](https://www.spaceengineersgame.com) by [Keen Software House](https://www.keenswh.com)
* jetpack icon from [SpaceEngineers](https://www.spaceengineersgame.com) by [Keen Software House](https://www.keenswh.com)
* dampener icon from [SpaceEngineers](https://www.spaceengineersgame.com) by [Keen Software House](https://www.keenswh.com)
  
