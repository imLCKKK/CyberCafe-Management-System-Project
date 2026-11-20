param()

# Encode AI-generated photos for web delivery; this does not restyle their content.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$foodRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'frontend/public/food'))
$archiveRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot '.tools/menu-image-originals'))
$planPath = Join-Path $repoRoot 'docs/nexora-menu-image-jobs.json'
$plan = Get-Content -Raw -Encoding UTF8 -LiteralPath $planPath | ConvertFrom-Json
if (@($plan.jobs | Where-Object status -ne 'generated').Count -gt 0) {
    throw 'Finish image generation before exporting or archiving production PNGs.'
}
$sources = @(
    @{ Key='com-ga-xoi-mo'; Name='Cơm gà xối mỡ'; Group='rice'; File='frontend/public/food/samples/com-ga-xoi-mo-v1.png' },
    @{ Key='mi-cay-hai-san'; Name='Mì cay hải sản'; Group='noodles'; File='frontend/public/food/samples/mi-cay-hai-san-v1.png' },
    @{ Key='tra-dao-cam-sa'; Name='Trà đào cam sả'; Group='tea'; File='frontend/public/food/samples/tra-dao-cam-sa-v1.png' }
)
foreach ($job in $plan.jobs) {
    if ($job.status -eq 'generated') {
        $sources += @{ Key=$job.key; Name=$job.name; Group=$job.group; File=$job.file; Job=$job }
    }
}

$jpeg = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$encoding = [Drawing.Imaging.EncoderParameters]::new(1)
$encoding.Param[0] = [Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality, [long]86)
$registry = [ordered]@{}
$assets = @()
$replacedPaths = @{}
New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
try {
    foreach ($source in $sources) {
        if ($source.Key -notmatch '^[a-z0-9-]+$') { throw 'Invalid image key' }
        $sourcePath = [IO.Path]::GetFullPath((Join-Path $repoRoot $source.File))
        if (-not $sourcePath.StartsWith($foodRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Image source must stay in the food asset directory'
        }
        $stem = [IO.Path]::GetFileNameWithoutExtension($sourcePath)
        if ($stem -match '\.640$') { $stem = $stem -replace '\.640$', '' }
        $outputs = @{}
        $bitmap = $null
        foreach ($size in @(320, 640)) {
            $target = Join-Path $foodRoot "$stem.$size.jpg"
            if (-not (Test-Path -LiteralPath $target)) {
                if ($null -eq $bitmap) { $bitmap = [Drawing.Image]::FromFile($sourcePath) }
                $thumbnail = [Drawing.Bitmap]::new($size, $size)
                $graphics = [Drawing.Graphics]::FromImage($thumbnail)
                try {
                    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
                    $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                    $graphics.DrawImage($bitmap, 0, 0, $size, $size)
                    $thumbnail.Save($target, $jpeg, $encoding)
                } finally { $graphics.Dispose(); $thumbnail.Dispose() }
            }
            $outputs[$size] = '/food/' + [IO.Path]::GetFileName($target)
        }
        if ($null -ne $bitmap) { $bitmap.Dispose() }
        $registry[$outputs[640]] = $outputs[320]
        $replacedPaths[$source.File] = 'frontend/public' + $outputs[640]
        $assets += [ordered]@{ key=$source.Key; name=$source.Name; group=$source.Group; imageUrl=$outputs[640]; thumbnailUrl=$outputs[320] }
        if ($source.Job) {
            # Preserve raw production PNGs locally; approved sample PNGs remain in place.
            if ([IO.Path]::GetExtension($sourcePath) -eq '.png') {
                $archivePath = [IO.Path]::GetFullPath((Join-Path $archiveRoot ([IO.Path]::GetFileName($sourcePath))))
                if (-not $archivePath.StartsWith($archiveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Archive target escaped the workspace image archive'
                }
                if (Test-Path -LiteralPath $sourcePath) {
                    if (Test-Path -LiteralPath $archivePath) { throw 'Raw image archive already exists; inspect it before replacing' }
                    Move-Item -LiteralPath $sourcePath -Destination $archivePath
                } elseif (-not (Test-Path -LiteralPath $archivePath)) {
                    throw 'Raw production image and archive are both missing'
                }
                $source.Job | Add-Member -NotePropertyName source_png_local -NotePropertyValue ('.tools/menu-image-originals/' + [IO.Path]::GetFileName($sourcePath)) -Force
            }
            $source.Job.file = 'frontend/public' + $outputs[640]
            $source.Job | Add-Member -NotePropertyName image_url -NotePropertyValue $outputs[640] -Force
            $source.Job | Add-Member -NotePropertyName thumbnail_url -NotePropertyValue $outputs[320] -Force
        }
    }
} finally { $encoding.Dispose() }

foreach ($job in $plan.jobs) {
    if ($job.PSObject.Properties['reference_files']) {
        $job.reference_files = @($job.reference_files | ForEach-Object {
            if ($replacedPaths.ContainsKey($_)) { $replacedPaths[$_] } else { $_ }
        })
    }
    if ($job.PSObject.Properties['previous_file']) {
        $previousPath = [IO.Path]::GetFullPath((Join-Path $repoRoot $job.previous_file))
        if (Test-Path -LiteralPath $previousPath) {
            if (-not $previousPath.StartsWith($foodRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Previous candidate must stay in the food asset directory'
            }
            $previousArchive = [IO.Path]::GetFullPath((Join-Path $archiveRoot ([IO.Path]::GetFileName($previousPath))))
            if (-not $previousArchive.StartsWith($archiveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Previous candidate archive escaped the workspace archive'
            }
            if (Test-Path -LiteralPath $previousArchive) { throw 'Previous candidate archive already exists' }
            Move-Item -LiteralPath $previousPath -Destination $previousArchive
            $job | Add-Member -NotePropertyName previous_png_local -NotePropertyValue ('.tools/menu-image-originals/' + [IO.Path]::GetFileName($previousPath)) -Force
        }
    }
}

$utf8 = New-Object Text.UTF8Encoding($false)
$plan.status = 'images_exported'
[IO.File]::WriteAllText((Join-Path $repoRoot 'frontend/src/shared/catalog/foodImageSources.json'), ($registry | ConvertTo-Json -Depth 10) + "`n", $utf8)
[IO.File]::WriteAllText((Join-Path $repoRoot 'docs/nexora-menu-assets.json'), ([ordered]@{ task='SCRUM-34'; kind='image_assets_only'; assets=$assets } | ConvertTo-Json -Depth 10) + "`n", $utf8)
[IO.File]::WriteAllText($planPath, ($plan | ConvertTo-Json -Depth 20) + "`n", $utf8)
Write-Output "Exported $($assets.Count) image pairs (320px/640px); raw production PNGs archived locally."
