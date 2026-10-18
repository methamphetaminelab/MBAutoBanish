# AutoBanish

A BepInEx mod for Megabonk. Mark the items, weapons and tomes you never want to see, and the mod presses Banish for you.

- Items are banished when they drop from a chest.
- Weapons and tomes are banished when a level-up offers them. The game closes the level-up after a banish, so that level-up is spent on it, same as when you banish by hand.

The mod clicks the game's own Banish button, so the normal rules apply: each banish uses a charge, and after the first free banish of a run the game also asks for gold. With no charges left or not enough gold nothing happens.

## Usage

Press **F6** to open the menu. Open it in the main menu or on pause, since the cursor is hidden during a run. Click a card to mark it, click it again to unmark. Changes are saved immediately.

## Config

`BepInEx/config/megabonk.autobanish.cfg`

| Key | Default | Description |
| --- | --- | --- |
| `MenuKey` | `F6` | Opens and closes the menu |
| `Items`, `Weapons`, `Tomes` | empty | Marked entries (internal names, comma separated). Filled in by the menu |

## Notes

- Item, weapon and tome names are shown in the game's language.
- Works with InstantChests.

## Install

With a mod manager (r2modman, Gale): install AutoBanish from the [Megabonk community on Thunderstore](https://thunderstore.io/c/megabonk/).

Manually: install [BepInExPack_IL2CPP](https://thunderstore.io/c/megabonk/p/BepInEx/BepInExPack_IL2CPP/), start the game once, then put `AutoBanish.dll` into `BepInEx/plugins`.

## Building

You need the .NET SDK (6 or newer) and Megabonk with BepInEx that has been started at least once, so that `BepInEx/interop` exists.

```sh
dotnet build -c Release -p:GameDir=/path/to/Megabonk/
```

Instead of `-p:GameDir` you can put the path into `Local.props` (ignored by git):

```xml
<Project>
  <PropertyGroup>
    <GameDir>/path/to/Megabonk/</GameDir>
  </PropertyGroup>
</Project>
```

Without either, the default Steam path on Windows is used. The path must end with a slash. After a build the DLL is copied into the game's `BepInEx/plugins`.

To release, bump the version in `Plugin.cs` (`BepInPlugin`) and `thunderstore/manifest.json`, then run `./pack.sh`. The Thunderstore zip ends up in `dist/`.

## License

MIT
