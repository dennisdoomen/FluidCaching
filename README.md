<h1 align="center">Fluid Caching</h1>

<h4 align="center">A least recently used cache that you can use without worrying</h4>

<div align="center">

[![](https://img.shields.io/github/actions/workflow/status/dennisdoomen/FluidCaching/build.yml?branch=master)](https://github.com/dennisdoomen/FluidCaching/actions?query=branch%3amaster)
[![Coveralls branch](https://img.shields.io/coverallsCoverage/github/dennisdoomen/FluidCaching?branch=master)](https://coveralls.io/github/dennisdoomen/FluidCaching?branch=master)
[![](https://img.shields.io/github/release/dennisdoomen/FluidCaching.svg?label=latest%20release&color=007edf)](https://github.com/dennisdoomen/FluidCaching/releases/latest)
[![](https://img.shields.io/nuget/dt/FluidCaching.svg?label=downloads&color=007edf&logo=nuget)](https://www.nuget.org/packages/FluidCaching)
[![](https://img.shields.io/librariesio/dependents/nuget/FluidCaching.svg?label=dependent%20libraries)](https://libraries.io/nuget/FluidCaching)
![GitHub Repo stars](https://img.shields.io/github/stars/dennisdoomen/FluidCaching?style=flat)
[![GitHub contributors](https://img.shields.io/github/contributors/dennisdoomen/FluidCaching)](https://github.com/dennisdoomen/FluidCaching/graphs/contributors)
[![GitHub last commit](https://img.shields.io/github/last-commit/dennisdoomen/FluidCaching)](https://github.com/dennisdoomen/FluidCaching)
[![open issues](https://img.shields.io/github/issues/dennisdoomen/FluidCaching)](https://github.com/dennisdoomen/FluidCaching/issues)
![Static Badge](https://img.shields.io/badge/4.7%2C_10.0-dummy?label=dotnet&color=%235027d5)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](https://makeapullrequest.com)

</div>

## About

### What's this?

A while ago, I [ran a test](https://www.continuousimprover.com/2016/01/evaluating-ravendb-as-embedded-database.html) to use RavenDB as a projection store for an event sourced system. One of the things I tried to speed up RavenDB's projection speed was to use the [Least Recently Used cache](http://www.codeproject.com/Articles/23396/A-High-Performance-Multi-Threaded-LRU-Cache) developed by [Brian Agnes](http://www.codeproject.com/script/Membership/View.aspx?mid=3034272) a couple of years ago. This cache gave us a nice speed increase, but the original author appears to be unreachable and seemingly abandoned the project. So I started looking for a way to distribute the code in a way that makes it painless to consume it in other projects. With this in mind, I decided to initiate a new open-source project Fluid Caching.

The cache in itself did meet our requirements quite well. It supported putting a limit on its capacity. It allows you to specify the minimum amount of time objects must be kept in the cache (even if that would exceed the capacity), as well as a maximum amount of time. It's completely safe to use in multi-threaded scenarios and is using an algorithm that keeps the performance under control, regardless of the number of items in the cache. It also supports multiple indexes based on different keys on top of the same cache, and is pretty flexible in how you get keys from objects. All credits for the initial implementation go to Brian, and I welcome you to read his original article from 2009 on some of the design choices.

The library supports:
* **.NET Framework 4.7** and higher
* **.NET 10.0** and higher

### Another caching library!? Why?

Good question. Some of the custom thread synchronization primitives are part of the .NET framework these days. Next to that, we all write asynchronous code and thus have a need for support for async/await. Being able to compile the code against any modern .NET version isn't a luxury either. Other features include thread-safe factory methods and some telemetry for tuning the cache to your needs. In terms of code distribution, my preferred method for a library like this is a source-only NuGet package, so that you don't have to bother the consumers of your packages with another dependency. Also, the code quality itself needs some [Object Calisthenics](https://www.continuousimprover.com/2015/10/9-simple-practices-for-writing-better.html) love as well as some sprinkles of my [Coding Guidelines](https://csharpcodingguidelines.com). And finally, you can't ship a library without at least a decent amount of properly scoped unit tests and a fully automated build pipeline.

## How do I use it?

Considering a `User` class, in its simplest form, you can use the cache like this:

```csharp
var cache = new FluidCache<User>(1000, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), () => DateTime.Now);
```

This will create a cache with a maximum capacity of a 1000 items, but considering the minimum age of 1 minute, the actual capacity may exceed that for a short time. After an object hasn't been requested for as long as 10 minutes, it will be eligible for garbage collection. To retrieve objects from the cache, you need to create an index:

```csharp
IIndex<string, User> indexById = cache.AddIndex("byId", user => user.Id);
```

The lambda you pass in will be used to extract the key from the `User` object. Notice that you can have multiple indexes at the same time without causing duplication of the `User` instances. To retrieve an object from the cache (or create it on the spot), use the cache like this:

```csharp
User? user = await indexById.GetItem("dennisd", id => Task.FromResult(new User { Id = id }));
```

## Where do I get it?

It's available in two different forms.
* A NuGet package called [FluidCaching](https://www.nuget.org/packages/FluidCaching/) that adds a binary dependency for .NET Framework 4.7 or .NET 10.0 and later.
* A NuGet package called [FluidCaching.Sources](https://www.nuget.org/packages/FluidCaching.Sources/) that just adds the source code to your project without any binary dependencies. All types are `internal`, so they don't become part of your public API. Your project must use C# 10 or later.

## So how does it work?

As I mentioned before, I highly recommend the original article if you want to understand some of the design decisions, but let me share some of the inner workings right now. The `FluidCache` class is the centerpiece of the solution. It's more of a factory for other objects than a cache per se. Instead, all items in the cache are owned by the `LifespanManager`. Its responsibility is to track when an item has been 'touched' and use that to calculate when it is eligible for garbage collection while accounting for the provided minimum age.

Each item in the cache is represented by a `Node`, which on itself is part of a linked list of such nodes. The `LifespanManager` maintains an internal collection of 265 bags of which only one is open at the same time. Each bag has a limited lifetime and contains a pointer to the first node in the linked list. Whenever a new item is added to the cache, it is inserted into the head of the linked list. Similarly, if an existing item is requested, it is moved to the current bag as well. But whenever an item is added, requested or removed and a certain internal interval is passed, a (synchronous) clean-up operation is executed. This clean-up will iterate over all the bags, starting with the oldest one, and try to remove all nodes from that bag, provided that the bag's end-date matches the configured minimum and maximum age. When the clean-up has completed, the current bag is closed (its end date and time is set) and the next one is marked as 'open'.

Through the `FluidCache` class, you can create multiple indexes and provide an optional factory method. However, indexes are nothing more than simple dictionaries that connect the key of the index with a weak reference to the right node. They will never prevent the garbage collector from cleaning your item. Only after the `LifespanManager` removes the reference from its internal collection of aging bags, the GC can do its job. Obviously, that factory will be invoked only once, and only if the object isn't in the cache already. Instead of passing the factory method in the call to `GetItem`, you can also pass a more global factory into the `AddIndex` method.

## Building

To build this repository locally, you need:
* The [.NET SDKs](https://dotnet.microsoft.com/en-us/download/visual-studio-sdks) for .NET 4.7 and 10.0.
* Visual Studio, JetBrains Rider or Visual Studio Code with the C# DevKit

Build using PowerShell:

```bash
./build.ps1
```

Or on Linux and macOS:

```bash
./build.sh
```

The build compiles the code, runs the tests, checks the public API, scans the NuGet dependencies with [PackageGuard](https://github.com/dennisdoomen/packageguard), runs the JetBrains InspectCode analysis and creates the NuGet packages.

## Contributing

Your contributions are always welcome! Please have a look at the [contribution guidelines](CONTRIBUTING.md) first.

Previous contributors include:

<a href="https://github.com/dennisdoomen/FluidCaching/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=dennisdoomen/FluidCaching" alt="contrib.rocks image" />
</a>

(Made with [contrib.rocks](https://contrib.rocks))

## Versioning

This library uses [Semantic Versioning](https://semver.org/) to give meaning to the version numbers. For the versions available, see the [releases](https://github.com/dennisdoomen/FluidCaching/releases) on this repository.

## Credits

All credits for the initial implementation go to [Brian Agnes](http://www.codeproject.com/script/Membership/View.aspx?mid=3034272). This library also wouldn't have been possible without the following tools, packages and companies:

* [FluentAssertions](https://fluentassertions.com/) - Fluent API for asserting the results of unit tests by [Dennis Doomen](https://github.com/dennisdoomen)
* [Nuke](https://nuke.build/) - Smart automation for DevOps teams and CI/CD pipelines
* [xUnit](https://xunit.net/) - Community-focused unit testing tool for .NET by [Brad Wilson](https://github.com/bradwilson)
* [Coverlet](https://github.com/coverlet-coverage/coverlet) - Cross platform code coverage for .NET by [Toni Solarin-Sodara](https://github.com/tonerdo)
* [GitVersion](https://gitversion.net/) - From git log to SemVer in no time
* [ReportGenerator](https://reportgenerator.io/) - Converts coverage reports by [Daniel Palme](https://github.com/danielpalme)
* [PackageGuard](https://github.com/dennisdoomen/packageguard) - Scans NuGet dependencies for license and security issues by [Dennis Doomen](https://github.com/dennisdoomen)
* [StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) - StyleCop rules for .NET
* [Roslynator](https://github.com/dotnet/roslynator) - A set of code analysis tools for C# by [Josef Pihrt](https://github.com/josefpihrt)
* [CSharpCodingGuidelines](https://github.com/bkoelman/CSharpGuidelinesAnalyzer) - Roslyn analyzers by [Bart Koelman](https://github.com/bkoelman) to go with the [C# Coding Guidelines](https://csharpcodingguidelines.com/)
* [Meziantou](https://github.com/meziantou/Meziantou.Framework) - Another set of awesome Roslyn analyzers by [Gérald Barré](https://github.com/meziantou)

## License

This project is licensed under the [MIT License](LICENSE).
