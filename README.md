
# WpfAutoUpdater (.NET 9.0 WPF)

A minimal WPF app that checks GitHub Releases and can download/install updates.

 ![Image Alt](https://github.com/DevBaschar/WpfAutoUpdater/blob/master/Screenshot%202025-10-01%20151505.png?raw=true)

## Build
```bash
dotnet restore
dotnet build
```

## Run
```bash
dotnet run --project WpfAutoUpdater/WpfAutoUpdater.csproj
```

## Publish & Release via GitHub Actions
Tag your commit with a semantic version, e.g. `v1.2.3`.
The workflow builds and uploads a ZIP asset to the GitHub Release.
