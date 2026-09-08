# Improved Garrisons

A Mount & Blade II: Bannerlord module that automates garrison management —
recruiting, training, and moving garrison parties between your settlements —
with optional multiplayer support via [BannerlordCoop](https://steamcommunity.com/sharedfiles/filedetails/?id=3770450698).

Targets game version v1.4.8.

## Download

- Steam Workshop: https://steamcommunity.com/sharedfiles/filedetails/?id=3793463493
- GitHub Releases: https://github.com/norto22/improved-garrisons/releases

Each release is a single `ImprovedGarrisons` module folder, identical whether
you get it from Steam or GitHub.

## Client install

Copy the `ImprovedGarrisons` folder into your Bannerlord `Modules` directory,
then enable it in the launcher.

## Dedicated server install

Improved Garrisons only runs on a dedicated server through BannerlordCoop.
Install a working BannerlordCoop dedicated server first, then follow
[`ImprovedGarrisons/SERVER-INSTALL.txt`](ImprovedGarrisons/SERVER-INSTALL.txt)
in the downloaded module — it covers the extra manifest patch a Coop
dedicated server needs to actually activate this module, verification steps,
and what to redo after a Coop update. The guide covers Coop v0.1.5's paired
server/client builds, persistent save directory, shared gameplay configuration,
and preserving IG settings during updates.

## Garrison templates and surplus guards

The Template page shows **Actual garrison** (stationed troops, wounded counts,
and current troops / capacity) separately from **Template targets** (the desired
composition). **Edit template troops** changes those targets; it does not add or
remove real soldiers. Choose **Done** to save, including an empty template, or
**Cancel** to keep the previous targets. Removed types stay out of the targets
when reopening the editor, but remain available in its troop-selection list.

Under Guard Parties, **Auto-create guards from surplus troops** is off by default.
When enabled, it creates a guard using the configured **Automatic guard creation
party size**, drawn only from healthy troops above template targets or outside
the template. Troops that can upgrade into still-needed targets are reserved.
Unlimited targets retain their troops and upgradeable recruits. Heroes and
wounded troops stay in the garrison.

The garrison waits until a full eligible batch is available, no guard already
exists, and the settlement is not under siege or raid. An empty or missing
template causes no transfers. This mode uses the guard-party size, not the
ordinary total-garrison creation threshold, and takes priority over ordinary
automatic patrol selection. Guard replenishment also respects template needs
while this option is enabled; the separate village-defense setting remains
independent.

The Guard Parties header shows surplus-guard readiness, including eligible troops
against the required party size and the current reason for waiting. It updates
as troops and settings change, and identifies an active guard, a settlement under
attack, a missing template or garrison, or a batch ready for the next hourly check.

| Surplus guards | Automatic removal | Unneeded troops |
|---|---|---|
| Off | Off | Stay in the garrison. |
| On | Off | Form a guard when the conditions above are met. |
| Off | On | Follow the existing automatic-removal behavior. |
| On | On | Wait for guards; automatic removal does not dismiss them. |

Disabling surplus guards leaves existing guard parties alone. Normal guard
orders and other independently enabled management features continue to apply.

## This repository

This repo is the module's source/build tree, not something you need to
install the mod. See [`AGENTS.md`](AGENTS.md) for the decompile/build
workflow if you're modifying the mod itself.
