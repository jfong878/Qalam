# Qalam lexicon packs

These compressed TSV packs power the **Senses** card for single words. They are
local reference data; the translation engine is unchanged.

| File | Content | Source | License |
| --- | --- | --- | --- |
| `ar-lane.tsv.gz` | Classical Arabic root senses | Edward William Lane, *An Arabic-English Lexicon* (1863–93), Perseus digitization | Public-domain text; Perseus XML CC BY-SA 3.0 |
| `zh-cedict.tsv.gz` | Modern Mandarin words and glosses | [CC-CEDICT](https://www.mdbg.net/chinese/dictionary?page=cc-cedict) | CC BY-SA 4.0 |

Lane's text is attributed to:

> Text provided by Perseus Digital Library, with funding from The U.S.
> Department of Education and The Max Planck Society.

CC-CEDICT is community-maintained and is distributed under the Creative Commons
Attribution-ShareAlike 4.0 International License.

## Regenerate

```powershell
python tools/build_lexicon.py
```

The builder caches downloads under `.launch-check/lexicon-src` (gitignored) and
writes the packs above. Lane's XML uses a transliteration that cannot always be
converted back to a form safely, so the Arabic pack is indexed by **root** and
keeps Lane's transliteration as metadata. The card shows the root and the
English gloss; it does not claim a reconstructed vocalized form.

Hans Wehr's *Dictionary of Modern Written Arabic* is still in copyright and is
not bundled. Lane is classical; CC-CEDICT is modern Mandarin. Neither pack is a
substitute for reading the full entry or checking the context.
