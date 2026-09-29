# Duettino — Brief di progetto

> Documento di partenza, frutto di una sessione di brainstorming. Serve da input per
> `/grill-with-docs` (per ricavare `GLOSSARY.md` e le ADR) e poi `/to-spec` → `/to-tickets`.
> Niente codice è stato ancora scritto.

## 0. Nome

**Duettino**: un piccolo duetto, cioè le due fonti (quello che dici e quello che senti) insieme,
in un'app piccola e leggera.

- **Repo:** `duettino`
- **Descrizione GitHub:** *Lightweight Windows recorder: mix any input device with whatever is
  playing on any output device into one MP3. Calls, meetings, videos, anything. No drivers.*
  La funzione è generica; le call sono l'esempio principale (ed è la parola che la gente cerca),
  non l'unico uso. Il README darà la spiegazione estesa (vedi §11).
- **Perché non "Duetto":** ci sono 85 repository su GitHub con quel nome (quasi tutti
  irrilevanti), ma soprattutto su Google domina [Duetto Research](https://www.duettocloud.com/en-us/),
  un'azienda di software per hotel. Il nome sarebbe introvabile e ci sarebbe un piccolo
  rischio di sovrapposizione col loro marchio. "Duettino" risultava libero (0 repository) a settembre 2026.
- **Scartati:** *Earshot* (già usato da un'app commerciale che fa quasi la stessa cosa,
  [tryearshot.app](https://tryearshot.app/)), *BothSides*, *HeadsetRec*, *TwoWay*.

## 1. Problema

Voglio registrare le call di lavoro (Teams, Zoom, Meet, …) che faccio da PC Windows.

- **Senza cuffie** è facile: le voci escono dalle casse e basta un telefono che registra.
- **Con le cuffie** diventa impossibile: la voce degli altri esce solo nelle cuffie, nessun
  registratore esterno la sente.

Mi serve un'app **semplice e leggera** che registri insieme **un dispositivo di ingresso a
scelta** (di solito il microfono) e **ciò che suona su un dispositivo di uscita a scelta**, in
un unico file audio.

Le cuffie sono il caso che ha fatto nascere il bisogno, ma **non sono un requisito**: l'uscita
può essere anche le casse del PC, un monitor HDMI, una dock USB…, e l'ingresso un microfono
qualsiasi, anche di un dispositivo diverso dall'uscita.

Anche le call sono solo il caso d'origine: l'app registra **qualsiasi cosa** stia suonando
sull'Uscita insieme all'Ingresso, che si sia in call o no. Altri usi: webinar e lezioni
online, video commentati, sessioni di gioco con la propria voce, prove musicali con una
base, podcast da remoto.

## 2. Alternative valutate (settembre 2026)

| Strumento | Perché no |
|---|---|
| OBS Studio | Fa il lavoro, ma è pesantissimo e pieno di funzioni che non servono. |
| Xbox Game Bar | Scomoda; orientata a registrare giochi/video. |
| [Audacity](https://windowsforum.com/news/record-windows-11-system-audio-in-audacity-with-wasapi-loopback.415374/) | Cattura il loopback WASAPI, ma **un solo dispositivo alla volta**: non mixa mic + uscita. |
| [AudioCapture](https://github.com/masonasons/AudioCapture) | La più vicina (mic + audio di sistema in un file), ma nessun binario pronto, va compilata, ~40 stelle, fa molto di più del necessario (cattura per processo). |
| [Reco](https://github.com/dosxnjos/reco) | Mic + loopback in MP3, ma ~810 MB per via della trascrizione Whisper integrata. |
| [teamsrec-capture](https://github.com/drzdez/teamsrec-capture) | Legata a Teams, progetto di nicchia. |
| [wasamix](https://github.com/ytchenak/wasamix) | Mixa verso un cavo virtuale, non registra su file. |
| ScreenSnap Pro, EaseUS RecExperts | Commerciali, orientati allo schermo. |
| Registrazione nativa Teams/Zoom | Avvisa tutti, finisce su OneDrive/cloud, non sempre disponibile. |

**Conclusione:** nessuna soluzione è insieme semplice, leggera e pronta all'uso → la sviluppiamo.

## 3. Decisioni prese

Candidate a diventare ADR durante `/grill-with-docs`.

1. **Stack: C# / .NET 10 + WinForms + NAudio.**
   Eseguibile nativo, avvio istantaneo, leggero. NAudio è la libreria audio di riferimento
   su .NET ed espone già sia la cattura del microfono (WASAPI) sia la cattura loopback.
   L'SDK .NET 10 è già installato sulla macchina. Scartato Python + tkinter (packaging con
   PyInstaller più pesante e lento all'avvio).
2. **Nessun driver virtuale.** Si usa il **loopback WASAPI** nativo di Windows per
   "ascoltare" il dispositivo di uscita. Niente Stereo Mix, niente VB-Cable.
3. **UI: una finestrella singola**, niente tray per ora.
   ```
   ┌─ Duettino ─────────────────────┐
   │ Ingresso: [Mic webcam       ▼] │
   │  ▮▮▮▮▮▯▯▯▯▯                    │
   │ Uscita:   [Casse Realtek    ▼] │
   │  ▮▮▮▯▯▯▯▯▯▯                    │
   │                                │
   │   [ ● Registra ]    00:12:34   │
   │ Salva in: Documenti\Registraz. │
   └────────────────────────────────┘
   ```
4. **Output: un singolo MP3**, 48 kHz stereo, 128 kbps (~1 MB/min, ~60 MB/ora).
5. **Le due voci mixate insieme** nello stesso audio (non canali separati, non file separati).
6. **Encoding MP3 tramite Media Foundation di Windows** (encoder MP3 di sistema, esposto
   da NAudio), quindi nessuna dipendenza nativa extra tipo LAME.
7. **Ingresso e Uscita liberi e indipendenti.** Qualsiasi dispositivo di acquisizione attivo
   come Ingresso e qualsiasi dispositivo di riproduzione attivo come Uscita, senza assumere
   che siano lo stesso apparecchio (cuffie USB, casse + microfono della webcam, ecc.).
8. **File di lavoro WAV durante la registrazione**, convertito in MP3 allo Stop.
   Motivo: resistenza ai crash. Se l'app o il PC si piantano, il WAV fino a quel punto
   resta recuperabile. Il prezzo è lo spazio temporaneo (~10 MB/min).
9. **UI scritta in codice, senza designer visuale WinForms.** La finestra è minuscola
   (due tendine, due indicatori, un pulsante, qualche etichetta): il layout in codice
   (es. `TableLayoutPanel`) è più leggibile, facile da modificare anche per un agente e
   non dipende dal designer. IDE di riferimento: **JetBrains Rider**, il cui designer
   WinForms sui progetti .NET moderni è meno affidabile di quello di Visual Studio.
   Build, test e publish passano tutti dalla CLI `dotnet`. Visual Studio è installato e
   resta disponibile se servisse (vedi §12 per il packaging MSIX).

## 4. Come funziona (pipeline)

```
 Ingresso ──(cattura WASAPI)───► buffer ─► converti formato ─┐
                                                             ├─► MIX ─► file WAV di lavoro
 Uscita ────(cattura loopback)─► buffer ─► converti formato ─┘              │
                                                                            ▼ (allo Stop)
                                                                   conversione → MP3
```

1. **Due catture in parallelo:** Ingresso scelto (cattura WASAPI) e Uscita scelta
   (cattura loopback).
2. **Normalizzazione del formato:** le due fonti arrivano quasi sempre in formati diversi
   (es. Ingresso mono 16 kHz, Uscita stereo 48 kHz, a volte 5.1/7.1 o array di mic a 4 canali).
   Entrambe vengono portate a 48 kHz stereo float (resampling + downmix/upmix dei canali).
3. **Mix:** somma delle due fonti, con clipping controllato nel passaggio a 16 bit.
4. **Scrittura:** un "motore" a ritmo di orologio preleva dal mix a tempo reale e scrive
   il WAV di lavoro. L'header del WAV viene aggiornato periodicamente (flush), così il file
   resta valido anche dopo un crash.
5. **Finalizzazione:** allo Stop il WAV viene convertito in MP3 in background (una call di
   un'ora può richiedere qualche secondo), poi il WAV viene cancellato. Se la conversione
   fallisce, il WAV resta.
6. **Indicatori di livello:** picco per ogni fonte, aggiornati ~10 volte al secondo.

## 5. Insidie tecniche note

- **Il loopback tace nel silenzio.** Quando nessun suono esce dal dispositivo di uscita,
  Windows non consegna pacchetti di silenzio: *non consegna niente*. Se il ritmo della
  registrazione fosse guidato dai dati in arrivo, le due fonti si sfaserebbero. Serve un
  orologio interno che guidi la scrittura e riempia di silenzio i buchi, con una piccola
  latenza di sicurezza (~200–300 ms) per assorbire il jitter.
- **Deriva degli orologi.** Ingresso e Uscita hanno clock hardware diversi (a maggior ragione
  se sono apparecchi diversi): in un'ora possono
  divergere di frazioni di secondo. I buffer devono tollerare lievi underrun (→ silenzio)
  e overflow (→ scarto), senza crescere all'infinito.
- **Cuffie Bluetooth e profilo "Hands-Free".** Su alcune configurazioni, quando il mic
  delle cuffie BT si attiva, l'audio passa a un endpoint diverso ("Headset / Hands-Free")
  rispetto a quello stereo. Se registro l'endpoint sbagliato, catturo silenzio. Windows 11
  tende a unificarli, ma va verificato con le cuffie reali. Da qui l'idea di un'opzione
  "Dispositivo di comunicazione predefinito" (vedi domande aperte).
- **Eco con le casse.** Se l'Uscita sono le casse, l'Ingresso (microfono) capta anche la voce
  degli altri che esce dagli altoparlanti: nel Mix quelle voci compaiono **due volte**, una dal
  loopback e una dal microfono con qualche decina di ms di ritardo, con effetto eco. L'app di
  call cancella l'eco solo sul *proprio* flusso, non su quello che catturiamo noi. Possibili
  risposte: accettarlo (resta comprensibile), usare la modalità "comunicazioni" di Windows
  per ottenere la cancellazione d'eco di sistema dove il driver la offre, oppure una
  cancellazione d'eco nostra (es. WebRTC AEC o SpeexDSP): **il loopback è esattamente il
  segnale di riferimento** che serve all'algoritmo. Vedi domande aperte.
- **L'app di call può usare un'uscita diversa da quella predefinita.** L'utente deve
  scegliere l'uscita che usa davvero Teams/Zoom.
- **Dispositivo scollegato durante la registrazione** (jack staccato, BT che cade): la
  cattura si interrompe con errore → l'app deve fermarsi in modo pulito, salvare ciò che
  ha e avvisare.
- **Formati "extensible".** Il formato di mix dei dispositivi è spesso dichiarato come
  *WaveFormatExtensible* (float 32 bit) e va riconosciuto e trattato correttamente.
- **Encoder MP3 di Windows:** accetta PCM 16 bit a 44,1/48 kHz, per cui il WAV di lavoro
  va scritto in quel formato.
- **Icona "microfono in uso".** Se si tenessero i livelli sempre attivi (anche senza
  registrare), Windows mostrerebbe di continuo l'indicatore del microfono. Decisione
  attuale: livelli attivi **solo durante la registrazione**.

## 6. Glossario proposto (seme per `GLOSSARY.md`)

- **Registrazione**: una sessione tra "Registra" e "Stop", che produce un file audio.
- **Ingresso**: il dispositivo di acquisizione scelto; di solito un microfono, ma può essere
  qualsiasi sorgente (line-in, cavo virtuale…). _Evitare_: input, microfono, sorgente mic.
- **Uscita**: il dispositivo di riproduzione scelto, di cui si cattura ciò che suona
  (cuffie, casse, HDMI…). _Evitare_: output, cuffie, speaker, sistema.
- **Cattura loopback**: la cattura di ciò che viene riprodotto su un'Uscita.
- **Mix**: l'unione di Ingresso e Uscita in un solo flusso audio.
- **File di lavoro**: il WAV scritto durante la Registrazione. _Evitare_: temp, buffer.
- **Finalizzazione**: la conversione del File di lavoro nel file MP3 definitivo.

## 7. Domande aperte (da affrontare con `/grill-with-docs`)

1. **Volumi relativi:** spesso la propria voce risulta molto più forte o più debole degli
   altri. Due cursori di guadagno (Ingresso / Uscita) sì o no? Eventuale normalizzazione
   automatica?
2. **Dispositivi all'avvio:** preselezionare i predefiniti di Windows o ricordare l'ultima
   scelta (file di impostazioni in `%AppData%`)? Aggiungere nelle tendine una voce
   "Predefinito comunicazioni" che segue Windows?
3. **Cartella e nome file:** proposta `Documenti\Registrazioni\Call_AAAA-MM-GG_HH-mm.mp3`,
   con cartella modificabile e pulsante "Apri cartella". Vogliamo poter dare un nome alla
   registrazione (es. "call cliente X") prima o dopo?
4. **Chiusura durante la registrazione:** conferma + salvataggio? Cosa succede con la
   Finalizzazione in corso?
5. **Recupero dopo crash:** all'avvio l'app cerca File di lavoro orfani e propone di
   finalizzarli?
6. **Pausa/ripresa:** serve?
7. **Distribuzione:** eseguibile single-file *framework-dependent* (piccolo, richiede il
   .NET 10 Desktop Runtime) oppure *self-contained* (nessun prerequisito, ~70+ MB)?
   E puntiamo al Microsoft Store già dalla v1 o in un secondo momento? (vedi §12)
8. **Lingua UI:** solo italiano o anche inglese?
9. **Eco con le casse** (vedi §5): nella v1 lo accettiamo, avvisiamo l'utente ("con le casse
   si può sentire un'eco: meglio le cuffie"), proviamo la cancellazione d'eco di sistema o
   ne implementiamo una nostra usando il loopback come riferimento?

## 8. Fuori perimetro (v1)

- Icona nella tray e scorciatoie globali da tastiera.
- Avvio automatico quando parte Teams/Zoom.
- Trascrizione, riassunti, AI.
- Cattura per singola applicazione (per processo).
- Canali o file separati per le due voci (possibile evoluzione futura: è un cambiamento
  piccolo nella fase di mix).
- Registrazione video/schermo.
- Supporto a sistemi diversi da Windows 10/11.

## 9. Test e verifica

**Seam di test proposto:** separare nettamente il livello "dispositivi" (enumerazione e
catture WASAPI, sottile e non testabile in automatico) dal **motore di registrazione**
(normalizzazione formati, mix, orologio, scrittura). Il motore riceve due flussi audio
astratti e un orologio iniettabile, così si può testare con segnali sintetici:

- formati diversi (mono/stereo/multicanale, 16/44,1/48 kHz, PCM/float) → output coerente
  a 48 kHz stereo;
- una fonte che tace (nessun dato) → silenzio nel file, durata corretta, niente sfasamento;
- deriva simulata tra le fonti su durate lunghe → nessuna crescita illimitata dei buffer;
- somma oltre fondo scala → clipping, niente wrap-around;
- file di lavoro valido anche se il processo si interrompe dopo un flush.

**Verifica manuale** (checklist con hardware reale):

- call reale con cuffie cablate e con cuffie Bluetooth (verifica dell'endpoint Hands-Free);
- call reale con le **casse** del PC e un microfono separato (entità dell'eco);
- Ingresso e Uscita su apparecchi diversi (es. mic della webcam + cuffie USB);
- call lunga (≥ 1 ora): sincronia delle voci alla fine, dimensione del file, tempo di Finalizzazione;
- cuffie staccate a metà registrazione;
- chiusura dell'app durante la registrazione.

## 10. Come procedere

0. **Riservare il nome "Duettino" sul Microsoft Store** (Partner Center, gratuito, vedi §12)
   prima che lo prenda qualcun altro.
1. ~~**Creare il repo**~~ fatto: `fakkio/duettino`, con skill, `AGENTS.md` e
   `docs/agents/` già configurati (issue tracker GitHub, etichette di triage,
   `GLOSSARY.md` + `docs/adr/`).
2. **`/grill-with-docs`** su questo brief: chiudere le domande aperte (§7), produrre
   `GLOSSARY.md` dal glossario (§6) e le ADR dalle decisioni (§3).
3. **`/prototype`** (spike usa-e-getta, consigliato): console app che registra 30 secondi
   di Ingresso + loopback dell'Uscita in WAV. Serve a verificare subito sui dispositivi reali
   le insidie più rischiose: silenzio del loopback, endpoint Bluetooth ed eco con le casse.
4. **`/to-spec`**: spec della v1 pubblicata come issue.
5. **`/to-tickets`**: spezzarla in ticket verticali. Ordine suggerito:
   1. scheletro WinForms + elenco dispositivi;
   2. motore di registrazione (normalizzazione + mix + orologio) con i test;
   3. registrazione su WAV end-to-end;
   4. Finalizzazione in MP3;
   5. indicatori di livello e timer;
   6. gestione errori (dispositivo scollegato, chiusura, recupero);
   7. impostazioni persistenti e packaging.
6. **`/implement`** / **`/tdd`** ticket per ticket.
7. **Rilascio pubblico** (vedi §11): prima release con binario scaricabile su GitHub,
   poi winget, poi la promozione.

## 11. Dove farlo conoscere

Il pubblico è chi ha il nostro stesso problema e non trova risposte. La regola è
**rispondere a chi cerca, non fare spam**: dichiarare sempre che l'app è nostra e dare
prima la spiegazione, poi il link.

**Prerequisiti prima di promuovere:**
- una release su GitHub con l'`.exe` pronto da scaricare (niente "compila da solo":
  è proprio il difetto di AudioCapture);
- un README con una **spiegazione chiara e dettagliata** (cosa fa in una frase: "registra
  quello che dici e quello che senti"; poi come funziona, per chi è, gli usi possibili oltre
  alle call), una GIF di 10 secondi, "Download" ben visibile e una sezione FAQ
  ("perché non Stereo Mix?", "funziona con le cuffie Bluetooth?", "e con le casse?");
- per l'angolo SEO, le frasi che la gente cerca davvero nel README: *record Teams call
  with headphones*, *record mic and speakers at the same time*, *Audio Hijack for Windows*,
  *OBS alternative for audio only*.

**Dove si fanno già queste domande (rispondere lì):**
- SuperUser / Stack Exchange: domande su *record microphone and system audio simultaneously*;
- Microsoft Q&A e Tech Community (thread su registrazione di Teams e Stereo Mix mancante);
- Tom's Guide forum (es. [questo thread](https://forums.tomsguide.com/threads/recording-software-that-can-record-from-2-sound-outputs-plus-microphone-are-there-any.342760/post-1500656));
- Quora (es. [questa domanda](https://www.quora.com/How-do-I-record-internal-and-external-audio-simultaneously-on-a-PC));
- forum Adobe Audition sul tema "stereo mix + microfono";
- Reddit: cercare thread esistenti in r/software, r/Windows11, r/techsupport, r/MicrosoftTeams, r/Zoom.

**Siti di alternative:**
- **AlternativeTo**: pubblicare Duettino come alternativa a OBS Studio, Audacity,
  Xbox Game Bar e soprattutto **Audio Hijack**. Audio Hijack esiste solo per Mac e in molti
  cercano l'equivalente Windows: è l'angolo più forte.

**Microsoft Store** (vedi §12): la scheda dello Store è anche una vetrina ricercabile
("call recorder", "record Teams").

**Installazione con un comando:**
- **winget** (PR su `microsoft/winget-pkgs`): è il canale più importante, perché rende
  `winget install duettino` possibile e dà credibilità;
- **Scoop** (bucket `extras`) e **Chocolatey**.

**GitHub:**
- topic: `call-recorder`, `audio-recorder`, `wasapi`, `wasapi-loopback`, `naudio`,
  `teams`, `zoom`, `windows`, `dotnet`, `winforms`;
- proporla nelle liste curate (awesome-windows, awesome-dotnet e liste di software audio open source).

**Community (post di lancio, una volta sola per posto):**
- Reddit: r/software, r/opensource, r/Windows11, r/podcasting, r/csharp e r/dotnet
  (qui l'angolo tecnico), r/ItalyInformatica;
- Hacker News "Show HN";
- Product Hunt.

**Siti di freeware** (inserimento gratuito, portano traffico da Google):
- Softpedia, MajorGeeks, Neowin (software news), FileHorse.

**Contenuti tecnici** (portano visite nel tempo):
- articolo su dev.to / Medium / Hashnode: *"Windows WASAPI loopback goes silent when nothing
  plays, and how to mix it with the mic without drift"*. È il problema tecnico non ovvio
  del progetto e porta visite naturali da sviluppatori;
- eventuale risposta su Stack Overflow alle domande su NAudio loopback + mixing, con
  link al codice.

## 12. Pubblicazione sul Microsoft Store

**Sì, è pubblicabile.** Lo Store accetta app desktop Win32/.NET come Duettino in due modi:

| | **Pacchetto MSIX** (consigliato) | **Installer EXE/MSI** |
|---|---|---|
| Firma del codice | **La fa lo Store gratis** | Serve un certificato di firma del codice a pagamento (centinaia di €/anno) |
| Dove sta il file | Caricato sullo Store | Ospitato da noi (URL HTTPS con versione) |
| Aggiornamenti | Automatici via Store | Gestiti da noi |
| Installazione/disinstallazione | Pulita, isolata | Dipende dall'installer |

**Scelta: MSIX.** Evita il costo del certificato ed elimina anche l'avviso SmartScreen
("app non riconosciuta") che invece colpisce l'`.exe` non firmato scaricato da GitHub.

**Cosa comporta:**
- **Account sviluppatore:** gratuito per i privati (da settembre 2024 Microsoft ha tolto
  la quota di iscrizione per gli sviluppatori individuali). Da verificare al momento
  dell'iscrizione.
- **Riservare il nome** "Duettino" subito in Partner Center: la prenotazione è gratuita.
- **Pacchetto self-contained:** sullo Store non c'è un pacchetto del runtime .NET 10 Desktop
  da cui dipendere, quindi il pacchetto deve includerlo (~70+ MB, accettabile).
- **Capability `microphone`** dichiarata nel manifest. Da app impacchettata, Duettino
  compare in *Impostazioni → Privacy → Microfono* con un proprio interruttore: l'app deve
  gestire bene il caso "accesso al microfono negato" con un messaggio chiaro. La cattura
  loopback dell'Uscita non richiede capability.
- **Full trust** (`runFullTrust`): normale per le app desktop impacchettate; WASAPI e
  loopback funzionano senza cambiamenti.
- **Informativa sulla privacy** obbligatoria (l'app usa il microfono): basta una pagina
  semplice (es. GitHub Pages o un file nel repo) che dica che tutto resta in locale,
  niente rete, niente telemetria.
- **Questionario di classificazione per età**, screenshot e descrizione per la scheda.

**Come produrre l'MSIX** (decisione da prendere, candidata ADR):
- **Progetto di packaging di Visual Studio** (`.wapproj`): il più semplice e guidato, ma
  è specifico di Visual Studio e non si compila con la sola CLI `dotnet`. È il caso in cui
  Visual Studio torna utile.
- **`makeappx` del Windows SDK + manifest scritto a mano**: si scrive una volta, è
  scriptabile e gira anche in GitHub Actions. È l'opzione coerente con "tutto da CLI" e
  con il flusso agentico.
- Proposta: `makeappx` scriptato; Visual Studio solo come ripiego.

**Doppio canale:**
- **Store**: per gli utenti normali (MSIX, aggiornamenti automatici, niente avvisi di sicurezza);
- **GitHub Releases + winget**: `.exe` portabile per chi non usa lo Store. winget può anche
  installare direttamente dalla sorgente `msstore`.

**Quando:** non necessariamente dalla v1. Proposta: v1 su GitHub Releases, Store dalla
prima versione "stabile" dopo un po' di uso reale. Il nome però va riservato subito.

## Nota legale

In Italia registrare una conversazione a cui si partecipa è in genere lecito per uso
personale; diffonderla è un'altra questione. L'app non deve fare nulla in merito, ma
è bene saperlo.
