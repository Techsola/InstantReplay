# Techsola.InstantReplay [![MyGet badge](https://img.shields.io/myget/techsola/vpre/Techsola.InstantReplay.svg?label=myget)](https://www.myget.org/feed/techsola/package/nuget/Techsola.InstantReplay "MyGet (prereleases)") [![Build status badge](https://github.com/Techsola/InstantReplay/workflows/CI/badge.svg)](https://github.com/Techsola/InstantReplay/actions?query=workflow%3ACI "Build status")

[🔬 Currently experimental. More documentation will be added later.]

Produces an animated GIF on demand of the last ten seconds of a Windows desktop app’s user interface. This can be useful to include in error reports or to help understand how an unusual situation came about.

### Goals

- **Low resource usage** while recording

- **Privacy**: never captures content from other apps

- **Ease of consumption**: the right thing happens if you double-click a .gif file on Windows or if you open a .gif attachment in a web browser

- **Fast generation** when a GIF is requested

### Non-goals

- Optimizing GIF **file size** (unless it also speeds up GIF creation)

- Pixel-perfect recording of **non-client** areas of the app windows (but improvements will be considered)

- **Async I/O**: There’s enough CPU-blocking work in encoding a GIF that it’s preferable to create it on a background thread. Once that is happening, it’s okay for a desktop app to continue blocking the same background thread on the occasional I/O delay because this call is rare. It’s not as though a desktop app would be saving thousands of GIFs concurrently and run into throughput issues.

## Is this for me?

While other integrations could happen in the future, right now this library only works with Windows desktop applications that have access to native Win32 APIs.

| App model     | Supported    |
|---------------|--------------|
| Windows Forms | ✔            |
| WPF           | ✔ (untested) |
| UWP           | ❌           |

To continue fleshing out the list: support currently depends on whether the app is able to [invoke](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke) native Windows functions such as [`BitBlt`](https://docs.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt) and [`EnumWindows`](https://docs.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows).

## How to use

[🔬 Currently experimental. Examples and more documentation will be added later.]

### Set up

Until this library is released to nuget.org, add this package source to a `nuget.config` file at the root of your project’s source repository:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="Techsola prerelease" value="https://www.myget.org/F/techsola/api/v3/index.json" />
  </packageSources>
</configuration>
```

If your project was open in Visual Studio when you edited the `nuget.config` file, close the solution and reopen it. Then use the NuGet package manager to install the latest available version of the package `Techsola.InstantReplay` into your app’s startup project. Make sure that the ‘Include prerelease’ box is checked and the selected package source is ‘All.’

Now that the package is added to your project, add a call to `InstantReplayCamera.Start();` before your app’s first window is shown. (The namespace to include is `Techsola.InstantReplay`.) This call only needs to be made once in the lifetime of the process. Subsequent calls are ignored.

For a Windows Forms app, the ideal place for this call is in `Program.Main` before `Application.Run` is called.

### Profit

Whenever you want a GIF of the last ten seconds of the app’s user interface, call `InstantReplayCamera.SaveGif();` to obtain a byte array containing an animated GIF. (Or `null`, if there are currently no frames to save.) A good place to do this is in your app’s top-level unhandled exception reporter so that you get a recording of the UI along with the exception information.

TODO: Recommend the use of `Task.Run` when on a UI thread due to the CPU-blocking work it takes to encode a GIF.

## Debugging into Techsola.InstantReplay source

Stepping into Techsola.InstantReplay source code, pausing the debugger while execution is inside Techsola.InstantReplay code and seeing the source, and setting breakpoints in Techsola.InstantReplay all require loading symbols for Techsola.InstantReplay. To do this in Visual Studio:

1. Go to Debug > Options, and uncheck ‘Enable Just My Code.’ (It’s a good idea to reenable this as soon as you’re finished with the task that requires debugging into a specific external library.)  
   ℹ *Before* doing this, because Visual Studio can become unresponsive when attempting to load symbols for absolutely everything, I recommend going to Debugging > Symbols within the Options window and selecting ‘Load only specified modules.’

2. If you are using a prerelease version of Techsola.InstantReplay package, go to Debugging > Symbols within the Options window and add this as a new symbol location: `https://www.myget.org/F/techsola/api/v2/symbolpackage/`  
   If you are using a version that was released to nuget.org, enable the built-in ‘NuGet.org Symbol Server’ symbol location.

3. If ‘Load only specified modules’ is selected in Options > Debugging > Symbols, you will have to explicitly tell Visual Studio to load symbols for Techsola.InstantReplay. One way to do this while debugging is to go to Debug > Windows > Modules and right-click on Techsola.InstantReplay. Select ‘Load Symbols’ if you only want to do it for the current debugging session. Select ‘Always Load Automatically’ if you want to load symbols now and also add the file name to a list so that Visual Studio loads Techsola.InstantReplay symbols in all future debug sessions when Just My Code is disabled.
