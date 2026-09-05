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
and what to redo after a Coop update.

## This repository

This repo is the module's source/build tree, not something you need to
install the mod. See [`AGENTS.md`](AGENTS.md) for the decompile/build
workflow if you're modifying the mod itself.
