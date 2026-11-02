// Local spaced-repetition deck. Cards come from saved translations; scheduling
// is deliberately simple (an SM-2-style interval/ease model) and stays on disk.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Qalam
{
    internal enum StudyGrade
    {
        Again,
        Hard,
        Good,
        Easy
    }

    internal sealed class StudyCard
    {
        public string Source { get; set; }
        public string Translation { get; set; }
        public string Detected { get; set; }
        public string Target { get; set; }
        public DateTime Added { get; set; }
        public DateTime Due { get; set; }
        public DateTime Reviewed { get; set; }
        public int IntervalDays { get; set; }
        public int Repetitions { get; set; }
        public int Lapses { get; set; }
        public double Ease { get; set; }
    }

    internal sealed class StudyStats
    {
        public int Total { get; set; }
        public int Due { get; set; }
        public int NewCards { get; set; }
        public int ReviewedToday { get; set; }
        public DateTime? NextDue { get; set; }
    }

    internal static class StudyScheduler
    {
        public static void Apply(StudyCard card, StudyGrade grade, DateTime now)
        {
            if (card == null) return;
            if (card.Ease <= 0) card.Ease = 2.5;
            card.Reviewed = now;

            if (grade == StudyGrade.Again)
            {
                card.Lapses++;
                card.Repetitions = 0;
                card.IntervalDays = 0;
                card.Ease = Math.Max(1.3, card.Ease - 0.20);
                card.Due = now.AddMinutes(10);
                return;
            }

            card.Repetitions++;
            if (grade == StudyGrade.Hard)
            {
                card.IntervalDays = Math.Max(1, (int)Math.Round(Math.Max(card.IntervalDays, 1) * 1.2));
                card.Ease = Math.Max(1.3, card.Ease - 0.15);
            }
            else if (grade == StudyGrade.Good)
            {
                if (card.Repetitions == 1) card.IntervalDays = 1;
                else if (card.Repetitions == 2) card.IntervalDays = 3;
                else card.IntervalDays = Math.Max(1, (int)Math.Round(Math.Max(card.IntervalDays, 1) * card.Ease));
            }
            else
            {
                if (card.Repetitions == 1) card.IntervalDays = 3;
                else card.IntervalDays = Math.Max(4, (int)Math.Round(Math.Max(card.IntervalDays, 1) * card.Ease * 1.3));
                card.Ease = Math.Min(3.0, card.Ease + 0.15);
            }

            if (card.IntervalDays > 365) card.IntervalDays = 365;
            card.Due = now.AddDays(card.IntervalDays);
        }
    }

    internal static class StudyStore
    {
        private const int MaxCards = 1000;
        private static readonly object Gate = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static string FilePath { get { return Path.Combine(Paths.StoreDir, "study.json"); } }

        public static List<StudyCard> Load()
        {
            lock (Gate) { return LoadUnlocked(); }
        }

        public static List<StudyCard> All()
        {
            List<StudyCard> cards = Load();
            cards.Sort(delegate(StudyCard a, StudyCard b)
            {
                int due = a.Due.CompareTo(b.Due);
                return due != 0 ? due : a.Added.CompareTo(b.Added);
            });
            return cards;
        }

        public static List<StudyCard> Due()
        {
            DateTime now = DateTime.Now;
            List<StudyCard> cards = All();
            List<StudyCard> due = new List<StudyCard>();
            foreach (StudyCard card in cards)
            {
                if (card.Due <= now) due.Add(card);
            }
            return due;
        }

        public static StudyStats Stats()
        {
            List<StudyCard> cards = Load();
            DateTime now = DateTime.Now;
            StudyStats stats = new StudyStats();
            stats.Total = cards.Count;
            foreach (StudyCard card in cards)
            {
                if (card.Due <= now) stats.Due++;
                if (card.Repetitions == 0) stats.NewCards++;
                if (card.Reviewed != default(DateTime) && card.Reviewed.Date == now.Date) stats.ReviewedToday++;
                if (card.Due > now && (!stats.NextDue.HasValue || card.Due < stats.NextDue.Value))
                    stats.NextDue = card.Due;
            }
            return stats;
        }

        public static int Add(HistoryEntry entry)
        {
            if (entry == null) return 0;
            return Import(new HistoryEntry[] { entry });
        }

        public static int Import(IEnumerable<HistoryEntry> entries)
        {
            if (entries == null) return 0;
            lock (Gate)
            {
                List<StudyCard> cards = LoadUnlocked();
                int added = 0;
                DateTime now = DateTime.Now;
                foreach (HistoryEntry entry in entries)
                {
                    if (cards.Count >= MaxCards) break;
                    if (entry == null || string.IsNullOrEmpty(entry.Source) || string.IsNullOrEmpty(entry.Translation))
                        continue;
                    bool exists = false;
                    foreach (StudyCard existing in cards)
                    {
                        if (string.Equals(existing.Source, entry.Source, StringComparison.Ordinal) &&
                            string.Equals(existing.Target, entry.Target, StringComparison.Ordinal))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (exists) continue;

                    StudyCard card = new StudyCard();
                    card.Source = entry.Source;
                    card.Translation = entry.Translation;
                    card.Detected = entry.Detected;
                    card.Target = entry.Target;
                    card.Added = now;
                    card.Due = now;
                    card.Ease = 2.5;
                    cards.Add(card);
                    added++;
                }
                if (added > 0) SaveUnlocked(cards);
                return added;
            }
        }

        public static StudyCard Grade(StudyCard card, StudyGrade grade)
        {
            if (card == null) return null;
            lock (Gate)
            {
                List<StudyCard> cards = LoadUnlocked();
                StudyCard stored = null;
                foreach (StudyCard candidate in cards)
                {
                    if (string.Equals(candidate.Source, card.Source, StringComparison.Ordinal) &&
                        string.Equals(candidate.Target, card.Target, StringComparison.Ordinal))
                    {
                        stored = candidate;
                        break;
                    }
                }
                if (stored == null)
                {
                    stored = card;
                    cards.Add(stored);
                }
                StudyScheduler.Apply(stored, grade, DateTime.Now);
                SaveUnlocked(cards);
                return stored;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                try { if (File.Exists(FilePath)) File.Delete(FilePath); }
                catch (Exception ex) { Logger.Write("study clear: " + ex.Message); }
            }
        }

        private static List<StudyCard> LoadUnlocked()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<StudyCard>();
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                List<StudyCard> cards = Json.Deserialize<List<StudyCard>>(json);
                if (cards == null) return new List<StudyCard>();
                DateTime now = DateTime.Now;
                foreach (StudyCard card in cards)
                {
                    if (card.Ease <= 0) card.Ease = 2.5;
                    if (card.Due == default(DateTime)) card.Due = now;
                    if (card.Source == null) card.Source = "";
                    if (card.Translation == null) card.Translation = "";
                }
                cards.RemoveAll(delegate(StudyCard card) { return string.IsNullOrEmpty(card.Source); });
                return cards;
            }
            catch (Exception ex)
            {
                Logger.Write("study load: " + ex.Message);
                return new List<StudyCard>();
            }
        }

        private static void SaveUnlocked(List<StudyCard> cards)
        {
            try
            {
                Directory.CreateDirectory(Paths.StoreDir);
                File.WriteAllText(FilePath, Json.Serialize(cards), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logger.Write("study save: " + ex.Message);
            }
        }
    }
}
