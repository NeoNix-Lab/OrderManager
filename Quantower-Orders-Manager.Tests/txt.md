Per orientarsi fra le piattaforme concorrenti e capire dove c’è spazio per prodotti cTrader, puoi procedere così:

1. **MQL5 Market (MetaTrader 5) – sezione “Expert Advisors”**: accedendo a `https://www.mql5.com/en/market/mt5/expert` si trovano i “Trading Robots for MetaTrader 5”. Nella colonna a sinistra la piattaforma mostra i tipi di Expert Advisor più venduti: *Martingale*, *Grid*, *Arbitrage*, *Hedging*, *Scalping*, *News*, *Trend*, *Level trading*, *Neural networks* e *Multicurrency*. Queste categorie rappresentano ciò che vende di più sul marketplace MT5. Si notano molte strategie ad alta frequenza (scalping, grid/martingala), robot basati su notizie, trading su livelli e le prime soluzioni di rete neurale.

2. **MQL5 Market – sezione “Indicators”**: la pagina dedicata agli indicatori MT5 mostra un elenco di categorie simili (trend, oscillatori, canali/levels, multi‑timeframe, multicurrency ecc.). Anche se il server a volte restituisce un errore (502), esplorando la sezione indicatori si vede che i più scaricati sono indicatori di trend, oscillatori, pattern di supporto/resistenza e volumi (order flow), oltre a pacchetti multi‑timeframe e multi‑currency.

3. **TradingView – sezione “Scripts”**: nella pagina `https://www.tradingview.com/scripts/editors-picks/` la comunità mette in evidenza gli script più apprezzati. Le scelte recenti includono indicatori basati su *Finite‑State Machines* e strumenti di *Trading Activity / Volume*, che misurano l’attività di mercato e il volume per identificare zone di interesse. Altre categorie popolari (visibili dalla scheda “Popular”) includono indicatori di trend cloud, zone di probabilità di mean‑reversion, strumenti ICT/Smart‑Money‑Concepts, profili di volume avanzati e order‑flow footprints.

4. **Cerca le stesse parole chiave sullo Store cTrader**: per verificare se cTrader dispone delle stesse categorie, cerca nel cTrader Store termini come *“order flow”*, *“footprint”*, *“ICT”*, *“neural network”* o *“hedging EA”*. Se non appaiono risultati, o se i prodotti hanno poche recensioni/download, probabilmente c’è un gap di mercato.

5. **Confronta domanda indiretta**: usa forum o social (Reddit r/algotrading, Telegram, Discord) per vedere se gli utenti cTrader chiedono funzioni come order‑flow, indicatori ICT, dashboard di risk‑management o bot ML; se ricevi pochissime risposte, è un segnale che l’offerta è scarsa.

### Mappa delle opportunità

| Categoria/tema                           | Popolarità MT5                                                                 | Popolarità TradingView                                             | Stato su cTrader    | Opportunità |
| ---------------------------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------------ | ------------------- | ----------- |
| **Order flow / Volume profile**          | Categoria “Volume” / molti robot scalping e volume; pochi indicatori footprint | Indicatori order‑flow, volume profile, Trading Activity Index      | Quasi assente       | 🔥 forte    |
| **Machine‑learning / Neural nets**       | Categoria “Neural networks” presente ma piccola                                | Pochi script (Finite‑State Machine, AI‑trend cloud)                | Quasi assente       | 🔥 forte    |
| **Market structure / ICT (Smart Money)** | Non classificato (presenti trend e level trading)                              | Molto popolare su TradingView (ICT, supply/demand, breaker blocks) | Assente             | 🔥 forte    |
| **Risk‑management tools**                | Presenti come “Hedging”, “Grid”, “Martingale”                                  | Esistono script per money management e position sizing             | Solo tool basici    | Medio       |
| **Portfolio / Correlation analysis**     | Quasi inesistente (focus su singoli simboli)                                   | Alcuni script multi‑asset / correlation heatmap                    | Assente             | Medio       |
| **Scalping / High‑frequency bots**       | Molto popolare (Scalping, Grid, Martingale)                                    | Meno popolare ma presente                                          | Saturato su cTrader | Basso       |

### Consigli pratici

* Usa gli elenchi “Popular” e “Editors’ Picks” dei competitor per stimare cosa vende: controlla il numero di recensioni e i prezzi per farti un’idea delle dimensioni del mercato.
* Crea un foglio di calcolo per registrare nome prodotto, categoria, prezzo e recensioni; puoi farlo manualmente o con uno script di scraping (rispettando i ToS).
* Per ogni categoria identificata, verifica nel cTrader Store se esistono prodotti simili. Se trovi un vuoto (es. nessun footprint/order‑flow indicator) o solo versioni gratuite con poche recensioni, potresti creare un plugin/EA per colmare quel gap.
* Paragona anche i prezzi: se su MQL5 gli indicatori volume profile si vendono a 50–100 USD e su cTrader non esistono, puoi posizionarti in quella fascia.

Con questo metodo hai una guida concreta per individuare cosa è “hot” sui marketplace più grandi e capire dove cTrader è indietro.
