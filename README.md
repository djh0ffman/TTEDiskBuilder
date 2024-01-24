# TTE Disk Builder

Simple C# console app which builds a bootable floppy disk image based on a json file

Pass the app the folder where the disk is to be built from.

The folder must contain a ```disk.json``` and a ```bootblock``` binary file.

The app will write a ```final.adf``` in the folder.

The following packer executables need to be available either in the folder or via a path.

* Shrinkler -> https://github.com/askeksa/Shrinkler
* ZX0 (Salvador) -> https://github.com/emmanuel-marty/salvador
* Zopfli (Deflate) -> ????

Example json

```
[
  {
    "Filename": "tinytro.bin",
    "FileID": "BOOT",
    "PackingMethod": 2,
    "Cacheable": false,
  },
  {
    "Filename": "LOAD",
    "FileID": "0000",
    "PackingMethod": 2,
    "Cacheable": false,
  }
]
```

|Field|Description|
|:---|:---|
|Filename|Physical file name in folder|
|FileID|Four byte alpha-numeric file identifier|
|Packing Method|See below|
|Cacheable|Sets file to be cacheable by TTE loader|

Packing Method

        0 = None
        1 = Shrinkler
        2 = ZX0
        3 = LZ
        4 = Deflate
        5 = Trim

# Resulting ADF file

The resulting ADF file will contain a table of contents from location $400. All files are then applied straight after the table of contents.

## Table of contents

Each entry is 16 bytes long.

|:---|:---|
|FileID|LONG - ASCII FileID|
|Disk Position|LONG - The starting position in bytes on the disk|
|Packed File Size|LONG - bits 31-28 = Packing Type / bits 27-24 = Cache flag / bits 23-0 = Data length|
|Unpacked File Size|LONG - Size of file after unpacking|



