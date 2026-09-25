# ledger_slots (lane wt/slots)

## PRE job S1 -- rungs.ps1 engine slots
tier: sonnet, effort medium. intent: add -Slot/-Slots to tools\rungs.ps1 (real %TEMP% capture, UTF-8
summary.txt, per-tree lock, slot-0-unchanged + slot n>=1 locks, tag line names the slot).
files: tools/rungs.ps1
HEAD: f3876fb7fbda3e08ac126a4194f6f725665ed2d1
hashes (git hash-object):
  tools/rungs.ps1                    f9f43cbc26007ebadb5158c20df033d6fb025227
  tools/smoketest/run.ps1            ad134d9c0ffbe6ddba0fb3016534699cebf9a2be
  tools/screens/run.ps1              69d9eed4f43a60cccdd1a7309b521803f8ece593
  tools/smoketest/SmokeTest.cs.txt   406da91977a89940daaa72fc3e721ef56d18d618
  tools/screens/Shots.cs.txt         eea15c110cef1c995d09b0344368c59a5123e150
  docs/DESIGN.md                     9637c92641e946f8d9f756cc9e7b07bfcc7d00e1
  docs/README.md                     c6c0a1c13e8eff821ee87e3600bb3eb49b942776
  docs/CHANGES.md                    44ca0246e80b7e3f4c7dd03ab52201f53168a8ac
  CLAUDE.md                          93f6b61e4938c3fffbd05ecad5cc82e853395b5d

## Note
S2 (SmokeTest.cs.txt / screens port-shift, scripts/ seam) and S3 (docs) are NOT started -- S2 alone
touches ~40 port literals across a 10k-line harness file and needs its own dedicated pass (each
literal judged: shift it or leave it literal per the job spec) plus new solo checks, run at rung 3
before commit. Do not attempt S2 in the same sitting as S1 without a fresh context budget.
