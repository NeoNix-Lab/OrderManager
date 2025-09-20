Ecco la traduzione in italiano del tuo testo, resa chiara, precisa e tecnicamente fedele—rispettando le buone pratiche per i testi tecnici: frasi semplici, terminologia coerente, stile attivo e senza giri di parole ([ustranslation.com][1], [accelingo.com][2]).

---

## Sinossi

### Appunti

* **(1)** Indica gli elementi che ritieni **più critici**.

Faccio trading di futures su ES e NQ sui timeframe da 1, 3 e 5 minuti. Utilizzo Rithmic e DxFeed per i dati. Ho tradato manualmente per un po', ma ora sono assegnato a un nuovo progetto al lavoro e non avrò più tempo per farlo durante l'orario di mercato. Ho molte variabili, non le uso tutte insieme, ma voglio riuscire a inglobare tutto ciò che mi serve per tradare in mercati diversi. Idealmente, un giorno vorrei creare 4-5 strategie diverse su asset non correlati.

#### Condizioni di ingresso/uscita:

1. **Critico**

   * RVOL misurato su HMA e smoothed (livello medio).
   * Volume delta rispetto al prezzo.
   * Strength relativo del volume delta.
   * HMA personalizzata (lunghezza divisa per ATR — reagisce velocemente con alta volatilità).
   * Rapporto volume delta / volume.
   * Volume delta che si muove in direzione opposta al prezzo.
     Ne uso solo 2-3 a volta, ma voglio poter cambiare i parametri in base al regime di mercato.

2. **Critico**
   Tre slot temporali impostabili.
   Se tutti i tre sono disattivati → tradiamo in orario standard CME.
   Se almeno uno è attivo → tradiamo solo in quello.

3. **Critico**
   Lista personalizzabile dei parametri da usare per gli ingressi.
   Servono almeno *x* di quelli selezionati per entrare, e bisogna essere nell'intervallo temporale attivo.

4. Uscita:

   * Lista separata (opzionale, ma utile) con parametri da usare per uscire.
   * Almeno *x* di quelli selezionati devono essere veri per uscire.
   * Se esci dall’orario selezionato, esci anche dalla posizione.
   * Se nessuno slot è selezionato, uscita automatica alla chiusura di mercato: 17–18 ogni giorno, venerdì 17 -> domenica 18.

5. **Critico – Stop loss**
   Basato sui massimi/minimi della candela precedente + ATR (in tick).
   C’è una distanza minima e massima consentita; se il nuovo stop è fuori da quell'intervallo, si usa il limite (min o max).

6. **Critico – Take profit**
   In base a: massimi/minimi sessione giorno precedente, overnight, pre-market/mattinale.
   Se il TP scelto è troppo vicino, si passa al secondo TP più vicino o alternativo.
   Si usa TP alternativo anche se il prezzo è al di fuori di tutti gli high/low.

7. **Critico**
   Entrate/uscite (non TP/SL) solo alla chiusura della candela. Se il segnale arriva, piazzi subito un ordine *Market* (tipo DAY se serve specificarne la durata).
   SL e TP vengono chiusi immediatamente.
   Possono esserci inversioni nello stesso candle: se hai long e arriva un segnale short → chiudi long e subito apri short.

8. **Slippage** basato su `ATR in tick × [0.000 – 2.000]`.

9. **Order size**: numero di contratti per ordine.

10. (Opzionale) **Massima perdita giornaliera**: sistema stop-trading del giorno se si tocca. Si riparte solo quando ricominci lo slot attivo, o alla riapertura del mercato se nessuno è selezionato.

11. (Nice to have) **Order stacking**: di default entri solo una volta per segnale, ma se hai stacking attivo, puoi entrare fino a un numero massimo di volte prima di uscire.
    Esempio: stacking=1 → una entry, poi niente finché non chiudi o inverti. stacking=2 → puoi entrare due volte consecutivamente. Alla chiusura/inversione chiudi tutto.

12. **Logging**: fondamentale per verificare il funzionamento, dettagliato alla fine del documento.

---

### Note tecniche sul calcolo delle variabili

* `Rvol = volume / volume medio`

* `RvolShort`, `RvolLong`: volume / media volume su finestra definita dall’utente

* `HMA`: media mobile di Hull su input utente

* `RvolSmoothed = (RvolShort + RvolLong + HMA) / 3`

* `IsRising = RvolSmoothed > RvolSmoothed[t-1]`

* `IsFalling = RvolSmoothed < RvolSmoothed[t-1]`

