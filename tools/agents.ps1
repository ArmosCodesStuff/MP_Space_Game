# agents.ps1 [-Minutes 30] [-Loop] -- the watchdog on lower-model agents (CLAUDE.md section 4.1, owner 2026-09-25).
# Lists every RUNNING workflow agent on Sonnet or Haiku that has run for -Minutes or longer: its workflow, id, model, age,
# idle time (since its transcript last grew), transcript size and label. Opus agents are not listed. -Loop checks every
# -Minutes and EXITS, printing the flagged agents, the first time one is flagged: run it in the background so the
# coordinator is woken only when there is something to look at, then look (its transcript's tail), stop it and redo it
# on Opus if it is wasting time or tokens, and start the loop again. Read-only.
param([int]$Minutes = 30, [switch]$Loop, [string]$Root = (Join-Path $env:USERPROFILE '.claude\projects'))

function Check {
  $now = Get-Date
  Get-ChildItem $Root -Recurse -Filter journal.jsonl -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -gt $now.AddHours(-12) } | ForEach-Object {
      $dir = $_.DirectoryName
      $started = @{}; $done = @{}
      foreach ($line in Get-Content $_.FullName) {
        try { $j = $line | ConvertFrom-Json } catch { continue }
        if ($j.type -eq 'started') { $started[$j.agentId] = $j.label } elseif ($j.type -eq 'result') { $done[$j.agentId] = 1 }
      }
      foreach ($id in @($started.Keys)) {
        if ($done.ContainsKey($id)) { continue }
        $meta = Join-Path $dir "agent-$id.meta.json"
        $tr = Join-Path $dir "agent-$id.jsonl"
        if (-not (Test-Path $meta) -or -not (Test-Path $tr)) { continue }
        $model = [string](Get-Content $meta -Raw | ConvertFrom-Json).model
        if ($model -notmatch 'sonnet|haiku') { continue }
        $f = Get-Item $tr
        $age = ($now - $f.CreationTime).TotalMinutes
        $idle = ($now - $f.LastWriteTime).TotalMinutes
        # A transcript silent for 3 hours belongs to a workflow that was stopped, not to a running agent.
        if ($age -ge $Minutes -and $idle -lt 180) {
          "{0} {1} [{2}] running {3:N0} min, idle {4:N0} min, {5:N0} KB: {6}" -f (Split-Path $dir -Leaf), $id, $model, $age, $idle, ($f.Length / 1KB), $started[$id]
        }
      }
    }
}

if ($Loop) {
  while ($true) {
    Start-Sleep -Seconds ($Minutes * 60)
    $flagged = @(Check)
    if ($flagged.Count) { $flagged; exit 0 }
  }
} else { Check }
