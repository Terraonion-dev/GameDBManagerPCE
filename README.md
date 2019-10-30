# CartDBManagerPCE
Screenshots manager for Super SD System 3

## Usage

If you can't compile the source, or just want to use the tool, you can grab a precompiled build and sample library from the [Built](Built) directory

### Adding games to the library
- First, prepare a 64x64 screenshot for each game you want to add
- Go to the very last list entry, marked with an *, and write the game name in the first column. A new row will be created for your game.
- Fill in the year and genre.
- Press the button in the "Screenshot" column and select the PNG file you want to appear in the SSDS3 menu
- Press the button in the "Hashes" column. In this screen, select "Add Hash", and then point to the cd .cue file or rom .pce you want to associate with this game. A game can have more than 1 hash attached for different versions or region variations, and they will all share the same screenshot.
- Repeat these steps for all games you want to add.
- Once finished adding games, press "Convert Imgs" button. This will convert your png files to console tilemaps. It will only convert the new files, so if you change the image after generating the tile cache, just delete it from the cache and then regenerate images.
- Once the database is built, it can be used to generate the game images, or to distribute it.
- For distribution, you must distribute the db.xml file, the executable, and the TileCache directory. The original images aren't needed, as they have been converted to tile data previously.

### Scanning roms
- Once you have built your DB, or have acquired a pre-built one, you can scan your SD card for known games, so their screenshots are stored in a file that SSDS3 can use to show them on screen.
- Just press the "Scan Roms" and point to your SD card root, or any directory with roms.
- When finished, a "Games.db" file will be created in each directory with games having screenshots. This file is specific to your directory structure, so sharing it might not work if other user games aren't named the same way.

### Creating suitable images
As commented above, png files used must be 64x64 pixels in size, but there are more restrictions.
Because only the tile layer is used, and a part of the palettes are used for the menu, only 13 16-color palettes can be used so you might need to reduce your image color count.

Then the tilemap conversion code will attempt to reduce the colors by first discarding color information not used by the console, then merging similar colors... but if it can't make all them fit in 13 palettes, an error will show in the program console screen when this image is processed with "Convert Imgs" button, and you'll need to further reduce the colors.

## Games.db structure
If you want to build you own tool from scratch, here is the internal working of the files created on each directory after scanning:

The file is divided in 3 sections:

### CRC Section: 4 * 1024 bytes fixed size
Each 4 byte entry of this table is the 32-bit checksum of the filename for a ROM (for rom games) or directory (for cd games).
This checksum is computed over the first 56 bytes of the file or directory name, converted to uppercase. The algorithm to use is the one in the provided in the Crc32 class, that is the same than the firmware uses to compute the crc on its side when scanning directories. Just compute it with Crc32.update_crc(0xFFFFFFFF, namecnv, 56).

It's stored in little endian format. **This table must be sorted by checksum value, so the firmware can perform a fast binary search on it**

### Screenshot ID Section: 2 * 1024 bytes fixed size
Each 2 bytes in this table store a little endian 16-bit screenshot index for the next section. This table is indexed by the position of the crc in the previous table, so keep in mind this when sorting the CRCs. When moving a CRC value to a different position while sorting, the corresponding screenshot value must also be moved to match the new position. Several games can have the same screenshot ID if they are just rom version or regional variations with the same screenshot, saving space

### Screenshots Section: 3072 * num screenshots, variable size, max 1024 screenshots.
Each 3072 entry on this table is further divided in 4 sub parts:

- Screenshot tiles: (offset 0) 2048 bytes: Tiles are stored in VDC order as is, ready to be transferred to the VDC. 8x8 tiles, 8x8 pixels each, 4 bit per pixel.
- Tile palette indices: (offset 2048) 64 (8 * 8) bytes: Palette index the tile uses (1 byte per tile), allowed values are 0 to 12 (0Ch)
- Palettes: (offset 2112) 416 bytes (13 palettes, 16 colors, 16 bits per color): Color palettes used in the tile. Each entry is a 16-bit, little endian, palette color in pcengine format: 0000000GGGRRRBBB
- Extra info: (offset 2560):
 - Offset 0: Extra field version. Currently 0
 - Offset 1: Genre index.
 - Offset 2 and 3: Year, 16 bit, little endian
