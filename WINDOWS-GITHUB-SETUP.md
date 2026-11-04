# Running the fork from Git on Windows

The Git checkout contains the source. The build creates a separate, runnable
package under `_output`; your Kavita data lives in that package's `config`
folder, not in Git.

1. Commit and push the desired changes from your development PC. On the server,
   install Git (with Scoop, `scoop install git`) and clone the fork and branch.
   For example: `git clone --branch develop https://github.com/Lunarfyre-ST/Kavita.git`
2. In the checkout, double-click **Install Build Dependencies.bat**. It uses
   compatible existing Node and .NET 10 SDK installations where available;
   otherwise it uses Scoop. It installs the project's npm and NuGet packages.
   This requires network access and may take several minutes.
3. Double-click **Build Kavita Fork.bat**. It creates a dated folder in
   `_output`, containing **Start Kavita Fork.bat** and its own runtime.
4. For a fresh installation, run the launcher and open `http://localhost:5051`.
   To bring over a Scoop installation, stop it, back up its entire `config`
   folder, and copy the backup into the new package before starting. See the
   package's README for details and keep the original backup.

For later updates, double-click **Update Kavita Fork.bat**. It requires a clean
Git checkout and a configured upstream branch, pulls only a fast-forward update,
installs frontend dependencies, and builds a new package. It never stops the
running server or replaces its data. Stop Kavita, back up its `config` folder,
copy that folder into the new package, and launch the new package. Keep the old
backup because a newer version may migrate the database.

The setup writes machine-specific tool paths to the Git-ignored file
`scripts/fork-build.local.json`. If tools are moved or removed, edit that file
or remove it and run setup again. The dependency installer does not replace an
existing settings file. The original Scoop installation is never uninstalled.
