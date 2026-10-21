param([Parameter(Mandatory = $true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$logo = [Drawing.Image]::FromFile((Join-Path (Split-Path $PSScriptRoot -Parent) 'logo.png'))
try {
    foreach ($appearance in @('light', 'dark')) {
        $bitmap = New-Object Drawing.Bitmap 328, 628
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $rectangle = New-Object Drawing.Rectangle 0, 0, 328, 628
        $start = [Drawing.ColorTranslator]::FromHtml('#172441')
        $end = [Drawing.ColorTranslator]::FromHtml('#263F67')
        if ($appearance -eq 'dark') { $start = [Drawing.ColorTranslator]::FromHtml('#101826'); $end = [Drawing.ColorTranslator]::FromHtml('#1B2C46') }
        $gradient = New-Object Drawing.Drawing2D.LinearGradientBrush $rectangle, $start, $end, 90.0
        $heading = New-Object Drawing.Font 'Segoe UI', 30, ([Drawing.FontStyle]::Bold), ([Drawing.GraphicsUnit]::Pixel)
        $body = New-Object Drawing.Font 'Segoe UI', 16, ([Drawing.FontStyle]::Regular), ([Drawing.GraphicsUnit]::Pixel)
        $caption = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#C8D9F1'))
        try {
            $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.FillRectangle($gradient, $rectangle)
            $graphics.DrawImage($logo, 44, 96, 240, 240)
            $graphics.DrawString('Gap Audio', $heading, [Drawing.Brushes]::White, 44, 374)
            $graphics.DrawString('V1.0.0', $body, $caption, 46, 423)
            $graphics.DrawString('Offline Audio Editor', $body, $caption, 46, 548)
            $bitmap.Save((Join-Path $OutputDirectory ('wizard-' + $appearance + '.png')), [Drawing.Imaging.ImageFormat]::Png)
        } finally { $caption.Dispose(); $body.Dispose(); $heading.Dispose(); $gradient.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $logo.Dispose() }
