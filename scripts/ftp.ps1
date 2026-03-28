param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("upload", "delete")]
    [string]$Action,

    [Parameter(Mandatory = $true)]
    [string]$Server,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [Parameter(Mandatory = $true)]
    [string]$Password,

    [Parameter(Mandatory = $true)]
    [string]$RemoteDir,

    [Parameter(Mandatory = $true)]
    [string]$RemoteFile,

    [string]$LocalPath = "",

    [int]$Retries = 6
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-RemoteDir([string]$remoteDir) {
    $dir = ($remoteDir ?? "").Trim()
    if ([string]::IsNullOrWhiteSpace($dir) -or $dir -eq "." -or $dir -eq "./" -or $dir -eq "/") {
        return ""
    }
    return ($dir.Trim("/").TrimEnd("/") + "/")
}

function New-FtpUri([string]$server, [string]$remoteDir, [string]$remoteFile) {
    $dir = Normalize-RemoteDir $remoteDir
    return "ftp://$server/$dir$remoteFile"
}

function Try-Upload([string]$uri, [bool]$enableSsl, [string]$localPath) {
    try {
        $bytes = [System.IO.File]::ReadAllBytes($localPath)
        $req = [System.Net.FtpWebRequest]::Create($uri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
        $req.Credentials = New-Object System.Net.NetworkCredential($Username, $Password)
        $req.UsePassive = $true
        $req.UseBinary = $true
        $req.KeepAlive = $false
        $req.Timeout = 20000
        $req.ReadWriteTimeout = 20000
        $req.EnableSsl = $enableSsl
        $req.ContentLength = $bytes.Length

        $stream = $req.GetRequestStream()
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Close()

        $resp = $req.GetResponse()
        $resp.Close()
        return @{ ok = $true; msg = "uploaded" }
    }
    catch [System.Net.WebException] {
        $r = $_.Exception.Response
        if ($r -is [System.Net.FtpWebResponse]) {
            $code = $r.StatusCode
            $desc = $r.StatusDescription
            $r.Close()
            return @{ ok = $false; msg = "$code $desc" }
        }
        return @{ ok = $false; msg = $_.Exception.Message }
    }
    catch {
        return @{ ok = $false; msg = $_.Exception.Message }
    }
}

function Try-Delete([string]$uri, [bool]$enableSsl) {
    try {
        $req = [System.Net.FtpWebRequest]::Create($uri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::DeleteFile
        $req.Credentials = New-Object System.Net.NetworkCredential($Username, $Password)
        $req.UsePassive = $true
        $req.UseBinary = $true
        $req.KeepAlive = $false
        $req.Timeout = 15000
        $req.ReadWriteTimeout = 15000
        $req.EnableSsl = $enableSsl

        $resp = $req.GetResponse()
        $resp.Close()
        return @{ ok = $true; notFound = $false; msg = "deleted" }
    }
    catch [System.Net.WebException] {
        $r = $_.Exception.Response
        if ($r -is [System.Net.FtpWebResponse]) {
            $code = $r.StatusCode
            $desc = $r.StatusDescription
            $r.Close()
            if ($code -eq [System.Net.FtpStatusCode]::ActionNotTakenFileUnavailable) {
                return @{ ok = $false; notFound = $true; msg = $desc }
            }
            return @{ ok = $false; notFound = $false; msg = "$code $desc" }
        }
        return @{ ok = $false; notFound = $false; msg = $_.Exception.Message }
    }
    catch {
        return @{ ok = $false; notFound = $false; msg = $_.Exception.Message }
    }
}

$uri = New-FtpUri -server $Server -remoteDir $RemoteDir -remoteFile $RemoteFile
Write-Host "$Action $uri"

if ($Action -eq "upload") {
    if ([string]::IsNullOrWhiteSpace($LocalPath)) {
        throw "LocalPath is required for upload."
    }
    $resolved = (Resolve-Path $LocalPath).Path
    for ($i = 1; $i -le $Retries; $i++) {
        $r = Try-Upload -uri $uri -enableSsl $false -localPath $resolved
        if ($r.ok) { Write-Host "Uploaded (FTP)."; exit 0 }
        $r2 = Try-Upload -uri $uri -enableSsl $true -localPath $resolved
        if ($r2.ok) { Write-Host "Uploaded (FTPS)."; exit 0 }
        Write-Host "Upload attempt $i failed. FTP: $($r.msg) | FTPS: $($r2.msg)"
        Start-Sleep -Seconds ([Math]::Min(20, 2 * $i))
    }
    throw "Failed to upload after retries."
}

if ($Action -eq "delete") {
    Start-Sleep -Seconds 2
    for ($i = 1; $i -le $Retries; $i++) {
        $r = Try-Delete -uri $uri -enableSsl $false
        if ($r.ok) { Write-Host "Deleted (FTP)."; exit 0 }
        if ($r.notFound) { Write-Host "Not found (already removed)."; exit 0 }
        $r2 = Try-Delete -uri $uri -enableSsl $true
        if ($r2.ok) { Write-Host "Deleted (FTPS)."; exit 0 }
        if ($r2.notFound) { Write-Host "Not found (already removed)."; exit 0 }
        Write-Host "Delete attempt $i failed. FTP: $($r.msg) | FTPS: $($r2.msg)"
        Start-Sleep -Seconds ([Math]::Min(20, 2 * $i))
    }
    throw "Failed to delete after retries."
}

