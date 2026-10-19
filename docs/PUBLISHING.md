# Publishing

1. Build and run the automated checks in BUILDING.md. Complete the live checklist in TESTING.md, including a clean setup with only the supported game and BepInEx. Record unverified checks honestly before making the release decision.
2. Run scripts/package.ps1. It writes the installable ZIP and SHA-256 file under dist. Use the same verified runtime files for GitHub and Nexus Mods.
3. Review git status and git diff. Include source, approved runtime assets, tests and documentation. Build output, personal files and release archives stay out of source control.
4. Commit the reviewed changes with a message such as `Prepare Weapons Pack 1.6.3`. Push only when authorized.
5. Create a draft GitHub release titled `Apocalypter Weapons Pack 1.6.3`, using a new `1.6.3` tag on that exact source commit. Do not reuse or move an earlier release tag. Attach `Apocalypter-Weapons-Pack-1.6.3.zip` and its SHA-256 file. Copy the player-facing 1.6.3 section from CHANGELOG.md.
6. If attaching a separate `Apocalypter-Weapons-Pack-1.6.3-Source.zip`, verify that it contains only public source, documentation, tests and approved assets. It must correspond to the same source commit. GitHub's automatic source downloads are generated from the release tag; uploading a separate archive does not update them.
7. Leave the release as a draft while required checks are outstanding. Publish and mark latest only after the release decision and explicit authorization.
8. Upload the same installable ZIP to Nexus Mods, set version 1.6.3 and use the same player-facing changelog. Verify the resulting download and file identity before retiring the previous main file.

For authorized Nexus uploads through GitHub Actions, follow [Nexus upload setup and verification](NEXUS_UPLOAD.md). The workflow is manual and defaults to read-only verification.

Players install the explicit release ZIP. Source archives are for development. Keep the plugin GUID, DLL identity and plugin folder stable for existing saves, settings and mod integrations.

The existing repository is [Apocalypter-Weapons-Pack](https://github.com/JadedAutist/Apocalypter-Weapons-Pack). Committing, pushing, tagging and publishing are separate actions; preparing local archives performs none of them.
