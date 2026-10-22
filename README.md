# Feared

Feared is a single-player 2.5D action game. The Cathedral level opens outdoors in the cathedral grounds, an HD-2D diorama you walk through freely in every direction, then climbs through the inside of the building: nave, grand stair, gallery, a switchback stair tower, belfry, choir loft, and the sanctum boss. Its chain can swing from rings, yank light enemies, or zip the player toward heavy targets. Combat adds combo attacks, parries, enemy encounters, XP, and unlockable abilities.

## Run and rebuild

Open with Unity **6000.6.6f1**; package versions are pinned in `Packages/manifest.json`. The current playable scene is `Assets/_MyFiles/Scenes/Cathedral.unity` and is first in Build Settings. Open it and press Play.

To reconstruct the setup from scratch, run these Unity menu items in order:

1. `Brawler/Build Test Scene`
2. `Brawler/Setup Everything (Hero, Enemy, HUD, Packs)`
3. `Brawler/Rebuild Everything (Roster, HUD, Cathedral)`

Step 3 alone regenerates the Cathedral after code changes. It also runs headless, and `CaptureShots` renders the game camera at chosen points into `Logs/Shots`:

```
Unity.exe -batchmode -projectPath . -executeMethod Brawler.Editor.BatchTools.RebuildAll
Unity.exe -batchmode -projectPath . -executeMethod Brawler.Editor.BatchTools.CaptureShots -shot tower:86,6.6,0.8
```

## Controls

| Action | Keyboard and mouse | Gamepad |
| --- | --- | --- |
| Move | WASD or arrows | Left stick |
| Attack | J or left mouse | West face button |
| Dash | Left Shift or K | East face button |
| Jump | Space | South face button |
| Parry | L or right mouse | Right shoulder |
| Grapple | E | Right trigger |
| Menu | Tab | Start |
| Abilities | 1 / 2 / 3 | North button / left shoulder / left trigger |

On-screen prompts follow the last gameplay device used. Gamepad bindings use the generic Unity Gamepad layout.

## Project map and assets

Runtime code is under `Assets/_MyFiles/Scripts`: Core, Combat, Player, Enemy, Progression, Level, and Presentation. `Editor` contains the scene and asset setup tools. EditMode tests are in `Assets/_MyFiles/Tests/EditMode`; PlayMode tests include an opening-scene smoke check in `Assets/_MyFiles/Tests/PlayMode`. Run them with Unity Test Runner.

Third-party source packs live under `Assets/_ThirdParty`, which Git ignores. A clone alone will not reproduce the imported art/audio. Player sound effects live in `Assets/_MyFiles/Audio/SFX`. `Tools/make_sfx.py` builds them from two CC0 foley libraries ([Tinysized SFX](https://opengameart.org/content/fantasy-sound-effects-tinysized-sfx) in `Assets/_ThirdParty/AudioTinysized`, [Weapons and Apparel](https://opengameart.org/content/fantasy-weapons-and-apparel-sfx-library) in `Assets/_ThirdParty/AudioWeapons`) plus synthesized blade swings. The cathedral architecture (walls, windows, columns, vaults, floors, facade, towers, doors) is SigilsVault's CC0 [Modular Cathedral Kit](https://sigilsvault.itch.io/modular-cathedral-kit), unzipped as `Assets/_ThirdParty/Modular Cathedral Kit`; without it the builder falls back to plain stone boxes. The grounds' props are Kenney's CC0 [Graveyard Kit](https://kenney.nl/assets/graveyard-kit) (FBX in `Assets/_ThirdParty/Kenney_graveyard-kit`). The 3D breakable pots come from FabinhoSC's CC0 [Stylized Vases and Pots](https://opengameart.org/content/stylized-vases-and-pots) (FBX and textures in `Assets/_ThirdParty/Pots3D`). The score and hit counter use the pixel font sheet `Assets/_ThirdParty/pxfntfree13-16x16.png`. The browser/WebGL export is still pending and has not been validated.
