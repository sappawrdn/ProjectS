# Project S — Setup buat Teammate

Art + scene udah ke-commit, jadi kamu **tinggal pull → buka → jadi**. Ga usah download art / run menu apa-apa.

1. **Install Unity `6000.4.3f1`** lewat Unity Hub (versi harus persis ini).
2. `git pull` (atau clone: `git clone https://github.com/sappawrdn/ProjectS.git`).
3. Buka folder `ProjectS` di Unity Hub → tunggu dia **resolve package + rebuild Library** (beberapa menit, sabar).
   Plugin Apple auto ke-resolve dari `LocalPackages/`. Warning "no macOS library for Apple.Accessibility" = aman, abaikan.
4. Buka **`Assets/Scenes/Level3.unity`** → pencet **Play**. WASD jalan, mouse noleh.

Yang keliatan = persis punya Sappa (maze + texture PSX + WallTemplate + pintu + lampu merah + 3 key + exit).

**Catatan:** haptics + spatial audio cuma kerasa di iPhone asli (tahap device, nanti). `CLAUDE.md` di root itu
lokal/gitignored — bukan buat kamu. Design bible & status ada di `memory-bank/` (baca `progress.md` dulu).
