<h1 align="center">
  <br>
  Fluid Caching
  <br>
</h1>

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
[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/dennisdoomen/FluidCaching)](https://github.com/dennisdoomen/FluidCaching/graphs/commit-activity)
[![open issues](https://img.shields.io/github/issues/dennisdoomen/FluidCaching)](https://github.com/dennisdoomen/FluidCaching/issues)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](https://makeapullrequest.com)
![](https://img.shields.io/badge/release%20strategy-githubflow-orange.svg)
![Static Badge](https://img.shields.io/badge/4.7%2C_10.0-dummy?label=dotnet&color=%235027d5)

<a href="#about">About</a> •
<a href="#how-do-i-use-it">How To Use</a> •
<a href="#how-does-it-work">How It Works</a> •
<a href="#download">Download</a> •
<a href="#building">Building</a> •
<a href="#contributors">Contributors</a> •
<a href="#versioning">Versioning</a> •
<a href="#credits">Credits</a> •
<a href="#you-may-also-like">Related</a> •
<a href="#license">License</a>

</div>

## About

### What's this?

Fluid Caching is a multi-threaded, high-performance Least Recently Used (LRU) cache with async/await support. It
supports:

* A limit on the capacity of the cache.
* A minimum amount of time that an item stays in the cache, even if that exceeds the capacity for a short time.
* A maximum amount of time that an unused item stays in the cache.
* Safe use from multiple threads, with a factory that creates a missing item only once.
* Multiple indexes on top of the same cache, each with its own key and an optional key comparer.
* Statistics such as hits, misses and the current number of items, to help you tune the cache.

### What's so special about that?

It's a content-only NuGet package. The package adds the C# source code to your project, and all types are `internal`.
So you can use it in your own libraries and NuGet packages, without forcing your users to take a dependency on
another package. The code compiles with C# 10 or later, and is tested on .NET Framework 4.7 and .NET 10.

### Where does it come from?

A while ago, I [ran a test](https://www.continuousimprover.com/2016/01/evaluating-ravendb-as-embedded-database.html)
to use RavenDB as a projection store for an event sourced system. To speed up the projections, I used the
[Least Recently Used cache](http://www.codeproject.com/Articles/23396/A-High-Performance-Multi-Threaded-LRU-Cache)
written by [Brian Agnes](http://www.codeproject.com/script/Membership/View.aspx?mid=3034272) in 2009. It gave us a
nice speed increase, but the original author seems to have abandoned the project. So I started Fluid Caching to make
the code easy to use in other projects, and to add async/await support, thread-safe factory methods, statistics,
unit tests and a fully automated build pipeline. All credits for the initial implementation go to Brian.

### Who created this?
My name is Dennis Doomen and I'm a Microsoft MVP and Principal Consultant at [Aviva Solutions](https://avivasolutions.nl/) with 28 years of experience under my belt. As a software architect and/or lead developer, I specialize in designing full-stack enterprise solutions based on .NET as well as providing coaching on all aspects of designing, building, deploying and maintaining software systems. I'm the author of [Fluent Assertions](https://www.fluentassertions.com), a popular .NET assertion library, [Liquid Projections](https://www.liquidprojections.net), a set of libraries for building Event Sourcing projections and I've been maintaining [coding guidelines for C#](https://www.csharpcodingguidelines.com) since 2001.

Contact me through [Email](mailto:dennis.doomen@avivasolutions.nl), [BlueSky](https://bsky.app/profile/ddoomen.bsky.social), [Twitter/X](https://twitter.com/ddoomen) or [Mastadon](https://mastodon.social/@ddoomen)

## How do I use it?

Add the `FluidCaching` NuGet package to a project that uses C# 10 or later. Projects that target .NET Framework use
C# 7.3 by default, so add `<LangVersion>10</LangVersion>` (or higher) to your `.csproj` file.

Considering a `User` class, create the cache like this:

```csharp
using FluidCaching;

var cache = new FluidCache<User>(1000, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10), () => DateTime.UtcNow);
```

This creates a cache with a capacity of 1000 items. Because of the minimum age of 1 minute, the actual number of items
can be higher for a short time. After an item hasn't been requested for 10 minutes, it can be removed from the cache.
The last argument tells the cache how to get the current time, which makes it easy to use in unit tests.

To get items from the cache, you need an index:

```csharp
IIndex<string, User> indexById = cache.AddIndex("byId", user => user.Id);
```

The lambda gets the key from a `User` object. You can add more than one index without duplicating the `User`
instances. To get an item from the cache, or create it if it's not there yet, use:

```csharp
User? user = await indexById.GetItem("dennisd", id => Task.FromResult(new User { Id = id }));
```

The cache calls the factory only once, even if many threads ask for the same key at the same time. Instead of passing
the factory to every `GetItem` call, you can also pass it to `AddIndex`:

```csharp
IIndex<string, User> indexById = cache.AddIndex("byId", user => user.Id, id => LoadUserAsync(id));

User? user = await indexById.GetItem("dennisd");
```

Some other things you can do:

* `cache.Add(user)` adds an item directly, without using an index.
* `indexById.Remove("dennisd")` removes an item from the cache.
* `cache.Clear()` removes all items from the cache.
* `cache.AddIndex("byName", user => user.Name, keyEqualityComparer: StringComparer.OrdinalIgnoreCase)` creates an index
  that ignores the casing of the key.
* `cache.Get("byId", "dennisd")` gets an item through the name of an index.
* `cache.Statistics` gives you the number of hits and misses, the current number of items and more.
* The optional `validateCache` argument of the `FluidCache` constructor lets you clear the whole cache when an external
  condition changes.

## How does it work?

The `FluidCache` class is the center of the solution. It's more of a factory for other objects than a cache itself.
All items in the cache are owned by the `LifespanManager`. It tracks when an item was last 'touched', and uses that to
decide when the item can be removed, while respecting the configured minimum age.

Each item in the cache is wrapped in a `Node`, which is part of a linked list of nodes. The `LifespanManager` keeps an
internal collection of 265 bags. Only one bag is open at a time. Each bag has a limited lifetime and points to the
first node of its linked list. A new item is added to the head of the list of the open bag. When an existing item is
requested, it moves to the open bag as well. When an item is added, requested or removed and a certain interval has
passed, the cache runs a (synchronous) clean-up. This clean-up goes through all bags, starting with the oldest one, and
removes the nodes from bags that are older than the configured minimum and maximum age. Then it closes the current bag
and opens the next one.

The indexes are simple dictionaries that connect a key to a weak reference to the right node. So they never stop the
garbage collector from cleaning up your item. Only after the `LifespanManager` removes an item from its bags, can the
garbage collector do its job.

## Download

This library is available as [a NuGet package](https://www.nuget.org/packages/FluidCaching) on https://nuget.org. To install it, use the following command-line:

  `dotnet add package FluidCaching`

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

The build compiles the code, runs the tests, checks the public API, scans the NuGet dependencies with
[PackageGuard](https://github.com/dennisdoomen/packageguard), runs JetBrains InspectCode and creates the NuGet package.

## Contributors

Your contributions are always welcome! Please have a look at the [contribution guidelines](CONTRIBUTING.md) first.

<a href="https://github.com/dennisdoomen/FluidCaching/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=dennisdoomen/FluidCaching" alt="contrib.rocks image" />
</a>

(Made with [contrib.rocks](https://contrib.rocks))

## Versioning
This library uses [Semantic Versioning](https://semver.org/) to give meaning to the version numbers. For the versions available, see the [releases](https://github.com/dennisdoomen/FluidCaching/releases) on this repository.

## Credits
This library wouldn't have been possible without the following tools, packages and companies:

* [Brian Agnes](http://www.codeproject.com/script/Membership/View.aspx?mid=3034272) - The author of the original [high performance multi-threaded LRU cache](http://www.codeproject.com/Articles/23396/A-High-Performance-Multi-Threaded-LRU-Cache)
* [Nuke](https://nuke.build/) - Smart automation for DevOps teams and CI/CD pipelines
* [Rider](https://www.jetbrains.com/rider/) - The world's most loved .NET and game dev IDE
* [xUnit](https://xunit.net/) - Community-focused unit testing tool for .NET.
* [FluentAssertions](https://fluentassertions.com/) - Extension methods to fluently assert the outcome of .NET tests
* [Coverlet](https://github.com/coverlet-coverage/coverlet) - Cross platform code coverage for .NET
* [GitVersion](https://gitversion.net/) - From git log to SemVer in no time
* [ReportGenerator](https://reportgenerator.io/) - Powerful code coverage visualization
* [PackageGuard](https://github.com/dennisdoomen/packageguard) - Scans NuGet dependencies for license and security issues
* [StyleCopAnalyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) - StyleCop rules for .NET
* [Roslynator](https://github.com/dotnet/roslynator) - A set of code analysis tools for C#
* [CSharpGuidelinesAnalyzer](https://github.com/bkoelman/CSharpGuidelinesAnalyzer) - Roslyn analyzers to go with the [C# Coding Guidelines](https://csharpcodingguidelines.com/)
* [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer) - Another set of Roslyn analyzers

## Support the project
* [Github Sponsors](https://github.com/sponsors/dennisdoomen)
* [Tip Me](https://paypal.me/fluentassertions)
* [Buy me a Coffee](https://ko-fi.com/dennisdoomen)
* [Sponsor Me](https://www.patreon.com/bePatron?u=9250052&redirect_uri=http%3A%2F%2Ffluentassertions.com%2F&utm_medium=widget)

## You may also like

* [My Blog](https://www.dennisdoomen.com)
* [Reflectify](https://github.com/dennisdoomen/reflectify?tab=readme-ov-file#readme) - Reflection extensions without causing dependency pains
* [Mockly](https://github.com/dennisdoomen/mockly?tab=readme-ov-file#readme) - Fluent HTTP mocking for .NET like it should have been done
* [Pathy](https://github.com/dennisdoomen/pathy?tab=readme-ov-file#readme) - Fluently building and using file and directory paths without binary dependencies
* [FluentAssertions](https://github.com/fluentassertions/fluentassertions) - Extension methods to fluently assert the outcome of .NET tests
* [C# Coding Guidelines](https://csharpcodingguidelines.com/) - Forkable coding guidelines for all C# versions

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
