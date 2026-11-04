# OpenSubtitles v2024 coverage by language, English pair

Measured with `scan.py`: for each language, a 20 MB prefix of the
English-<lang> TMX stream is filtered with the same acceptance
rules as `src/Concordance.cs`, then the surviving share is applied
to the full v2024 alignment size reported by the OPUS API.
"In a 100k pack" is how many usable pairs a 100,000-pair pack
would actually hold; a 2M pack holds at most 2,000,000 and scales
the same way. Rank is by usable pairs, not by raw size.

| # | Language | aligned pairs | accepted | usable pairs | in a 100k pack | in a 2M pack | tier |
| ---: | --- | ---: | ---: | ---: | ---: | ---: | --- |
| 1 | Portuguese (Brazil) | 89,882,348 | 95.6% | 85,893,738 | 95,562 | 1,911,248 | fills a 2M pack |
| 2 | Spanish | 80,969,856 | 95.4% | 77,235,913 | 95,388 | 1,907,769 | fills a 2M pack |
| 3 | Romanian | 78,530,275 | 95.7% | 75,125,244 | 95,664 | 1,913,281 | fills a 2M pack |
| 4 | Arabic | 74,831,563 | 94.8% | 70,933,801 | 94,791 | 1,895,825 | fills a 2M pack |
| 5 | French | 67,220,420 | 95.7% | 64,360,205 | 95,745 | 1,914,900 | fills a 2M pack |
| 6 | Dutch | 63,448,633 | 95.9% | 60,858,491 | 95,917 | 1,918,354 | fills a 2M pack |
| 7 | Polish | 61,030,201 | 95.1% | 58,030,880 | 95,085 | 1,901,710 | fills a 2M pack |
| 8 | Turkish | 57,422,883 | 95.7% | 54,937,524 | 95,671 | 1,913,436 | fills a 2M pack |
| 9 | Italian | 57,404,756 | 95.2% | 54,626,389 | 95,160 | 1,903,200 | fills a 2M pack |
| 10 | German | 53,072,239 | 95.7% | 50,770,721 | 95,663 | 1,913,268 | fills a 2M pack |
| 11 | Russian | 49,593,611 | 94.5% | 46,890,129 | 94,548 | 1,890,974 | fills a 2M pack |
| 12 | Hebrew | 46,736,828 | 94.8% | 44,288,800 | 94,762 | 1,895,242 | fills a 2M pack |
| 13 | Vietnamese | 30,541,115 | 96.7% | 29,544,519 | 96,736 | 1,934,737 | fills a 2M pack |
| 14 | Korean | 26,297,116 | 93.7% | 24,633,797 | 93,674 | 1,873,498 | fills a 2M pack |
| 15 | Chinese (Simplified) | 19,629,206 | 96.1% | 18,856,886 | 96,065 | 1,921,309 | fills a 2M pack |
| 16 | Chinese (Traditional) | 16,290,630 | 95.4% | 15,547,563 | 95,438 | 1,908,773 | fills a 2M pack |
| 17 | Ukrainian | 8,735,137 | 93.6% | 8,179,487 | 93,638 | 1,872,778 | fills a 2M pack |
| 18 | Hindi | 2,529,437 | 87.0% | 2,199,853 | 86,970 | 1,739,401 | fills a 2M pack |
| 19 | Japanese | 1,908,317 | 92.5% | 1,764,880 | 92,483 | 1,764,880 | fills a 100k pack |

## What the numbers mean

- Everything above is *general* dialogue. It contains slang because
  subtitles contain slang, not because anyone tagged it. Register
  ranking is therefore inferred locally (src/Register.cs): a per-language
  colloquial/formal lexicon scores each candidate line before the
  corpus order is applied, so a casual line outranks a flat one that
  matches equally well.
- A probe-based slang density was measured and then discarded: with
  hand-written profanity lists it is dominated by artefacts (Arabic
  clitics and substrings inflate it by an order of magnitude, and
  Italian film clusters can sample near zero). Volume and
  acceptance are the two figures that survive scrutiny.
- Acceptance is a junk filter, not an alignment-quality score. OPUS
  alignment confidence is not evaluated here.
- The sample is a prefix of the stream, so it reflects the first
  documents in the file. Counts good to roughly a percentage point.
- Nearly every language here can fill a 2M pack. Dialogue supply is
  not what separates them; shipping size and alignment quality decide
  how much usable dialogue a pack holds.

## Word counting for Chinese and Japanese

The acceptance filter counted words by splitting on spaces, so an
unspaced sentence counted as one word and failed the two-word minimum.
Characters in Han and kana scripts are word units now. Both figures are
in the table's acceptance column: `qalam` is the old whitespace count,
`aware` is the shipped rule.
- Chinese (Simplified): 43.5% under the whitespace count, 96.1% after.
- Chinese (Traditional): 42.0% under the whitespace count, 95.4% after.
- Japanese: 46.5% under the whitespace count, 92.5% after.
