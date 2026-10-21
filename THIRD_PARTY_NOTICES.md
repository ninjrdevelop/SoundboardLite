# Third-party notices

Soundboard Lite is built on these open-source packages. This file lists the ones that aren't under a
plain MIT/Apache-2.0 license without conditions, plus a full list for reference.

## Requires attention

### SoundTouch.Net (2.3.2) — LGPL-2.1-or-later

Used for pitch-shifting (`SoundboardLite.Audio/Pitch/SoundTouchPitchShifter.cs`). This is
[owoudenberg/soundtouch.net](https://github.com/owoudenberg/soundtouch.net), a complete C# rewrite of
Olli Parviainen's original C++ SoundTouch library — **it has no native/unmanaged component**; the
whole thing is a single managed .NET assembly.

Why this is fine under LGPL for a self-contained single-file publish: the library ships as its own
separate assembly (`SoundTouch.Net.dll`), referenced by, not statically merged into, Soundboard
Lite's own compiled code — even once bundled into the single-file `.exe` via
`PublishSingleFile=true`, it remains a discrete, individually-extractable/replaceable component (the
.NET single-file host extracts it to a temp directory and loads it as its own assembly at runtime;
it isn't IL-merged into Soundboard Lite's own IL the way e.g. ILRepack would do it). LGPL's
"relink with a modified version of the library" requirement is satisfiable because of that separation.
Source: https://github.com/owoudenberg/soundtouch.net

## Full list

| Package | License | Used for |
|---|---|---|
| [NAudio.Core](https://github.com/naudio/NAudio) / NAudio.Wasapi | MIT | Audio decode/mix/output |
| [SoundTouch.Net](https://github.com/owoudenberg/soundtouch.net) | LGPL-2.1-or-later | Pitch shifting — see above |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | MIT | MVVM (source-generated) |
| [WPF-UI](https://github.com/lepoco/wpfui) | MIT | Fluent UI shell |
| [Microsoft.Data.Sqlite](https://github.com/dotnet/efcore) | MIT | SQLite driver |
| [Dapper](https://github.com/DapperLib/Dapper) | Apache-2.0 | Micro-ORM over SQLite |
| [Microsoft.Extensions.Hosting](https://github.com/dotnet/runtime) | MIT | Generic host / DI |
| [Serilog](https://github.com/serilog/serilog) / Serilog.Extensions.Hosting / Serilog.Sinks.File | Apache-2.0 | Local file logging |
| [ASP.NET Core](https://github.com/dotnet/aspnetcore) (`Microsoft.AspNetCore.App` shared framework) | MIT | Embedded local API (Kestrel, minimal APIs, WebSockets) |
| [@elgato/streamdeck](https://github.com/elgatosf/streamdeck) / @elgato/utils | MIT | Stream Deck plugin SDK |
| [xunit](https://github.com/xunit/xunit) / xunit.runner.visualstudio | MIT / Apache-2.0 | Test framework |
| [NSubstitute](https://github.com/nsubstitute/NSubstitute) | BSD-3-Clause | Test doubles |
| [Shouldly](https://github.com/shouldly/shouldly) | BSD-3-Clause | Test assertions |
| [coverlet.collector](https://github.com/coverlet-coverage/coverlet) | MIT | Test coverage collection |

No packages here require source disclosure of Soundboard Lite's own code, or prevent commercial/
proprietary use. If you redistribute a build, keep this file alongside it.
