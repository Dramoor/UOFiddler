# UOFiddler

## About

This is a fork of the UO fiddler. This source can be found [here](https://github.com/polserver/UOFiddler).

## Customizations Made

- Set source folder for client and files and it dynamically loads from that folder, instead of setting all paths individually.
- Can load mutiple extra maps automatically and has an xml file for setting sizes and names of custom maps. This is set inside the AppData folder.
- Can Load multiple anim.mul files automaticallyl and has an xml for setting the H L P slots for the added anim files. This is located in teh AppData folder.
- Can Select multiple settings in items, land tiles, texture and gumps tabs to allow for multiple removal or exporting of images.
- Can select a save files as uop to auto convert files to uop after saving for the items, land tiles, texture, sounds and gumps tabs.
- Has Dynamic Searching on the items tab with filters. You turn on in the misc menu.
- Can set hue now on the items tab in the Misc setting. You can also export the images in the hues or not.
- Can right click an image in the items tab view to select in all, to auto select in the cliloc, tiledata and radcolor tabs. (this setting works better/faster if u load those first)
- Can set the default cliloc for items in the Tiledata tab and clicking set. You still have to save the cliloc manually.
- MultiCollection.UOP can load and save in the Multi tab when using the MuliCollection.UOP file, it will auto load this. (this is still in a more testing phase and keeping a backup of your original is always good. This does re load in the classicuo/tazuo/osi classic client).
- All images export as the decimal value instead of the hexidecimal value.
- Animation Edits can now do a lot of new features.
- Can move an action from one spot to another.
- Can export and move any actions around in the vd remapper. You can also convert an L to an H with this.
- Can save frames individually if wanting.
- Can export a png in all directions for a specific action.
- Can export vd to a scaled size (10 percent to 100 percent and is still in testing phase).

## Requirements

- Requires .NET Desktop Runtime 10.0.x (or SDK) installed to run the application.
- You can download .NET 10.0 [here](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- Minimum supported Windows version is Windows 10.

## Building

You'll need Visual Studio 2026 v18.0 or newer, .NET 10.0 SDK and .NET desktop development workload.

## Reporting bugs and issues

Please report any bugs or issues [here](https://github.com/Dramoor/UOFiddler/issues).
