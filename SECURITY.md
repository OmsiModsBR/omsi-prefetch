# Security policy

## Reporting a vulnerability

Please report a vulnerability privately, through GitHub's private vulnerability reporting: open the **Security** tab of this repository and choose **Report a vulnerability**. Do not open a public issue or pull request for it, and do not post details in discussions.

Say what you found, the version (the `version` of `module.json`), the OmsiLaunch version, how to reproduce it and what you think the impact is. A report is acknowledged as soon as the maintainers can; a fix is released as a new version of the module with a note in `CHANGELOG.md`, and you are credited if you wish.

## Supported versions

Only the latest released version of Omsi Prefetch receives fixes.

## Scope

Omsi Prefetch runs inside the OMSI 2 process, loaded by OmsiLaunch's runtime module bridge, so a flaw in it runs with the rights of the game and of the user who starts it. In scope: the module's own code (the managed and native components), the files it reads and writes, the parsing of the data it reads, and the release archive and its checksum.

Also in scope: the session program `Prefetch.Preloader.exe`, which OmsiLaunch starts before OMSI and which reads game files, and the native component that runs inside OMSI and talks to it over a pipe.

What the module never does:

- It makes no network connection and sends nothing off the machine.
- It does not simulate keyboard or mouse input, click on the screen or automate OMSI's interface.
- It does not change OMSI's add-ons, licences or Steam ownership checks, and the build never copies anything into an OMSI installation.
- It does not follow a path from a data file outside the folder that file belongs to.
- It does not create, edit, rename or delete a file of your OMSI installation, vehicles, maps or add-ons. It reads game files read-only and writes only its own data under `.omsilaunch\module-data\omsi-prefetch`, bounded in size. The only other program it involves is its own session program, started by OmsiLaunch.
- It never holds a file the game rewrites or that OmsiLaunch's session transaction owns.
- On any failure it degrades: the module stops prefetching and OMSI continues unchanged ([errors and degradation](docs/reference/errors-and-degradation.md); [safety](docs/concepts/safety.md)).

Out of scope: vulnerabilities in OMSI itself, in Windows, in OmsiLaunch (report those to [OmsiModsBR/OmsiLaunch](https://github.com/OmsiModsBR/OmsiLaunch)) and in content packages made by third parties.
