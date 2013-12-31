# dotnet-exec

[![dotnet-execute](https://img.shields.io/nuget/v/dotnet-execute)](https://www.nuget.org/packages/dotnet-execute/)

[![dotnet-execute Latest](https://img.shields.io/nuget/vpre/dotnet-execute)](https://www.nuget.org/packages/dotnet-execute/absoluteLatest)

[![default](https://github.com/WeihanLi/dotnet-exec/actions/workflows/dotnetcore.yml/badge.svg)](https://github.com/WeihanLi/dotnet-exec/actions/workflows/dotnetcore.yml)

[![Docker Pulls](https://img.shields.io/docker/pulls/weihanli/dotnet-exec)](https://hub.docker.com/r/weihanli/dotnet-exec)

## Intro

`dotnet-exec` is a command line tool for executing C# program without a project file, and you can have your custom entry point other than `Main` method

## Install/Update

Latest stable version:

```sh
dotnet tool update -g dotnet-execute
```

Latest preview version:

```sh
dotnet tool update -g dotnet-execute --prerelease
```

## Examples

### Get started

Execute local file:

``` sh
dotnet-exec HttpPathJsonSample.cs
```

Execute local file with custom entry point:

``` sh
dotnet-exec 'HttpPathJsonSample.cs' --entry MainTest
```

Execute remote file:

``` sh
dotnet-exec https://github.com/WeihanLi/SamplesInPractice/blob/master/net7Sample/Net7Sample/ArgumentExceptionSample.cs
```

Execute raw code:

``` sh
dotnet-exec 'code:Console.WriteLine(1+1);'

dotnet-exec 'Console.WriteLine(1+1);'
```

Execute raw script:

```sh
dotnet-exec 'script:1+1'
```

``` sh
dotnet-exec 'Guid.NewGuid()'
```

### References

Execute raw code with custom references:

NuGet package reference:

``` sh
dotnet-exec 'CsvHelper.GetCsvText(new[]{1,2,3}).Dump();' -r "nuget: WeihanLi.Npoi,2.3.0" -u "WeihanLi.Npoi"
```

Local dll reference:

``` sh
dotnet-exec 'CsvHelper.GetCsvText(new[]{1,2,3}).Dump();' -r "./out/WeihanLi.Npoi.dll" -u "WeihanLi.Npoi"
```

Local dll in a folder references:

``` sh
dotnet-exec 'CsvHelper.GetCsvText(new[]{1,2,3}).Dump();' -r "folder: ./out" -u "WeihanLi.Npoi"
```

Local project reference:

``` sh
dotnet-exec 'CsvHelper.GetCsvText(new[]{1,2,3}).Dump();' -r "project: ./WeihanLi.Npoi.csproj" -u "WeihanLi.Npoi"
```

Framework reference:

``` sh
dotnet-exec 'WebApplication.Create().Run();' --reference 'framework:web'
```

Web framework reference in one option:

``` sh
dotnet-exec 'WebApplication.Create().Run();' --web
```

### Usings

Execute raw code with custom usings:

``` sh
dotnet-exec 'code:WriteLine(1+1);' --using "static System.Console"

dotnet-exec 'WriteLine(1+1);' --using "static System.Console"
```

Execute script with custom reference:

``` sh
dotnet-exec 'CsvHelper.GetCsvText(new[]{1,2,3}).Dump()' -r "nuget:WeihanLi.Npoi,2.4.2" -u WeihanLi.Npoi
```

### More

Execute with additional dependencies

``` sh
dotnet-exec 'typeof(LocalType).FullName.Dump();' --ad FileLocalType2.cs
```

``` sh
dotnet-exec 'typeof(LocalType).FullName.Dump();' --addition FileLocalType2.cs
```

Execute with exacting references and usings from the project file

``` sh
dotnet-exec 'typeof(LocalType).FullName.Dump();' --project ./Sample.csproj
```

Execute file with preview features:

``` sh
dotnet-exec RawStringLiteral.cs --preview
```

### Config Profile

You can customize the config you used often into a config profile to reuse it for convenience.

List the profiles had configured:

``` sh
dotnet-exec profile ls
```

Configure a profile:

``` sh
dotnet-exec profile set web -r "nuget:WeihanLi.Web.Extensions" -u 'WeihanLi.Web.Extensions' --web --wide false
```

Get the profile details:

``` sh
dotnet-exec profile get web
```

Remove the profile not needed:

``` sh
dotnet-exec profile rm web
```

Executing with specific profile config:

``` sh
dotnet-exec 'WebApplication.Create().Chain(_=>_.MapRuntimeInfo()).Run();' --profile web --using 'WeihanLi.Extensions'
```

![](C:\Users\Weiha\AppData\Roaming\Typora\typora-user-images\image-20221203002201989.png)

### Docker support

Execute with docker

``` sh
docker run --rm weihanli/dotnet-exec:latest dotnet-exec "1+1"
```

``` sh
docker run --rm weihanli/dotnet-exec:latest dotnet-exec "Guid.NewGuid()"
```

``` sh
docker run --rm --pull=always weihanli/dotnet-exec:latest dotnet-exec "ApplicationHelper.RuntimeInfo"
```

for full image tag list, see <https://hub.docker.com/r/weihanli/dotnet-exec/tags>

## Acknowledgements

- [Roslyn](https://github.com/dotnet/roslyn)
- [NuGet.Clients](https://github.com/NuGet/NuGet.Client)
- [System.CommandLine](https://github.com/dotnet/command-line-api)
- [Thanks JetBrains for the open source Rider license](https://jb.gg/OpenSource?from=dotnet-exec)
- Thanks the contributors and users for this project
