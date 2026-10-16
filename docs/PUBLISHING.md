# Publishing

This checkout contains the public two-weapon source. The author keeps the experimental weapon workspace outside this Git repository.

1. Build, run the automated checks, and complete the live checklist in TESTING.md.
2. Run scripts/package.ps1. It writes the installable ZIP and SHA-256 file under dist.
3. Review `git status` and `git diff`. Stage source, approved runtime assets, tests and docs. Build output, personal files and release ZIPs are ignored.
4. Commit the changes with a clear message, such as `Prepare 1.6.1 merchant and settings fixes`, then push main to the existing repository.
5. Draft a GitHub release targeting that commit with tag v1.6.1 and title Apocalypter Weapons Pack 1.6.1. Copy the relevant CHANGELOG.md section and attach the installable ZIP from dist. Save as a draft while live checks are outstanding; publish as latest when ready.
6. Upload the same ZIP to Nexus Mods so both downloads have the same hash.

For authorized Nexus uploads through GitHub Actions, follow [Nexus upload setup and verification](NEXUS_UPLOAD.md). The workflow is manual and defaults to read-only verification.

GitHub's generated Source code ZIP contains this source project and is not the installable mod. The game download is the explicit ZIP attached to the release. Keep the plugin GUID and plugin folder stable for Apocasetter updates.

The repository remote is https://github.com/JadedAutist/Apocalypter-Weapons-Pack. GitHub Desktop can open this existing local checkout through File > Add local repository. Review Changes, commit to main, then Push origin. Publishing a release is a separate action on GitHub.
