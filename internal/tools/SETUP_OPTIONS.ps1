# Shared setup choices. These are separate optional programs, not editor assemblies.
function Get-SetupToolCatalog {
 @(
  [pscustomobject]@{Id='wszst';Name='Wiimms SZS Tools';Recommendation='Recommended';Details='Conversion and validation';Label='Recommended - Wiimms SZS Tools: conversion and validation';Default=$true},
  [pscustomobject]@{Id='wit';Name='Wiimms ISO Tools';Recommendation='For ISO / WBFS';Details='Read and work with disc images';Label='Recommended for ISO/WBFS - Wiimms ISO Tools';Default=$true},
  [pscustomobject]@{Id='RiiStudio';Name='RiiStudio + CLI';Recommendation='Optional';Details='Advanced 3D and model editing';Label='Optional - RiiStudio + CLI: advanced 3D/model editing';Default=$false},
  [pscustomobject]@{Id='SwitchToolbox';Name='Switch Toolbox';Recommendation='Optional';Details='Extra formats. Not needed for HUD or textures.';Label='Optional - Switch Toolbox: extra formats; not needed for HUD/texture editing';Default=$false},
  [pscustomobject]@{Id='BrawlCrate';Name='BrawlCrate';Recommendation='Optional';Details='Specialist formats. Not needed for HUD or layouts.';Label='Optional - BrawlCrate: specialist formats; not needed for HUD/layout editing';Default=$false},
  [pscustomobject]@{Id='ffmpeg';Name='FFmpeg';Recommendation='For audio import';Details='Needed to convert MP3, FLAC and OGG';Label='Optional - FFmpeg: needed for MP3/FLAC/OGG conversion';Default=$false},
  [pscustomobject]@{Id='LoopingAudioConverter';Name='Looping Audio Converter';Recommendation='For BRSTM';Details='Convert audio to BRSTM';Label='Optional - Looping Audio Converter: BRSTM conversion';Default=$false},
  [pscustomobject]@{Id='NintyFont';Name='NintyFont';Recommendation='Optional';Details='Specialist fonts. Not needed for font replacement.';Label='Optional - NintyFont: specialist fonts; not needed for font replacement';Default=$false}
 )
}
function Get-SetupToolJobs([string]$SelectedTools) {
 $catalog=@(Get-SetupToolCatalog)
 $ids=@($SelectedTools -split ',' | ForEach-Object {$_.Trim()} | Where-Object {$_})
 foreach($id in $ids){if($id -notin $catalog.Id){throw "Unknown setup tool: $id"}}
 foreach($entry in $catalog){if($entry.Id -in $ids){ ,@($entry.Id,$entry.Label) }}
}
