# TTE Disk Builder

Simple C# console app which builds a bootable floppy disk image based on a json file

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


