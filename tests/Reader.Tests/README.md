Run `dotnet run --project tests/Reader.Tests/Reader.Tests.csproj` from the plugin repository.

The executable links the production page-turn lifecycle, input, layout, frame and renderer sources. Drawing adapters record page indexes, rectangles, strip transforms, tints and gradient shadows. A synchronous control adapter exercises automatic navigation, gesture completion and animation takeover.

Checks cover both reading directions, covers, boundaries, neighboring layouts, different image sizes, excessive drag distance, shadow clipping/fading, automatic animation convergence, cancellation, short flicks, two-finger pan/pinch and pointer transitions. A nonzero exit code means a regression. Native WinUI bindings, dispatcher scheduling and GPU appearance must also be checked in a real reader window.
