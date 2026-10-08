Run `dotnet run --project tests/Reader.Tests/Reader.Tests.csproj` from the plugin repository.

The executable links the production page-turn lifecycle, input, layout, frame and renderer sources. Drawing adapters record page indexes, rectangles, strip transforms, tints and gradient shadows. A synchronous control adapter exercises automatic navigation, gesture completion and animation takeover.

Checks cover both reading directions, covers, boundaries, neighboring layouts, different image sizes, excessive drag distance, shadow clipping/fading, automatic animation convergence, cancellation, short flicks, two-finger pan/pinch and pointer transitions.

Image-loading checks link the production disk cache, window controller, load service and background pipeline. A fake HTTP handler and real temporary files verify streaming, URL deduplication, corrupted-cache recovery, interrupted/canceled body cleanup and retries. Strategy/decoder adapters verify Hook compatibility, late dimensions, byte/bitmap ownership, canceled result rejection, mutable page indexes, queue pressure and pipeline restart. The cache validator uses a synthetic payload. The full Windows build verifies integration; native codec behavior is not simulated here.

A nonzero exit code means a regression. Native WinUI bindings, dispatcher scheduling, image codec behavior and GPU appearance must also be checked in a real reader window.
