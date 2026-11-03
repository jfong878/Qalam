// Binary qidx sidecar for large concordance packs.
//
// The TSV stays the interchange format. A qidx next to a TSV lets the app read
// only candidate rows and the token directory instead of materializing every
// pair and every posting list in memory. The format is intentionally small:
//   header, row offset table, row blob, token directory, token blob, postings.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Qalam
{
    internal interface IConcordancePack
    {
        string SourceLang { get; }
        string TargetLang { get; }
        string Corpus { get; }
        string License { get; }
        int PairCount { get; }
        List<ConcordanceHit> Search(string queryNorm, List<string> queryTokens,
                                    List<string> hintTokens, int max);
    }

    internal sealed class DiskConcordancePack : IConcordancePack
    {
        private sealed class TokenEntry
        {
            public long PostingsOffset;
            public int Count;
        }

        private readonly string _path;
        private readonly int _rowCount;
        private readonly long _rowOffsetsPos;
        private readonly long _rowBlobPos;
        private readonly long[] _rowOffsets;
        private readonly Dictionary<string, TokenEntry> _tokens;
        private readonly string _sourceLang;
        private readonly string _targetLang;
        private readonly string _corpus;
        private readonly string _license;

        public DiskConcordancePack(string path, string indexPath)
        {
            _path = indexPath;
            using (FileStream stream = File.OpenRead(indexPath))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                byte[] magic = reader.ReadBytes(8);
                if (Encoding.ASCII.GetString(magic) != "QLMIDX1\n")
                    throw new InvalidDataException("Not a Qalam qidx file.");
                int version = reader.ReadInt32();
                if (version != 1) throw new InvalidDataException("Unsupported qidx version.");
                _rowCount = reader.ReadInt32();
                _rowOffsetsPos = reader.ReadInt64();
                _rowBlobPos = reader.ReadInt64();
                long tokenDirPos = reader.ReadInt64();
                int tokenCount = reader.ReadInt32();
                long tokenBlobPos = reader.ReadInt64();
                long postingsPos = reader.ReadInt64();
                _sourceLang = ReadString(reader);
                _targetLang = ReadString(reader);
                _corpus = ReadString(reader);
                _license = ReadString(reader);

                stream.Position = _rowOffsetsPos;
                _rowOffsets = new long[_rowCount + 1];
                for (int i = 0; i <= _rowCount; i++) _rowOffsets[i] = reader.ReadInt64();

                _tokens = new Dictionary<string, TokenEntry>(tokenCount, StringComparer.Ordinal);
                stream.Position = tokenDirPos;
                for (int i = 0; i < tokenCount; i++)
                {
                    long next = stream.Position + 24;
                    int tokenOffset = reader.ReadInt32();
                    int tokenLength = reader.ReadInt32();
                    long postingsOffset = reader.ReadInt64();
                    int count = reader.ReadInt32();
                    reader.ReadInt32();
                    long after = stream.Position;
                    stream.Position = tokenBlobPos + tokenOffset;
                    string token = Encoding.UTF8.GetString(reader.ReadBytes(tokenLength));
                    TokenEntry entry = new TokenEntry();
                    entry.PostingsOffset = postingsOffset;
                    entry.Count = count;
                    _tokens[token] = entry;
                    stream.Position = next;
                }
            }
        }

        public string SourceLang { get { return _sourceLang; } }
        public string TargetLang { get { return _targetLang; } }
        public string Corpus { get { return _corpus; } }
        public string License { get { return _license; } }
        public int PairCount { get { return _rowCount; } }

        public List<ConcordanceHit> Search(string queryNorm, List<string> queryTokens,
                                           List<string> hintTokens, int max)
        {
            List<ConcordanceHit> hits = new List<ConcordanceHit>();
            if (_rowCount == 0 || queryTokens == null || queryTokens.Count == 0 || max <= 0)
                return hits;

            TokenEntry smallest = null;
            int present = 0;
            foreach (string token in queryTokens)
            {
                TokenEntry entry;
                if (!_tokens.TryGetValue(token, out entry) || entry.Count == 0) continue;
                present++;
                if (smallest == null || entry.Count < smallest.Count) smallest = entry;
            }
            if (smallest == null) return hits;

            int threshold = queryTokens.Count <= 3
                ? queryTokens.Count
                : (int)Math.Ceiling(queryTokens.Count * 0.6);
            if (present < threshold) return hits;

            using (FileStream stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                int take = Math.Min(smallest.Count, 25000);
                stream.Position = smallest.PostingsOffset;
                for (int i = 0; i < take; i++)
                {
                    int id = reader.ReadInt32();
                    ConcordancePair pair = ReadPair(reader, id);
                    if (pair == null) continue;

                    int matched = 0;
                    foreach (string token in queryTokens)
                        if (ConcordanceText.HasToken(pair.SourceNorm, token)) matched++;
                    if (matched < threshold) continue;

                    bool exact = queryNorm.Length >= 3 &&
                                 pair.SourceNorm.IndexOf(queryNorm, StringComparison.Ordinal) >= 0;
                    if (!exact && queryTokens.Count <= 3 && matched < queryTokens.Count) continue;

                    double score = matched * 100.0;
                    if (exact) score += 500.0;
                    int words = pair.SourceWords;
                    if (words >= 4 && words <= 18) score += 30.0;
                    else score -= Math.Abs(words - 7) * 2.0;
                    if (pair.Source.Length > 160) score -= 10.0;

                    if (hintTokens != null && hintTokens.Count > 0)
                    {
                        string targetNorm = pair.TargetNorm;
                        if (targetNorm == null)
                        {
                            targetNorm = ConcordanceText.Normalize(pair.Target);
                            pair.TargetNorm = targetNorm;
                        }
                        int hintHits = 0;
                        foreach (string token in hintTokens)
                        {
                            if (ConcordanceText.HasToken(targetNorm, token)) hintHits++;
                            if (hintHits >= 4) break;
                        }
                        score += hintHits * 12.0;
                    }

                    ConcordanceHit hit = new ConcordanceHit();
                    hit.Pair = pair;
                    hit.Score = score;
                    hit.Exact = exact;
                    hits.Add(hit);
                }
            }

            hits.Sort(delegate(ConcordanceHit a, ConcordanceHit b)
            {
                int compare = b.Score.CompareTo(a.Score);
                if (compare != 0) return compare;
                compare = a.Pair.Source.Length.CompareTo(b.Pair.Source.Length);
                if (compare != 0) return compare;
                return string.Compare(a.Pair.Source, b.Pair.Source, StringComparison.Ordinal);
            });
            Register.Rerank(hits, max, _sourceLang, _targetLang, queryTokens);

            if (hits.Count > max) hits.RemoveRange(max, hits.Count - max);
            return hits;
        }

        private ConcordancePair ReadPair(BinaryReader reader, int id)
        {
            if (id < 0 || id >= _rowCount) return null;
            long start = _rowOffsets[id];
            long end = _rowOffsets[id + 1];
            if (end - start < 8 || end - start > 4096) return null;
            reader.BaseStream.Position = _rowBlobPos + start;
            int sourceLength = reader.ReadInt32();
            int targetLength = reader.ReadInt32();
            if (sourceLength < 0 || targetLength < 0 || sourceLength > 4096 || targetLength > 4096)
                return null;
            if (8L + sourceLength + targetLength > end - start) return null;

            string source = Encoding.UTF8.GetString(reader.ReadBytes(sourceLength));
            string target = Encoding.UTF8.GetString(reader.ReadBytes(targetLength));
            ConcordancePair pair = new ConcordancePair();
            pair.Source = source;
            pair.Target = target;
            pair.SourceNorm = ConcordanceText.Normalize(source);
            pair.TargetNorm = null;
            pair.SourceWords = ConcordanceText.WordCountClean(source);
            pair.Corpus = _corpus;
            pair.License = _license;
            return pair;
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 65536) throw new InvalidDataException("Bad qidx string.");
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }
    }

    internal static class ConcordanceDiskIndex
    {
        public static string Write(string tsvPath, List<ConcordancePair> pairs, bool reverse,
                                   string sourceLang, string targetLang, string corpus, string license)
        {
            if (string.IsNullOrEmpty(tsvPath) || pairs == null || pairs.Count == 0) return null;
            string indexPath = tsvPath + ".qidx";
            string temp = indexPath + ".tmp";

            UTF8Encoding utf8 = new UTF8Encoding(false);
            byte[] sourceBytes = utf8.GetBytes(sourceLang ?? "");
            byte[] targetBytes = utf8.GetBytes(targetLang ?? "");
            byte[] corpusBytes = utf8.GetBytes(corpus ?? "");
            byte[] licenseBytes = utf8.GetBytes(license ?? "");
            int headerLength = 76 + sourceBytes.Length + targetBytes.Length +
                               corpusBytes.Length + licenseBytes.Length;

            List<long> rowOffsets = new List<long>(pairs.Count + 1);
            Dictionary<string, List<int>> postings =
                new Dictionary<string, List<int>>(StringComparer.Ordinal);
            long rowLength = 0;

            using (MemoryStream rowBlob = new MemoryStream())
            {
                using (BinaryWriter rows = new BinaryWriter(rowBlob, utf8))
                {
                    for (int id = 0; id < pairs.Count; id++)
                    {
                        ConcordancePair pair = pairs[id];
                        string source = reverse ? pair.Target : pair.Source;
                        string target = reverse ? pair.Source : pair.Target;
                        byte[] sourceData = utf8.GetBytes(source ?? "");
                        byte[] targetData = utf8.GetBytes(target ?? "");
                        rowOffsets.Add(rowLength);
                        rows.Write(sourceData.Length);
                        rows.Write(targetData.Length);
                        rows.Write(sourceData);
                        rows.Write(targetData);
                        rowLength += 8 + sourceData.Length + targetData.Length;

                        string normalized = ConcordanceText.Normalize(source);
                        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (string token in ConcordanceText.Tokens(normalized))
                        {
                            string stem = ConcordanceText.ArabicStem(token);
                            if (!seen.Add(stem)) continue;
                            List<int> ids;
                            if (!postings.TryGetValue(stem, out ids))
                            {
                                ids = new List<int>();
                                postings[stem] = ids;
                            }
                            ids.Add(id);
                        }
                    }
                    rowOffsets.Add(rowLength);
                }

                string[] tokens = new string[postings.Count];
                postings.Keys.CopyTo(tokens, 0);
                Array.Sort(tokens, StringComparer.Ordinal);

                byte[][] tokenBytes = new byte[tokens.Length][];
                int[] tokenOffsets = new int[tokens.Length];
                long tokenBlobLength = 0;
                for (int i = 0; i < tokens.Length; i++)
                {
                    tokenBytes[i] = utf8.GetBytes(tokens[i]);
                    tokenOffsets[i] = (int)tokenBlobLength;
                    tokenBlobLength += tokenBytes[i].Length;
                }

                long rowOffsetsPos = headerLength;
                long rowBlobPos = rowOffsetsPos + (long)(pairs.Count + 1) * 8;
                long tokenDirPos = rowBlobPos + rowLength;
                long tokenBlobPos = tokenDirPos + (long)tokens.Length * 24;
                long postingsPos = tokenBlobPos + tokenBlobLength;

                using (FileStream file = File.Create(temp))
                using (BinaryWriter writer = new BinaryWriter(file, utf8))
                {
                    writer.Write(Encoding.ASCII.GetBytes("QLMIDX1\n"));
                    writer.Write(1);
                    writer.Write(pairs.Count);
                    writer.Write(rowOffsetsPos);
                    writer.Write(rowBlobPos);
                    writer.Write(tokenDirPos);
                    writer.Write(tokens.Length);
                    writer.Write(tokenBlobPos);
                    writer.Write(postingsPos);
                    WriteString(writer, sourceBytes);
                    WriteString(writer, targetBytes);
                    WriteString(writer, corpusBytes);
                    WriteString(writer, licenseBytes);

                    for (int i = 0; i < rowOffsets.Count; i++) writer.Write(rowOffsets[i]);
                    writer.Write(rowBlob.ToArray());

                    long postingsOffset = postingsPos;
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        List<int> ids = postings[tokens[i]];
                        writer.Write(tokenOffsets[i]);
                        writer.Write(tokenBytes[i].Length);
                        writer.Write(postingsOffset);
                        writer.Write(ids.Count);
                        writer.Write(0);
                        postingsOffset += (long)ids.Count * 4;
                    }

                    for (int i = 0; i < tokenBytes.Length; i++) writer.Write(tokenBytes[i]);
                    for (int i = 0; i < tokens.Length; i++)
                    {
                        List<int> ids = postings[tokens[i]];
                        for (int j = 0; j < ids.Count; j++) writer.Write(ids[j]);
                    }
                }
            }

            if (File.Exists(indexPath)) File.Delete(indexPath);
            File.Move(temp, indexPath);
            return indexPath;
        }

        private static void WriteString(BinaryWriter writer, byte[] value)
        {
            writer.Write(value.Length);
            writer.Write(value);
        }
    }
}
