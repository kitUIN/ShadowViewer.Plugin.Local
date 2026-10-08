Run `dotnet run --project tests/Reader.Tests/Reader.Tests.csproj` from the plugin repository.

The executable links the production page-turn, input, layout, frame and renderer sources. Its drawing adapters record page indexes, rectangles and strip transforms. Checks cover both reading directions, covers, boundaries, cancellation, short flicks, multitouch and animation convergence. A nonzero exit code means a regression. Native WinUI/GPU behavior must also be checked in a real reader window.
