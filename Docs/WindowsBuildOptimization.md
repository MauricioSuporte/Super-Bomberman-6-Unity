# Windows build size workflow

## Current baseline and changes

The supplied v0.5.0 Windows player is 980.37 MiB (353 files). `.resS` files account
for 502.38 MiB and `.resource` files for 349.99 MiB. These are container totals,
not a confirmed texture/audio attribution. Original WAV/MP3 sizes are not player sizes.
The baseline ZIP created with the measurement script is 401.57 MiB. This is a
download-size baseline, not savings from the new audio settings or LZ4HC.

The source audit found 1,335 PNGs under Resources. Their dimensions imply about
468.78 MiB at RGBA32 without mipmaps (an estimate, not an imported-size measurement).
Generated 1536x512 skin sheets are among the largest, at 3 MiB each under that
assumption. This supports investigating textures alongside audio in the Build Report.

All 247 existing audio importers now have a Standalone Vorbis quality 80 override,
as a **candidate awaiting listening validation**. Default and Android settings,
source audio, channel count, sample rate, load type and preload settings are preserved.
Standalone also applies to future macOS/Linux builds. No audio or graphics assets
were removed. Existing user edits to fonts and materials were left untouched.

The Editor build-window handler adds LZ4HC and DetailedBuildReport only for Windows.
It runs when the user requests Build/Build and Run; it does not start a build on
reload. It enforces the effective options rather than editing generated Library
profiles. Direct BuildPipeline callers must explicitly supply these two options.
If another tool registers a build-window handler, integrate these flags there;
Unity supports only one registered handler.

After a Windows build, reports are exported outside the player to
`output/WindowsBuildReports/<timestamp>/`: assets sorted by packed bytes, totals
by Unity type, actual output files and build summary. The Tools > Windows Build >
Export Latest Build Report command also exports the existing last Windows report
without building. Packed attribution is not final LZ4HC disk size.

## Audio comparison

Use a Windows active target. Select individual AudioClips in the Project window,
then Tools > Windows Build > Selected Audio. Compare 100 (reference), 80, 70 and,
for music only, 60. These commands reimport selected audio; they do not build.
The lowest candidate that passes listening is retained per clip. Restore 100 for
any clip with audible artifacts. Each invocation copies the unchanged Default
sample settings before changing only desktop codec, load type and quality.

Compare at equal playback volume with headphones: complete tracks, high frequencies,
voices, explosion attacks and tails. Keep sample rate and stereo intact. Do not
mark quality 80 as approved just because it produces a smaller file.

Validate TitleScreen and stage intros/loops, pause/resume, scene transitions,
Normal Game, Boss Rush and Battle Mode with up to six players. Verify simultaneous
effects, no added stalls, and the Editor music loudness calibration. Keep
Decompress On Load so calibration via GetData remains supported.

## Measure and package

Only run Unity compilation/builds when explicitly requested. Build into a fresh
folder, preserving the baseline. Do not mix older build output into the candidate.
From the repository root, measure an existing build with:

```powershell
./Tools/Measure-WindowsBuild.ps1 -BuildPath 'C:\path\WindowsPlayer' -OutputDirectory './output/WindowsMeasurements'
```

Add `-CreateZip` to create a ZIP (Optimal compression) alongside its measurement,
outside the player. For a comparison add `-BaselineJson 'C:\path\measurement.json'`.
Each run has a unique output folder and does not overwrite a previous ZIP or report.
Package the baseline once with the same command if ZIP savings are needed. No
background build or ZIP creation occurs merely by opening Unity.

Record installed and ZIP savings separately. First compare baseline against quality
80 plus LZ4HC; then compare lower approved qualities with the same build options.
Savings remain unmeasured until a candidate player is built.

## Resources audit

Use assets.csv to rank actual imported assets, especially Resources textures and
generated skin sheets. PNG source size is not decoded texture size. Check scene,
prefab and Resources.Load/LoadAll dependencies, including generated names, before
excluding any asset. Lack of a direct GUID reference does not prove an asset unused.
Keep all content, pixel resolution, filtering and lossless texture settings for
this pass. No safe removal was established by the initial source audit.

## References

- https://docs.unity3d.com/6000.0/Documentation/Manual/class-AudioClip.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/BuildOptions.CompressWithLz4HC.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Build.Reporting.BuildReport.html
