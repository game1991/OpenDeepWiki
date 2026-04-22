# update-wsl-hosts.ps1                                                                                                                 
$wslIp = wsl hostname -I                                                                                                               
$wslIp = $wslIp.Trim().Split()[0]                                                                                                      
Write-Host "WSL2 IP: $wslIp"                              

$hostsPath = "C:\Windows\System32\drivers\etc\hosts"
$domains = @("dashboard.local", "opendeepwiki.local")

$content = Get-Content $hostsPath -Raw
foreach ($domain in $domains) {
    $line = "$wslIp    $domain"
    $pattern = "^\s*\d+\.\d+\.\d+\.\d+\s+$domain\s*$"
    if ($content -match $pattern) {
        $content = $content -replace $pattern, $line
    } else {
        $content += "`r`n$line"
    }
}

[System.IO.File]::WriteAllText($hostsPath, $content.Trim() + "`r`n")
ipconfig /flushdns
Write-Host "hosts updated!"