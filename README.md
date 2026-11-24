# NillySearch Pro — Ultra-Fast Instant File Finder for Windows 10/11

> **Πανάλαφρη (~70 KB) αστραπιαία αναζήτηση αρχείων για Windows 10/11 — ιδανική για παλιά & νέα PC (εναλλακτική των αργών Windows Search & Everything, χωρίς background service!).**
> **0 MB RAM στο παρασκήνιο** · **0% CPU όταν είναι κλειστό** · **0 εξωτερικά DLLs** · **100% Δωρεάν (MIT · 0€)**.
>
> **created by [Nilly graphics (Sotiris Christos)](https://nillygraphics.com/)**

---

## Γιατί NillySearch Pro; (Ιδανικό για παλιά laptop 4–8 GB RAM)

| Λύση | Background RAM / Service | Ταχύτητα Αναζήτησης | Ελληνικά (χωρίς τόνους) | Δικαιώματα Admin | Μέγεθος |
| :--- | :---: | :---: | :---: | :---: | :---: |
| Windows 11 Search (`SearchHost.exe`) | **150–350 MB + Indexer 24/7** | Αργή (Bing/Web lag) | Μερική | Ενσωματωμένο | Βαρύ |
| Everything (voidtools) | Background Service | Άμεση | Όχι (τόνοι/ς) | Απαιτεί Service/Admin | ~2–5 MB |
| **NillySearch Pro** | **0 MB (Κανένα Service!)** | **Αστραπιαία (Virtual Grid)** | **Ναι (τόνοι & `ς ↔ σ`)** | **Όχι (Zero Admin!)** | **~70 KB** |

---

## Τι κάνει το NillySearch Pro

- **Native Win32 `FindFirstFileExW` (`LARGE_FETCH`) Παράλληλη Σάρωση:**
  - Σαρώνει όλους τους δίσκους (`C:\`, `D:\`, `J:\`…) σε λίγα δευτερόλεπτα χωρίς δικαιώματα Administrator και χωρίς να κολλάει σε junctions/reparse loops.
  - **Τοπική Binary Cache (`.nillysearch_index.bin`):** στο επόμενο άνοιγμα φορτώνει **100.000+ αρχεία σε ~100 ms** για άμεση αναζήτηση πριν καν τελειώσει η ανανέωση!
- **Virtualized 60 FPS Λίστα Αποτελεσμάτων:**
  - Ακόμα και με 200.000 αποτελέσματα δεν κολλάει ποτέ και κρατάει τη RAM στο ελάχιστο.
- **Έξυπνη Αναζήτηση (Ελληνικά & Αγγλικά):**
  - Αγνοεί τόνους & τελικό σίγμα (`προσφορα` βρίσκει `Προσφορά_Πελάτη.pdf`, `λογοτυποσ` βρίσκει `Λογότυπος.svg`).
  - Πολλαπλές λέξεις (`nilly pdf`), επεκτάσεις (`*.psd`, `ext:pdf`), και φίλτρα μεγέθους (**`>10mb`**, **`>100mb`**, **`<500kb`**) για να βρίσκεις αμέσως τι γεμίζει τον δίσκο σου!
- **Φίλτρα 1-Κλικ:**
  - **Όλα · Φάκελοι · Έγγραφα · Εικόνες · Design (PSD/AI/AF/SVG) · Βίντεο/Ήχος · Συμπιεσμένα · Εφαρμογές**.
  - Επιλογή δίσκου (`Όλοι οι δίσκοι`, `C:\`, `J:\`, ή custom φάκελος) + προαιρετικός διακόπτης `+System` για να μένουν καθαρά τα αποτελέσματα από αρχεία συστήματος.
- **Γρήγορες Συντομεύσεις:**
  - `Enter` / Διπλό κλικ → Άνοιγμα αρχείου
  - `Ctrl+Enter` → Εμφάνιση & επιλογή στον Windows Explorer
  - `Ctrl+C` → Αντιγραφή πλήρους διαδρομής
  - `Ctrl+F` → Εστίαση αναζήτησης · `F5` → Ανανέωση δίσκων · `Esc` → Καθαρισμός
  - Δεξί κλικ → Άνοιγμα, Εμφάνιση στον φάκελο, Αντιγραφή διαδρομής/ονόματος, Ιδιότητες & **SHA-256**.

---

## Εγκατάσταση στα Windows 10/11 (100% Portable · Χωρίς Admin)

1. Εγκατάσταση με **1 κλικ** μέσα από το **[NillyHub](https://github.com/nillynp/nillyhub/releases/latest)** — ή κατέβασε το **`NillySearch-Pro-v1.0-Portable.zip`** από τα [GitHub Releases](https://github.com/nillynp/nillysearch-pro/releases).
2. Αποσυμπίεσε και τρέξε με διπλό κλικ το **`Install_NillySearchPro.bat`** *(δεν απαιτεί δικαιώματα Administrator)*.

### Απεγκατάσταση
Τρέξε το **`Uninstall_NillySearchPro.bat`** — καθαρίζει μόνο τις συντομεύσεις και την τοπική cache.

---

## Author, Credits & SEO

- **Created by:** **[Nilly graphics (Sotiris Christos)](https://nillygraphics.com/)**
- **Website:** [https://nillygraphics.com/](https://nillygraphics.com/)
- **GitHub:** [https://github.com/nillynp/nillysearch-pro](https://github.com/nillynp/nillysearch-pro)
- **License:** MIT License — 100% Free & Open Source (0€)

> **English Summary:** **NillySearch Pro** is an ultra-lightweight (~70 KB), open-source native Windows 10/11 instant file finder built with Win32 `FindFirstFileExW`, virtualized 60 FPS results, instant binary cache, size/extension filters (`>50mb`, `ext:pdf`), Greek accent-insensitive search, zero admin rights, and 0 MB background RAM — **created by Nilly graphics (Sotiris Christos)** ([nillygraphics.com](https://nillygraphics.com/)).
> **Keywords:** `NillySearch Pro`, `fast Windows 11 file search`, `Everything alternative no admin`, `Windows Search replacement low RAM`, `lightweight file finder`, `Nilly Suite`, `Nilly graphics`, `Sotiris Christos`, `nillygraphics.com`
