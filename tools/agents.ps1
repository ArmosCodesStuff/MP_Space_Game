# agents.ps1 [-Minutes 30] [-Loop] | -Tokens [-Run <id or fragment>] -- the watchdog on lower-model agents, and the
# token measure (owner 2026-09-25: iterate the process with numbers). Read-only.
# Watchdog: lists every RUNNING workflow agent on Sonnet or Haiku that has run for -Minutes or longer: its workflow, id,
# model, age, idle time (since its transcript last grew), transcript size and label. Opus agents are not listed. -Loop
# checks every -Minutes and EXITS, printing the flagged agents, the first time one is flagged: run it in the background so
# the coordinator is woken only when there is something to look at.
# -Tokens: per agent transcript (agent-*.jsonl; the journal carries no usage) the sum over every API turn of
# input + cache_creation + cache_read (each turn re-reads the context, so the sum is the true cost) and output tokens;
# one line per agent (phase / label, model, turns, in, out) and a TOTAL per workflow. -Run limits it to one workflow;
# otherwise the journals of the last 24 h. The retrospective reads this, never a transcript.
param([int]$Minutes = 30, [switch]$Loop, [switch]$Tokens, [string]$Run, [string]$Root = (Join-Path $env:USERPROFILE '.claude\projects'))

function Journals([double]$hours) {
  $now = Get-Date
  Get-ChildItem $Root -Recurse -Filter journal.jsonl -ErrorAction SilentlyContinue |
    Where-Object { ($Run -and $_.DirectoryName -like "*$Run*") -or (-not $Run -and $_.LastWriteTime -gt $now.AddHours(-$hours)) }
}

function Check {
  $now = Get-Date
  Journals 12 | ForEach-Object {
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

function TokenReport {
  Journals 24 | Sort-Object LastWriteTime | ForEach-Object {
    $dir = $_.DirectoryName
    $labels = @{}
    foreach ($line in Get-Content $_.FullName) {
      try { $j = $line | ConvertFrom-Json } catch { continue }
      if ($j.type -eq 'started') { $labels[$j.agentId] = "$($j.phase) / $($j.label)" }
    }
    "== {0} ({1:MM-dd HH:mm})" -f (Split-Path $dir -Leaf), $_.LastWriteTime
    $tin = [long]0; $tout = [long]0
    foreach ($tr in Get-ChildItem $dir -Filter 'agent-*.jsonl') {
      $id = $tr.BaseName.Substring(6)
      $in = [long]0; $out = [long]0; $turns = 0
      foreach ($line in Get-Content $tr.FullName) {
        if ($line -notmatch '"usage"') { continue }
        $u = $null
        try { $u = ($line | ConvertFrom-Json).message.usage } catch { continue }
        if (-not $u) { continue }
        $turns++
        $in += [long]$u.input_tokens + [long]$u.cache_creation_input_tokens + [long]$u.cache_read_input_tokens
        $out += [long]$u.output_tokens
      }
      $model = ''
      $meta = Join-Path $dir "agent-$id.meta.json"
      if (Test-Path $meta) { try { $model = [string](Get-Content $meta -Raw | ConvertFrom-Json).model } catch {} }
      "  {0,-44} {1,-24} turns={2,3} in={3,7:N2}M out={4,6:N1}k" -f $labels[$id], $model, $turns, ($in / 1e6), ($out / 1e3)
      $tin += $in; $tout += $out
    }
    "  TOTAL in={0:N2}M out={1:N1}k" -f ($tin / 1e6), ($tout / 1e3)
  }
}

if ($Tokens) { TokenReport }
elseif ($Loop) {
  while ($true) {
    Start-Sleep -Seconds ($Minutes * 60)
    $flagged = @(Check)
    if ($flagged.Count) { $flagged; exit 0 }
  }
} else { Check }
