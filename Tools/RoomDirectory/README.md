# Dedicated room supervisor

See [dedicated-server.md](../../Docs/multiplayer/dedicated-server.md) for build, deployment, network access, logs and measurements.

Run backend tests: `python -m unittest discover -s Tools/RoomDirectory -p "test_*.py" -v`.

The Windows Development smoke can exercise Revenge Kart inputs with two actual
Mirror clients:

```powershell
python Tools/RoomDirectory/smoke_full_clients.py --server <Development-SB6.exe> --client <Development-SB6.exe> --output <new-output-directory> --seconds 90 --diagnostics --revenge-input
```

This explicit fixture enables Revenge, kills the two human actors through native
damage, keeps COM actors durable, and supplies local-slot-1 direction/C plus three
charged A releases per client. It requires native server shots, owner confirmation,
both cart views, and correlated first-visual/confirmation diagnostics without
rejection or timeout. It saves binary hashes and raw captures. The players run
headless: first visual means the presentation code reported a visible pose;
hardware latency, rendered pixels, and offline parity still require manual testing.
