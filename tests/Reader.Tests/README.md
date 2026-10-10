Run `dotnet run --project tests/Reader.Tests/Reader.Tests.csproj` from the plugin repository.

The executable links the production page-turn lifecycle, input, layout, frame and renderer sources. Drawing adapters record page indexes, rectangles, strip transforms, tints and gradient shadows. A synchronous control adapter exercises automatic navigation, gesture completion and animation takeover.

Checks cover both reading directions, covers, boundaries, neighboring layouts, different image sizes, excessive drag distance, shadow clipping/fading, automatic animation convergence, cancellation, short flicks, two-finger pan/pinch and pointer transitions.

Diagonal page-turn checks cover all four drag directions in both spread modes, slanted texture and shadow geometry, paper coverage and shared strip edges, release/takeover continuity, canceled gestures, and flat completion. Drawing adapters record polygon fills and image-brush transforms; they do not validate native rasterization or GPU performance.

Chapter-navigation checks exercise the production command, wheel and pointer handlers in vertical, single-page and spread modes. They verify that arriving at a boundary is silent, continuing beyond it requests the matching chapter, the final spread is treated as a unit, tall vertical pages must reach their content edge, and canceled/zoomed/multi-pointer gestures remain silent. WinUI input adapters supply event data; popup bindings and native event routing are not simulated.

Concurrent frame handoff checks link the production drawing and image-window entry points. They pause the completion callback while a worker starts drawing or updating the cache, then verify that forward/backward automatic and gesture turns draw only the target spread and retain its bitmap instances.

Image-loading checks link the production disk cache, window controller, load service and background pipeline. A fake HTTP handler and real temporary files verify streaming, URL deduplication, corrupted-cache recovery, interrupted/canceled body cleanup and retries. Strategy/decoder adapters verify Hook compatibility, late dimensions, byte/bitmap ownership, canceled result rejection, mutable page indexes, queue pressure and pipeline restart. The cache validator uses a synthetic payload. The full Windows build verifies integration; native codec behavior is not simulated here.

A nonzero exit code means a regression. Native WinUI bindings, dispatcher scheduling, image codec behavior and GPU appearance must also be checked in a real reader window.
