# First Person View mod for PlateUp!

This mod, as advertised, gives you a first person mode for PlateUp!, the roguelite game created by It's Happening and Yogscast.  

## What does this mod do?

A first-person view changes how players experience running their restaurants, from how they arrange the place to how they move around it.  This mod provides that view, with a few adjustable parameters to help tailor the experience to any player's preference.

## What do I need to use this mod?

You will need to use PreferenceSystem, KitchenLib, and HarmonyX.  If you are playing the game via Steam, the Specific Workshop items you will need are as follows: 

* HarmonyX: https://steamcommunity.com/sharedfiles/filedetails/?id=2898033283
* KitchenLib: https://steamcommunity.com/sharedfiles/filedetails/?id=2898069883
* PreferenceSystem: https://steamcommunity.com/sharedfiles/filedetails/?id=2949018507

## Controls

Global:
* F5: Toggle between first-person and third-person.
* F6: Toggle player model visibility.

For keyboard and mouse:
* Move: WASD
* Look: Mouse

For controllers:
* Move: Left thumbstick or Dpad.
* Look: Right thumbstick.

Note: You can also toggle first person mode via Options->Preference System->First Person View.  There is currently no controller-based toggle binding.

## Installation instructions

Please make sure you have all three of the mods listed above!  Without all three, the game may fail to start.  This mod, currently, does not check whether you have these.  This will be rectified in future.

When this mod is on Steam Workshop, simply subscribe to it (and the other three mods).

If you are installing this mod manually:
1. Locate your PlateUp installation directory.  This should contain the PlateUp executable.
2. Go into the `Mods` folder.  If `Mods` does not exist, create it.
3. Create a folder called `FirstPersonView`.
4. Create a folder called `content`.
5. Place `FirstPersonView.dll` in the `content` folder.

Note: All file and directory names are case sensitive.  Ensure folders and files are named exactly as indicated above!

## Current Features

* First person camera with keyboard + mouse and controller support.
* Easy toggling of camera perspectives.
* General multiplayer support.
* Some UI pop-ups switch the perspective back to first-person.
* Some UI indicators in the world-space face towards the player while in first-person.
* Pops back to third-person when crane mode is activated.

## Issues

* Most UI indicators do not look at the player in first person.  Those that do, may focus on another player if you are in a multiplayer lobby.
* Multiplayer specific:
  * If the lobby host enters crane mode, first person mode is disabled for all players.
  * If a non-host player enters crane mode, first person does not disable properly.
* Unconfirmed: A restaurant failing may cause a game crash.

## Who am I?

I am just a PlateUp! player.  I enjoy the game, and have done for a long time.  I noticed some bugs in a Steam Workshop version of the mod based on Spiffy's version, and figured I'd try fix something.  My coding skills and knowledge are not to any professional standard or level, so don't expect the code to be there either.

## Attributions

This version of the mod is forked from that by quackandcheese, itself based on SpiffySnail's code.

Neither of the original pages I found this mod from indicated anything relating to licensing or attributions, but I'm not an ass.  quack and Spiffy put time into their respective iterations of this mod, and I thank both of them for that and their efforts.

As to licensing of this version—I'd assume either CC0 or some sort of attribution-required license, depending on whether any of PreferenceSystem, KitchenLib, or Harmony have any.

## See also

* https://github.com/quackandcheese/plateupFPV
* https://github.com/SpiffySnail/plateupFPV