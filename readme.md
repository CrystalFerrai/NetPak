# NetPak

NetPak is a .NET 6 library for reading and creating Unreal Engine pak files.

## Releases

There are no releases at this time. Once the library is more mature, releases might start happening. Until then, you can include the project in your own solutions and build from source.

## Building

The library is maintained as a Visual Studio 2022 .NET 6 project. You can either open the incldued solution and build it directly or include the NetPak project in your own solution.

The included solution contains the NetPak library and a console app used to test the library. This is primarily for development of the library.

To use the library in your own solution, the recommended approach is to add it to your own git repo as a submodule like this:

```
git submodule add https://github.com/CrystalFerrai/NetPak.git NetPak
```

If your project is not using git, then you can clone the repo and use it that way.

```
git clone --recursive https://github.com/CrystalFerrai/NetPak.git
```

Either way, once you have the library downloaded, simply add a reference to the NetPak project to your own Visual Studio solution or MSBuild project and depend on it from the project that needs to use it. You can ignore the NetPak.Test project as well as other libraries it depends on such as BinaryCompare. These are only used for development of the library.

## Usage

The main API you will interect with in the library is the PakFile class. There are two ways to obtain an instance of this class: open an existing file or create a new file.

### Opening an existing pak file

To open an existing pak file, call the `PakFile.Mount` static function, passing in the path to the pak file you want to open.

```
using PakFile file = PakFile.Mount(path);
```

This will open the file and load the index. It will not load the data for the entries in the file. Entry data will be loaded as entries are requested.

### Creating a new pak file

To create a new pakfile, call the `PakFile.Create` static function.

```
using PakFile file = PakFile.Create(fileName, mountPoint);
```

The `fileName` param is not actually used to create any file. It is only used as a seed for hashing entry paths. It can technically be any string you want and will work fine. Using the pak file name just follows the convention that UnrealPak uses.

The `mountPoint` param is an important one. This determines the root of the path where the entries in the pak file end up being loaded by the engine. A mount point usually looks something like this:

```
../../../MyGame/
```

It is recommended that you check the mount point for existing pak files for the game you are working with and follow that.

### Adding and removing entries

To add a new entry to a pak file, call `AddEntry`, passing in the path of the entry and its serialized binary data.

```
file.AddEntry(path, data);
```

To remove an entry, call `RemoveEntry`, passing in the entry path.

```
file.RemoveEntry(path);
```

### Reading and writing entry data

For entries contained within the pak file, you can read and/or overwite their data.

To read a single entry, call `ReadEntryData`, passing in the path and variable to receive the data.

```
if (file.ReadEntryData(path, out var data))
{
    // Do something with the data
}
```

To read all of the entries associated with an asset, you can call `GetAssetData`. This is simply a convenience function. You still need to update the different entries independently if you are modifying the data.

```
if (file.GetAssetData(path, out var data, out var exportPath, out var exportData, out var bulkPath, out var bulkData))
{
    // Do something with the data. If exports or bulk data don't exist, those outputs will be null.
}
```

To overwrite the data for an entry, call `WriteEntryData`, passing in the entry path and the data to write.

```
file.WriteEntryData(path, data);
```

### Saving a pak file

You can save a pak file to either a file or an arbitrary stream using one of the save methods.

```
file.Save(path);
// or
file.SaveTo(stream);
```

Note that nothing you do with a loaded pak file will have any effect on the file on disk until you save it.

### Disposing the pak file instance

Note that the PakFile class implements `IDisposable`. You must either wrap it in a `using` or dall `Dispose` on it to clean it up when you are done with it. Otherwise, it may keep the file on disk locked.