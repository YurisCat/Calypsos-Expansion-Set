param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 18764,
    [switch]$NoBrowser,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $PSScriptRoot 'IdolWorkbench.py'
$python = Get-Command python.exe -ErrorAction Stop
$projectId = & $python.Source -c 'import hashlib,pathlib,sys; print(hashlib.sha256(str(pathlib.Path(sys.argv[1]).resolve()).encode("utf-8")).hexdigest()[:16])' $projectRoot
if ($LASTEXITCODE -ne 0) { throw '无法读取工作台项目身份。' }
$pythonWindowless = Join-Path (Split-Path -Parent $python.Source) 'pythonw.exe'
if (!(Test-Path -LiteralPath $pythonWindowless)) {
    throw '需要 Python 3.10 或更新版本及 pythonw.exe。'
}

if ($Stop) {
    & $python.Source $backend --port $Port --stop
    if ($LASTEXITCODE -ne 0) { throw '工作台未能关闭，详见上方错误。' }
    Write-Output '工作台后台已停止。'
    return
}

$startArguments = @(('"{0}"' -f $backend), '--port', $Port)
if (!$NoBrowser) { $startArguments += '--open' }
Start-Process -FilePath $pythonWindowless -ArgumentList $startArguments `
    -WorkingDirectory $projectRoot -WindowStyle Hidden | Out-Null

$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/health" -TimeoutSec 1
        if ($health.app -eq 'ces-idol-workbench' -and $health.projectId -eq $projectId) {
            $ready = $true
            break
        }
    } catch { }
    Start-Sleep -Milliseconds 200
}
if (!$ready) { throw '工作台启动失败；请检查 Python、需求文件或本机端口。' }
Write-Output "工作台已就绪：http://127.0.0.1:$Port/"
