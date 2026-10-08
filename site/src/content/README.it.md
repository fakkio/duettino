<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/web/icon-dark.svg">
  <img src="assets/web/icon-light.svg" alt="" width="96" height="96">
</picture>

# Duettino

**Registra quello che dici e quello che senti.**

Con le cuffie, l'altro lato di una call non esce mai dal computer, quindi un registratore sulla scrivania sente solo te. Duettino è un piccolo programma gratuito per Windows che registra insieme il tuo microfono e ciò che suona nelle cuffie, in un unico MP3.

Teams e Zoom possono registrare una riunione da soli, ma solo se l'organizzatore o la tua azienda lo permette, e le chiamate di WhatsApp o Discord non si possono registrare. Duettino funziona con tutte, perché registra l'audio del tuo PC, non la chiamata.

![La finestra di Duettino (qui in inglese): Ingresso e Uscita scelti, Registra premuto, i due indicatori e il timer in movimento, poi Stop e l'MP3 salvato.](assets/web/demo.gif)

## Scarica

**[Scarica Duettino.exe](https://github.com/fakkio/duettino/releases/latest/download/Duettino.exe)** · [Tutte le versioni](https://github.com/fakkio/duettino/releases)

0.1.0 beta · ~55 MB · Windows 10/11 a 64 bit · niente da installare, .NET non serve

È una beta, provata sul mio hardware: per favore [segnala cosa non va](https://github.com/fakkio/duettino/issues).

> [!NOTE]
> La prima volta Windows potrebbe dire "Windows ha protetto il PC", perché Duettino è nuovo e Windows ancora non lo conosce: clicca su **Ulteriori informazioni**, poi su **Esegui comunque**. Le [domande frequenti](#domande-frequenti) spiegano il motivo e come controllare che il file sia quello che ho pubblicato.

## Come registrare una call Teams o Zoom con le cuffie

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="assets/web/how-it-works-dark.svg">
  <img src="assets/web/how-it-works-light.svg" alt="Quello che dici (l'Ingresso: il tuo microfono) e quello che senti (l'Uscita: le tue cuffie) si uniscono in un solo MP3 in Documenti\Duettino.">
</picture>

1. **Scegli il microfono come Ingresso e le cuffie come Uscita.** Se ne hai più di un paio, scegli quello su cui suona l'app della call. Non devono essere un unico auricolare: va bene anche un microfono USB con le casse del portatile.
2. **Premi Registra.** Un indicatore per lato si muove mentre parli e ascolti, e parte un timer.
3. **Premi Stop.** Dopo qualche secondo l'MP3 è in `Documenti\Duettino`, con il nome dell'ora in cui hai iniziato, come `Duettino_2026-10-07_15-30-00.mp3`. Un'ora pesa circa 60 MB.

Non c'è altro da preparare: nessun plugin, nessun bot che entra nella call, niente da cambiare nell'app della chiamata.

## Non solo call

Duettino nasce per le call, ma registra tutto ciò che il computer riproduce, insieme alla tua voce. Senza montare uno studio:

- **Webinar e lezioni online**: chi parla, e le domande che hai fatto tu.
- **Video commentati**: la tua voce sopra il video o la diretta che stai guardando.
- **Cantare o suonare su una base musicale**: registra la tua voce insieme alla base, poi ascolta come suonate insieme.
- **Podcast a distanza**: il tuo ospite dalla chiamata e tu dal microfono, in un solo file.

## Cosa fa per te

- **Entrambi i lati allo stesso volume.** Un microfono basso non sparisce sotto una call forte, e niente distorce quando qualcuno alza la voce. Quando taci, il fruscio della tua stanza non viene alzato.
- **Sincronizzato per ore.** La tua voce e la call restano allineate nelle chiamate lunghe, nei silenzi e nei piccoli blocchi del sistema, anche se due dispositivi non vanno mai alla stessa velocità.
- **Dispositivi che vanno e vengono.** Stacca il jack delle cuffie o rimetti gli auricolari nella custodia durante una call: Duettino passa al dispositivo predefinito di Windows, e torna al tuo quando riappare. Se non resta nessun dispositivo, registra il silenzio invece di fermarsi.
- **Niente perso in caso di crash.** Se Duettino o Windows si chiudono durante la registrazione, al prossimo avvio ti propone di recuperare quanto registrato. Se succede qualcosa che Duettino non sa risolvere, si ferma, tiene quello che ha e ti dice perché.
- **Chiusura sicura.** Chiudere durante una registrazione chiede prima "Fermare e salvare?" e aspetta che il file sia scritto.
- **Niente driver, niente Missaggio stereo.** Duettino usa una funzione già presente in Windows: nessun cavo virtuale da installare, niente da cambiare nelle impostazioni audio, e va bene qualsiasi microfono, cuffia o cassa.
- **Ricorda le tue scelte.** Microfono, cuffie e cartella vengono ricordati. Se uno non è collegato, Duettino usa quello predefinito di Windows e te lo dice.
- **Ti dice cosa non va.** Se Windows blocca il microfono, Duettino ti dice dove consentirlo e comincia a registrarlo appena lo fai.
- **Italiano e inglese.** La finestra parla italiano su un Windows in italiano, inglese altrove.

## Domande frequenti

<details>
<summary>Perché non usare il Missaggio stereo?</summary>

Il Missaggio stereo esiste solo su alcune schede audio, su Windows 11 spesso manca o è disattivato ("non funziona", "non appare"), e cattura solo ciò che suona sulla sua scheda, non le tue cuffie USB o Bluetooth. Ti servirebbe comunque un secondo programma per il microfono, e un modo per allineare i due file dopo.

Duettino registra qualsiasi cuffia o cassa, USB e Bluetooth comprese, e il tuo microfono insieme, sincronizzati.

</details>

<details>
<summary>Funziona con le cuffie Bluetooth?</summary>

Sì, con un limite che viene dal Bluetooth stesso. Mentre il microfono dell'auricolare è in uso, da Duettino o dall'app della call, Windows mette l'auricolare in una modalità "vivavoce" che suona come un vecchio telefono: ovattata, senza alti. Tutto ciò che senti nell'auricolare suona così, non solo la call, e così anche la registrazione. Ogni cambio di modalità lascia inoltre un secondo o due di silenzio, nelle tue orecchie e nella registrazione.

Per la qualità piena, usa un altro microfono, come quello del portatile o uno USB, sia in Duettino sia nell'app della call.

</details>

<details>
<summary>Posso registrare audio del PC e microfono contemporaneamente senza cuffie? Ci sarà eco?</summary>

Sì, ma il microfono sente anche le casse, quindi l'altra persona finisce nella registrazione due volte, a circa un decimo di secondo di distanza, come un'eco. Le app delle call eliminano quell'eco nella chiamata, non in ciò che registra Duettino. Quanto se ne sente dipende dal microfono: quello integrato di un portatile ne filtra spesso la maggior parte, uno separato può non filtrarla affatto. Con le cuffie non succede.

</details>

<details>
<summary>Carica qualcosa online?</summary>

No. Duettino registra solo sul tuo PC e non si collega mai alla rete: nessun account, nessuna telemetria, nessuna analisi d'uso. Le registrazioni restano nella cartella che scegli, e l'unico altro file che scrive è un piccolo file di impostazioni (i tuoi dispositivi e la cartella) in `%AppData%\Duettino`.

</details>

<details>
<summary>Windows dice che ha protetto il PC. È sicuro?</summary>

È SmartScreen. Compare per i programmi che non sono firmati con un certificato a pagamento e che non sono ancora stati scaricati da molte persone. Clicca su **Ulteriori informazioni**, poi su **Esegui comunque**.

Per controllare che il file sia quello che ho pubblicato, confronta lo SHA-256 nelle note della versione con quello che stampa `Get-FileHash Duettino.exe` in PowerShell. Tutto il codice sorgente è qui, e puoi [compilarlo da solo](#compilare-dal-sorgente).

</details>

<details>
<summary>Su Windows N ho ottenuto un WAV invece dell'MP3: perché?</summary>

Le edizioni Windows N non includono l'encoder MP3 che usa Duettino. Allora Duettino salva un file WAV, più grande ma con lo stesso suono, e ti dice come avere l'MP3: installa il Media Feature Pack da *Impostazioni › App › Funzionalità facoltative › Aggiungi una funzionalità*.

</details>

<details>
<summary>È legale registrare una call?</summary>

Dipende da dove siete tu e le altre persone. La regola semplice: avvisa gli altri che stai registrando. A differenza della registrazione di Teams o Zoom, Duettino non avvisa nessuno, quindi tocca a te. Questo non è un parere legale: controlla la legge del tuo paese.

</details>

<details>
<summary>È Audio Hijack per Windows, o un'alternativa a OBS per registrare solo audio? Come si confronta con Audacity?</summary>

- **OBS Studio** può registrare solo audio, dopo un po' di configurazione, ma è pensato per video e streaming.
- **Audacity** registra da un solo dispositivo audio alla volta, quindi microfono e audio del computer vanno prima uniti in uno, con il Missaggio stereo o un cavo virtuale.
- **Audio Hijack** fa questo e molto altro, ma solo su Mac.

Duettino fa solo questo lavoro: due dispositivi, un pulsante, un MP3.

</details>

## Compilare dal sorgente

Ti servono Windows e l'[SDK di .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0).

```
git clone https://github.com/fakkio/duettino.git
cd duettino
dotnet publish src/Duettino -p:PublishProfile=win-x64
```

L'eseguibile viene scritto in `src/Duettino/bin/publish/win-x64/`.

## Come è fatto

Duettino è progettato, provato su hardware reale e rivisto da me, [Fabio Lazzaroni](https://fabiolazzaroni.dev). Il codice è scritto con agenti di programmazione AI (Claude Code). Ogni decisione, con le alternative scartate, è registrata in [`docs/adr`](docs/adr). Il flusso di lavoro (mettere sotto torchio il brief, ADR, specifica → ticket, TDD) si basa sulle [skill di Matt Pocock](https://github.com/mattpocock/skills).

## Licenza

[MIT](LICENSE). Usalo, modificalo, condividilo.
