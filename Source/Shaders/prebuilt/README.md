# Prebuilt shaders

The `.mgfx` files here are the simulator's HLSL effects compiled for a MonoGame shader profile.
They are committed rather than built because MonoGame's effect compiler needs Microsoft's HLSL
compiler, which only exists on Windows: keeping the results in the repository means an ordinary
build, and the distribution package, need no shader toolchain at all.

Regenerate them with `scripts/build-shaders.sh` (locally, through Wine) or by running the
`Shaders` workflow, which compiles them on Windows and commits the result. Anything that changes
a `.fx` file needs one of those.

`OpenGL/` holds the shader model 3 build the DesktopGL backend loads. The Windows build compiles
its own shaders during the build and does not read this directory.
