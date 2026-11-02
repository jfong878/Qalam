Larger OpenSubtitles seed packs (2026-11-01)
- Input: first 150 MB (compressed) of OPUS OpenSubtitles v2024 en-es and ar-en TMX.
- Builder: Qalam --buildconcordance --both --limit 50000 --sample.
- en-es / es-en: 50,000 pairs each, 4,008,063 bytes per direction, 133.9 s.
- ar-en / en-ar: 50,000 pairs each, 5,066,375 bytes per direction, 135.1 s.
- Cold --findconcordance (process start + pack load): cabron 941/1083 ms; kitab 1233/1261 ms.
- Previous 20k seed baseline: cabron 1234 ms; kitab 558 ms (single runs).
- Installed packs kept; temporary TMX prefixes were deleted after the build.
