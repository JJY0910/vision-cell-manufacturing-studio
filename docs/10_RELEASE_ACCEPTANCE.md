# 10. Release and Demo Acceptance

## Release Levels

### Alpha

- WPF shell + simulator connection
- Dashboard/Motion/I/O basic
- Recipe skeleton
- Basic logs

### Beta

- Teaching/Recipe complete
- Inspection sequence complete
- SQLite persistence
- Offline debug basic
- CI build/test

### Portfolio Release 1.0

- High-quality UI polish
- 2D + synthetic 3D inspection
- Offline debug re-inspection
- Report export
- Demo script/video-ready
- README with architecture and screenshots

## Release 1.0 Acceptance Checklist

- [ ] Dashboard shows equipment state clearly.
- [ ] Connect/disconnect works.
- [ ] Safety interlock prevents unsafe operations.
- [ ] Servo/Home/Jog/Move absolute works.
- [ ] Teaching points can be saved/edited/used.
- [ ] Recipe can be created/validated/activated.
- [ ] Inspection sequence runs end-to-end.
- [ ] 2D inspection detects at least two defect types.
- [ ] 3D height map inspection detects at least one defect type.
- [ ] Inspection result persisted to SQLite.
- [ ] Offline Debug loads result and re-inspects.
- [ ] CSV export works.
- [ ] GitHub Actions build/test works.
- [ ] At least 20 meaningful tests exist.
- [ ] Requirements coverage table updated.
- [ ] Demo script included.

## Demo Story

1. Explain target job: equipment setup, teaching, simulation test.
2. Open Dashboard and connect simulator.
3. Show safety states: Door/EStop/Servo.
4. Servo On and Home axes.
5. Jog to a camera position and save teaching point.
6. Open Recipe and show ROI/inspection params.
7. Start Auto inspection sequence.
8. Show Pass/Fail overlay and cycle time.
9. Inject camera failure or motion timeout.
10. Show Offline Debug re-inspection.
11. Show GitHub PR/CI/test/requirement docs.

## Interview Talking Points

- Simulator-first architecture allows real driver replacement.
- MVVM keeps UI testable and prevents code-behind business logic.
- Safety interlock validates both UI and backend.
- Inspection results are traceable by correlationId.
- Offline Debug reflects real inspection equipment workflow.
- Codex was used as coding agent, but requirements/architecture/process were controlled.