* `RvolNormalized = RvolSmoothed / ATR`

* `RvolDifference = |RvolNormalized – RvolNormalized[t-1]|`

* `RvolOkay = RvolDifference > soglia utente`

* `RvolLongOkay = RvolOkay + IsRising`

* `RvolShortOkay = RvolOkay + IsFalling`

* **Price-to-Volume Delta Ratio**

  * `priceMove = |open – close|`
  * `avgPriceMove = media(priceMove)` su finestra input
  * `VD = |buyVolume – sellVolume|`
  * `avgVD = media(VD)` su finestra input
  * `APAVD = avgPriceMove / avgVD`
  * `CPVD = priceMove / VD`
  * `PVDstrong = CPVD > APAVD × soglia utente`
  * `PVDLongOkay = PVDstrong + accordo con VD`
  * `PVDShortOkay = PVDstrong + accordo con VD`

* **Strength del Volume Delta**

  * `VDpositive = VD > 0`, `VDnegative = VD < 0`
  * `VDstrong = VD > avgVD × soglia`
  * `VDLongOkay = VDstrong + VDpositive`
  * `VDShortOkay = VDstrong + VDnegative`

* **HMA personalizzata**: avere `HMA length / ATR`

  * `HmaLongOkay = close > customHMA`
  * `HmaShortOkay = close < customHMA`

* **Rapporto VolumeDelta / Volume**

  * `VDtVstrong = (VD/volume) > (avgVD/avgVolume) × soglia`
  * `VDtVLongOkay = VDtVstrong + VDpositive`
  * `VDtVShortOkay = VDtVstrong + VDnegative`

* **Divergenza VD - prezzo**

  * `VD positive/negative` come sopra
  * `priceDrop = close – open < 0`, `priceRise = close – open > 0`
  * `VDShortOkay = priceRise + VDnegative`
  * `VDLongOkay = priceDrop + VDpositive`

* **Filtri temporali** (in EST, con DST):
  3 slot attivabili, con start e end in minuti dall’inizio del giorno.

---

### Regole operative

#### Ingressi

* `orderSize = num. contratti`
* Stato posizione: -1, 0, +1 per evitare operazioni duplicate
* Stacking: se attivo, puoi entrare fino al limite stabilito

Parametri abilitabili (in ingresso):

* RVOL, VDPS, VDstrong, HMA personalizzata, VDtV, VDP \_
* Serve che almeno *x* di quelli attivi siano veri per entrare long/short.

#### Chiusure

Parametri abilitabili:

* Come ingresso; serve che almeno *y* di quelli attivi siano veri per uscire.
* Si chiude posizione o si esce se fuori slot orario.
* Stato ritorna a 0.

#### Stop Loss

* Usa massimo/minimo della candela precedente ± ATR×fattore utente
* Rispetta distanze min/max (se fuori range, limita)
* Si aggiorna ogni tick e ogni candela, rimuovendo lo stop precedente
* Se stop chiude la posizione, `positionStatus = 0`

#### Take Profit - livelli chiave

* Monitorare massimi/minimi:

  * Sessione giorno precedente (9:30–17:00 EST)
  * Overnight (18:00 giorno prima – 4:00 giorno corrente)
  * Pre-market (4:00–9:29 giorno corrente)
* Scegli i 3 massimi e minimi più recenti su un rolling di 24h (barra= timeframe selected)
* Buy → TP = minimo high > price più vicino
  Sell → TP = minimo low < price più vicino
* Se TP troppo vicino (< distanza minima), usa `altTP`
* Una volta settato al momento dell’entry, non cambia
* Se chiude posizione, `positionStatus = 0`
* Weekend/ferie → fallback all’ultimo giorno con dati

#### Log (debug)

* Ogni 3 candele: log parametri attuali, livello TP più vicino, SL precedente con ATR
* Log all’ingresso: buy/sell, SL e TP settati
* Log quando SL si aggiorna
* Errori
* Chiusura posizione con motivo (es. “chiuso buy, stop loss a 23451.25”)

---

Se vuoi, continuo ad adattare lo stile secondo il tuo contesto (es. C#, MAUI, ecc.). Fammi sapere!

[1]: https://ustranslation.com/blog/best-practices-for-translating-technical-documents/?utm_source=chatgpt.com "7 Best Practices for Translating Technical Documents"
[2]: https://www.accelingo.com/technical-translation-best-practices/?utm_source=chatgpt.com "Technical Translation: Best Practices to Avoid Losing Quality"
