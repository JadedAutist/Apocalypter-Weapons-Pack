# Nexus uploads

The `Nexus upload` GitHub Actions workflow uses the official Nexus-Mods/upload-action, pinned to v1.0.0-beta.11. It has no push or release triggers: run it deliberately after the release checks and author authorization. The game build still requires local game assemblies; this workflow reuses the exact approved installable ZIP already attached to GitHub.

## One-time setup

1. Create a Nexus API key at https://www.nexusmods.com/settings/api-keys.
2. Add it in this repository's Settings > Secrets and variables > Actions as a repository secret named `NEXUSMODS_API_KEY`.
3. Never paste the key into chat, source files, release notes or logs.
4. Run the workflow with Publish disabled. The report lists the public File IDs belonging to the Weapons Pack page. The numeric File ID in an old download URL is also accepted and resolved through the API.
5. Optionally save the correct current main download's File ID as the repository Actions variable `NEXUSMODS_FILE_ID`.

## Verify access

Open Actions > Nexus upload > Run workflow. Choose main, the exact published release tag and the approved ZIP's SHA-256. Leave Publish disabled. This verifies the GitHub ZIP, rejects source/development packages, checks the Nexus account and lists the files on https://www.nexusmods.com/apocalypter/mods/68. It makes only GET requests to Nexus; no upload sessions, versions, changelogs or archives are created.

Run `python -m unittest discover -s tests -p test_nexus_release_check.py` for local preflight regression checks.

## Publish an approved update

After normal gameplay/integration checks, run again with the intended File ID and Publish enabled. The official action uploads the already verified ZIP, marks it as the main download, updates the mod version and includes the GitHub release notes. Archive previous controls whether the prior download is archived. The workflow refuses an already existing Nexus version to prevent accidental duplicate uploads.

A successful verification run proves authentication and read access to the intended page and file. Actual upload permission is established when a real authorized upload succeeds. Do not claim a new download was published unless the upload step reports success and the Nexus file version has been checked. If a publish fails partway, inspect the page before retrying: uploading and adding a changelog are separate API operations.

Documentation: https://github.com/Nexus-Mods/upload-action
