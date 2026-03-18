# Native Version

Windows native implementation lives in:

```text
native/FloatingClockWidget.Native
```

## Build

```bash
dotnet build native/FloatingClockWidget.Native/FloatingClockWidget.Native.csproj -c Release
```

## Publish single exe

```bash
dotnet publish native/FloatingClockWidget.Native/FloatingClockWidget.Native.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o native/dist/win-x64
```

Output:

```text
native/dist/win-x64/FloatingClockWidget.Native.exe
```
