# Contributing to Fluid Caching

No open-source project is going to be successful without contributions. We are grateful for all contributions,
no matter their size. To make it easier for us to accept your change, please follow these steps.

## Finding existing issues

Before you file a new issue, please search the [issues](https://github.com/dennisdoomen/FluidCaching/issues) to
check if it already exists. If you find an existing issue, please add your feedback to the discussion. Instead of
posting "me too", upvote the issue with 👍.

## Pull requests

* Target the `master` branch with your [pull request](https://help.github.com/articles/using-pull-requests).
* The code complies with the [C# Coding Guidelines](https://csharpcodingguidelines.com/). The analyzers in this
  repository check most of these rules during the build.
* The files in `FluidCaching/` are also shipped as source code in the `FluidCaching.Sources` package. Keep them
  compatible with C# 10.
* The changes are covered by new or existing unit tests that follow the Arrange-Act-Assert syntax, like the ones in
  [FluidCacheSpecs.cs](FluidCaching.Specs/FluidCacheSpecs.cs).
* If you change the public API on purpose, update the approved API files. Run the tests in
  `FluidCaching.ApiVerificationTests` and then `AcceptApiChanges.ps1` or `AcceptApiChanges.sh`.
* Run `./build.ps1` (Windows) or `./build.sh` (Linux and macOS) before you submit your pull request.
