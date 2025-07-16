# EasyLightLevels
A Vintage Story mod that allows you to easily see the light levels of blocks around you.

Download the compiled DLL here: [mods.vintagestory.at/easylightlevels](https://mods.vintagestory.at/easylightlevels)

This mod was made with the Vintage Story Visual Studio template mod. To use it yourself, download it from the wiki on [this page](https://wiki.vintagestory.at/index.php?title=Modding:Setting_up_your_Development_Environment), run the modtools setup, add a dll mod with id "easylightlevels", and place this repo in mods-dll/easylightlevels/ (... I believe. There might be more to it, but this seemed to work).

---
### Changelog

- **Removed** support for configuring lighting rules via the config file.
- **Added** `F7` as the default hotkey to toggle light levels.
- **Added** a color-aid mode:  
  Use `.lightlvl colorAid` or `lightlvl ca` to enable it.
- **Updated** behavior so that blocks with a high light level will no longer be highlighted.
- **Changed** the default radius to `32`.

### Motive

Since **ElPatron** is unable to distinguish green from red, he wasn’t able to benefit from this mod.  
I wanted to make it more accessible for him by adding a **color-aid mode**.

While I was at it, I also **refactored** and **reworked** the mod a bit.