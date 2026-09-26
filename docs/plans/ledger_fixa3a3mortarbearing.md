# ledger_fixa3a3mortarbearing (round a3, kind code -> check)

## PRE
- Task: LaneA6bMortarChecks runs 0-2 placed False (tp4ar3_0/2_solo.fails.txt:1136, seed 314566543).
- Reproduced: fixa3a3mortarbearing_a solo@314566543 lines 1136-1138 FAIL (cursor 603 u -> 617 u).
- Root cause: the check. Space goes down the frame the hull is teleported home; the camera lerps (Hub.MoveCamera,
  6/s) so the window point AimWorld set reads a different world point by the time the ship reads the mouse.
  LobPoint/Lob match the card (clamp 150..1100 on the cursor's bearing); the code is right.
- Fix: settle 90 frames (hull held, cursor re-aimed) before Space; nothing asserted changes.
