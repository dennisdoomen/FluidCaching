* The build status is [![Build status](https://ci.appveyor.com/api/projects/status/098rwks5ye15l00q?svg=true)](https://ci.appveyor.com/project/dennisdoomen/fluidcaching)

## Fluid Caching

  _A least recently used cache that you can use without worrying_

## What's this?
At the beginning of this year, I reported on my endeavors to use RavenDB as a projection store for an event sourced system. One of the things I tried to speed up RavenDB's projection speed was to use the [Least Recently Used cache](http://www.codeproject.com/Articles/23396/A-High-Performance-Multi-Threaded-LRU-Cache) developed by [Brian Agnes](http://www.codeproject.com/script/Membership/View.aspx?mid=3034272) a couple of years ago. This cache gave us a nice speed increase, but the original author appears to be unreachable and seemingly abandoned the project. So I started looking for a way to distribute the code in a way that makes it painless to consume it in other projects. With this in mind, I decided to initiate a new open-source project Fluid Caching. As of August 10th, version 1.0.0 is officially available as a [source-only package on NuGet](https://www.nuget.org/packages/FluidCaching.Sources/1.0.0).

The cache in itself did meet our requirements quite well. It supported putting a limit on its capacity. It allows you to specify the minimum amount of time objects must be kept in the cache (even if that would exceed the capacity), as well as a maximum amount of time. It's completely safe to use in multi-threaded scenarios and is using an algorithm that keeps the performance under control, regardless of the number of items in the cache. It also supports multiple indexes based on different keys on top of the same cache, and is pretty flexible in how you get keys from objects. All credits for the initial implementation go to Brian, and I welcome you to read his original article from 2009 on some of the design choices.

## What does it look like

Considering a User class, in its simplest form, you can use the cache like this:

```csharp
var cache = new FluidCache<User>(1000, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10)), () => DateTime.Now);
```

This will create a cache with a maximum capacity of a 1000 items, but considering the minimum age of 1 minute, the actual capacity may exceed that for a short time. After an object hasn't been requested for as long as 10 minutes, it will be eligible for garbage collection. To retrieve objects from the cache, you need to create an index:

```csharp
IIndex<User, string> indexById = cache.AddIndex("byId", user => user.Id);
```

The lambda you pass in will be used to extract the key from the User object. Notice that you can have multiple indexes at the same time without causing duplication of the User instances. To retrieve an object from the cache (or create it on the spot), use the cache like this:

```csharp
User user = await indexById.GetItem("dennisd", id => Task.FromResult(new User { Id = id }));
```

## Why am I doing this
Good question. It's 2016, so some of the custom thread synchronization primitives are part of the .NET framework these days. Next to that, we all write asynchronous code and thus have a need for support for async/await. These days, being able to compile the code against any modern .NET version, even a Portable Class Library or .NET Core, isn't a luxury either. Other features I'm adding include thread-safe factory methods and some telemetry for tuning the cache to your needs. In terms of code distribution, my preferred method for a library like this would be a source-only NuGet package so that you don't have to bother the consumers of your packages with another dependency. Also, the code quality itself needs some [Object Calisthenics](http://www.continuousimprover.com/2015/10/9-simple-practices-for-writing-better.html) love as well as some sprinkles of my Coding Guidelines. And finally, you can't ship a library without at least a decent amount properly scoped unit tests and a [fully automated build pipeline](http://www.continuousimprover.com/2015/03/bringing-power-of-powershell-to-your.html).

## So how does it work
As I mentioned before, I highly recommend the original article if you want to understand some of the design decisions, but let me share some of the inner workings right now. The `FluidCache` class is the centerpiece of the solution. It's more of a factory for other objects than a cache per see. Instead, all items in the cache are owned by the `LifeSpanManager`. Its responsibility is to track when item has been 'touched' and use that to calculate when it is eligible for garbage collection while accounting for the provided minimum age.

Each item in the cache is represented by a `Node`, which on itself is part of a linked list of such nodes. The `LifeSpanManager` maintains an internal collection of 256 bags of which only one is open at the same time. Each bag has a limited lifetime and contains a pointer to the first node in the linked list. Whenever a new item is added to the cache, it is inserted into the head of the linked list. Similarly, if an existing item is requested, it is moved to the current bag as well. But whenever an item is added, requested or removed and a certain internal interval is passed, a (synchronous) clean-up operation is executed. This clean-up will iterate over all the bags, starting with the oldest one, and try to remove all nodes from that bag, provided that the bag's end-date matches the configured minimum and maximum age. When the clean-up has completed, the current bag is closed (it's end date and time is set) and the next one is marked as 'open'.

Through the `FluidCache` class, you can create multiple indexes and provide an optional factory method. However, indexes are nothing more than simple dictionaries that connect the key of the index with a weak reference to the right node. They will never prevent the garbage collector for cleaning your item. Only after the `LifeSpanManager` removes the reference from its internal collection of aging bags, the GC can do its job. Obviously, that factory will be invoked only once, and only if the object isn't in the cache already. The current API is async only, but as you can see, in those cases where usage of async/await is not really needed, it results in some ugly code. I'm considering to provide both a synchronous as well as an asynchronous API. Also, instead of passing the factory method in the call to `GetItem`, you can also pass a more global factory into the `AddIndex` method
