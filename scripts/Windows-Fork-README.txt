Kavita Fork for Windows x64
===========================

Copy the entire folder to the server PC and double-click Start Kavita Fork.bat.
Keep the window open while Kavita runs. Press Ctrl+C in the window to stop it.
The bundled .NET runtime is included; the server PC does not need the SDK.

A new installation starts at http://localhost:5051 and stores its data in the
config folder next to the launcher. From another device use the server PC's
address instead of localhost, subject to your Windows firewall settings.

To use a copy of your existing Scoop installation:
1. Stop the Scoop Kavita instance completely, and back up its entire config
   folder somewhere safe. The database and settings are in that folder.
2. Copy the entire backed-up config folder into this package, replacing the
   empty first-run config folder. Do not move or delete your original backup.
3. Start the fork. It will use the port in config\appsettings.json, usually
   5000, and may migrate the copied database. Do not run two instances against
   the same data or on the same port. Check library paths on the server PC.

Keep the backup from before the first fork launch: database migrations may make
the copied data incompatible with an older Scoop version. The fork and Scoop
installations are separate and do not automatically sync reading progress.

To update this fork, build a new package, stop the old instance, back up its
entire config folder, and copy that config into the new package before starting.
Never overwrite your only copy of the config folder.

To build a new package from the source checkout, use Build Kavita Fork.bat.
Building requires a compatible .NET 10 SDK, Node.js and installed frontend
dependencies. Optional local tool paths can be set in the ignored file
scripts\fork-build.local.json; they are specific to the build PC.
