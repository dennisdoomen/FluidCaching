## Fluid Caching

* LRU cache based on [this](http://www.codeproject.com/Articles/23396/A-High-Performance-Multi-Threaded-LRU-Cache) excellent article.
* The build status is [![Build status](https://ci.appveyor.com/api/projects/status/098rwks5ye15l00q?svg=true)](https://ci.appveyor.com/project/dennisdoomen/fluidcaching)

## Algorithm
* Items in the cache are represented by `Node`s. 
* The life-cycle of a node is managed by the `LifeSpanManager`.
* `Index`es only contain a `WeakReference` to the nodes and are there to support finding object through different keys.
* Whenever a new value is added to the cache, all indexes will receive a reference to the newly created `Node`.
* The `LifeSpanManager` assigns the nodes to `AgeBags`. Each `AgeBag` references the first node in a linked list of nodes. 
* The *current* bag maintains the most recently touched nodes. 
